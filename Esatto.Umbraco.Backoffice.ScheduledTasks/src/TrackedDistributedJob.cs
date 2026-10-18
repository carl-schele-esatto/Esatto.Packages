using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// Wraps a distributed job for one execution: skips it while paused (unless run manually) or when
/// it was handed out off its calendar schedule, and records the run. Exceptions are rethrown so Umbraco still logs them and finishes the job.
/// </summary>
internal sealed class TrackedDistributedJob : IDistributedBackgroundJob
{
    private readonly IDistributedBackgroundJob _inner;
    private readonly ScheduledTaskTrigger _trigger;
    private readonly string? _triggeredBy;
    private readonly IScheduledTaskSettingsStore _settings;
    private readonly IScheduledTaskRunLog _runLog;
    private readonly ILogger _logger;
    private readonly string? _skipReason;

    public TrackedDistributedJob(
        IDistributedBackgroundJob inner,
        ScheduledTaskTrigger trigger,
        string? triggeredBy,
        IScheduledTaskSettingsStore settings,
        IScheduledTaskRunLog runLog,
        ILogger logger,
        string? skipReason = null)
    {
        _inner = inner;
        _trigger = trigger;
        _triggeredBy = triggeredBy;
        _settings = settings;
        _runLog = runLog;
        _logger = logger;
        _skipReason = skipReason;
    }

    public string Name => _inner.Name;

    public TimeSpan Period => _inner.Period;

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

        if (_trigger == ScheduledTaskTrigger.Schedule && _settings.Get(taskId)?.IsPaused == true)
        {
            // Returning normally lets Umbraco stamp LastRun, so the job comes due again one period later.
            _logger.LogDebug("Distributed job {JobName} not running as it is paused in the Scheduled Tasks dashboard", Name);
            return;
        }

        var runId = _runLog.Start(taskId, _trigger, _triggeredBy);
        try
        {
            await _inner.ExecuteAsync(cancellationToken);
            _runLog.Succeed(taskId, runId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _runLog.Cancel(taskId, runId);
            throw;
        }
        catch (Exception ex)
        {
            _runLog.Fail(taskId, runId, ex);
            throw;
        }
    }
}
