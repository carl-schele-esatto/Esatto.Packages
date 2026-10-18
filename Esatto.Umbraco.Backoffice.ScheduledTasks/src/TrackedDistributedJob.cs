using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// Wraps a distributed job for one execution: skips it while paused (unless run manually) or when
/// it was handed out off its calendar schedule, runs it within its time limit (stoppable from the
/// dashboard), records the run, and on a failure schedules a retry if its run policy allows one.
/// Exceptions are rethrown so Umbraco still logs them and finishes the job.
/// </summary>
/// <remarks>
/// A retry isn't waited for here: Umbraco's poller runs one distributed job at a time, so waiting
/// would hold up all of them (scheduled publishing included). The failed run records when it's
/// tried again; <see cref="ManagedDistributedJobService"/> then gives the job's row the period that
/// makes it due then, on whichever server polls first.
/// </remarks>
internal sealed class TrackedDistributedJob : IDistributedBackgroundJob
{
    private readonly IDistributedBackgroundJob _inner;
    private readonly ScheduledTaskTrigger _trigger;
    private readonly string? _triggeredBy;
    private readonly IScheduledTaskSettingsStore _settings;
    private readonly IScheduledTaskRunLog _runLog;
    private readonly ILogger _logger;
    private readonly string? _skipReason;
    private readonly ScheduledTaskRunControl? _runControl;
    private readonly ScheduledTasksOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ScheduledTaskRun? _retryOf;

    public TrackedDistributedJob(
        IDistributedBackgroundJob inner,
        ScheduledTaskTrigger trigger,
        string? triggeredBy,
        IScheduledTaskSettingsStore settings,
        IScheduledTaskRunLog runLog,
        ILogger logger,
        string? skipReason = null,
        ScheduledTaskRunControl? runControl = null,
        ScheduledTasksOptions? options = null,
        TimeProvider? timeProvider = null,
        ScheduledTaskRun? retryOf = null)
    {
        _inner = inner;
        _trigger = trigger;
        _triggeredBy = triggeredBy;
        _settings = settings;
        _runLog = runLog;
        _logger = logger;
        _skipReason = skipReason;
        _runControl = runControl;
        _options = options ?? new ScheduledTasksOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _retryOf = retryOf;
    }

    public string Name => _inner.Name;

    public TimeSpan Period => _inner.Period;

    /// <summary>The job being tracked (Umbraco's own wrapper type isn't the job's).</summary>
    internal IDistributedBackgroundJob Inner => _inner;

    public Task ExecuteAsync() => ExecuteAsync(CancellationToken.None);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var taskId = ScheduledTaskIdentity.ForDistributed(Name);

        if (_skipReason is not null)
        {
            // As when paused: returning normally lets Umbraco finish the job, which reschedules it.
            _logger.LogDebug("Distributed job {JobName} not running as {Reason}", Name, _skipReason);
            return;
        }

        var setting = _settings.Get(taskId);
        if (_trigger != ScheduledTaskTrigger.Manual && setting?.IsPaused == true)
        {
            // A pause also drops a pending retry. Returning normally lets Umbraco stamp LastRun, so
            // the job comes due again one period later.
            if (_retryOf is not null) _runLog.SetRetry(taskId, _retryOf.Id, null);
            _logger.LogDebug("Distributed job {JobName} not running as it is paused in the Scheduled Tasks dashboard", Name);
            return;
        }

        // This run is the retry: it's no longer pending (on any server).
        if (_retryOf is not null) _runLog.SetRetry(taskId, _retryOf.Id, null);

        var policy = ScheduledTaskRunPolicies.Resolve(_inner.GetType(), setting?.RunPolicy);
        var attempt = _retryOf is null ? 1 : _retryOf.Attempt + 1;
        var maxAttempts = _retryOf?.MaxAttempts is { } max && max >= attempt ? max : Math.Max(attempt, policy.MaxAttempts);
        var persistAll = ScheduledTaskAttempts.PersistsAllRuns(
            setting?.PeriodOverride ?? _inner.Period, setting?.Calendar is not null, _options);

        var runControl = _runControl ?? new ScheduledTaskRunControl(_runLog, _timeProvider);
        using var execution = runControl.Begin(taskId, cancellationToken);
        var runId = _runLog.Start(taskId, _trigger, _triggeredBy, attempt, maxAttempts, persistAll);
        var result = await ScheduledTaskAttempts.RunAsync(
            _runLog, execution, taskId, runId, policy.Timeout, _options.RunLogMinimumLevel, _inner.ExecuteAsync);

        switch (result.Outcome)
        {
            case AttemptOutcome.ShutDown:
                throw new OperationCanceledException(cancellationToken);

            case AttemptOutcome.Failed:
                if (attempt < maxAttempts && !execution.IsStopRequested)
                    _runLog.SetRetry(taskId, runId, _timeProvider.GetUtcNow().UtcDateTime + policy.RetryDelay);
                result.Exception!.Throw();
                break;

            case AttemptOutcome.Stopped:
                _logger.LogInformation("Distributed job {JobName} was stopped from the Scheduled Tasks dashboard", Name);
                break;
        }
    }
}
