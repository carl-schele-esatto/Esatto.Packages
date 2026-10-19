using System.Collections.Concurrent;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// The task executions in progress on this server (a run and its retries), so the dashboard can
/// stop one. Stopping cancels the <see cref="CancellationToken"/> the job got: it's up to the job
/// to observe it.
/// </summary>
public sealed class ScheduledTaskRunControl
{
    private readonly ConcurrentDictionary<string, ScheduledTaskExecution> _executions = new(StringComparer.Ordinal);
    private readonly IScheduledTaskRunLog _runLog;
    private readonly TimeProvider _timeProvider;

    public ScheduledTaskRunControl(IScheduledTaskRunLog runLog, TimeProvider timeProvider)
    {
        _runLog = runLog;
        _timeProvider = timeProvider;
    }

    /// <summary>Starts tracking an execution of the task; dispose it when the execution ends.</summary>
    /// <param name="stoppingToken">Canceled when the application shuts down.</param>
    internal ScheduledTaskExecution Begin(string taskId, CancellationToken stoppingToken)
    {
        var execution = new ScheduledTaskExecution(this, taskId, stoppingToken);
        _executions[taskId] = execution;
        return execution;
    }

    /// <summary>Asks the task's execution on this server to stop. False when none is in progress here.</summary>
    internal bool RequestStop(string taskId, string? requestedBy)
        => _executions.TryGetValue(taskId, out var execution) && execution.RequestStop(requestedBy);

    /// <summary>Asks the execution running <paramref name="runId"/> to stop (a request made on another server).</summary>
    internal bool RequestStopOfRun(Guid runId, string? requestedBy)
        => _executions.Values.FirstOrDefault(e => e.CurrentRunId == runId) is { } execution && execution.RequestStop(requestedBy);

    internal bool IsExecuting(string taskId) => _executions.ContainsKey(taskId);

    internal bool HasExecutions => !_executions.IsEmpty;

    /// <summary>Who asked the task's execution here to stop; null when nobody did (or none is in progress).</summary>
    internal string? StopRequestedBy(string taskId) => _executions.TryGetValue(taskId, out var execution) ? execution.StopRequestedBy : null;

    internal TimeProvider TimeProvider => _timeProvider;

    internal IScheduledTaskRunLog RunLog => _runLog;

    internal void End(ScheduledTaskExecution execution)
        => _executions.TryRemove(KeyValuePair.Create(execution.TaskId, execution));
}

/// <summary>One execution of a task on this server: its run, and any retries in-process.</summary>
internal sealed class ScheduledTaskExecution : IDisposable
{
    private readonly ScheduledTaskRunControl _control;
    private readonly CancellationTokenSource _stop;
    private readonly CancellationToken _stoppingToken;
    private string? _stopRequestedBy;

    internal ScheduledTaskExecution(ScheduledTaskRunControl control, string taskId, CancellationToken stoppingToken)
    {
        _control = control;
        _stoppingToken = stoppingToken;
        TaskId = taskId;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
    }

    public string TaskId { get; }

    /// <summary>Canceled when someone stops the execution or the application shuts down.</summary>
    public CancellationToken Token => _stop.Token;

    /// <summary>The attempt in progress, so a stop request is recorded on it.</summary>
    public Guid? CurrentRunId { get; set; }

    public string? StopRequestedBy => Volatile.Read(ref _stopRequestedBy);

    public bool IsStopRequested => StopRequestedBy is not null;

    public bool IsShuttingDown => _stoppingToken.IsCancellationRequested;

    public bool RequestStop(string? requestedBy)
    {
        var by = string.IsNullOrWhiteSpace(requestedBy) ? "someone" : requestedBy;
        if (Interlocked.CompareExchange(ref _stopRequestedBy, by, null) is not null) return true; // already asked

        if (CurrentRunId is { } runId) _control.RunLog.MarkStopRequested(TaskId, runId, by);
        try
        {
            // The job's cancellation callbacks run on the thread pool, not on the caller's request.
            _ = _stop.CancelAsync();
        }
        catch (ObjectDisposedException)
        {
            // Just finished.
        }

        return true;
    }

    /// <summary>A token for one attempt: canceled on stop, shutdown, or after <paramref name="timeout"/>.</summary>
    public ScheduledTaskAttempt BeginAttempt(Guid runId, TimeSpan? timeout)
    {
        CurrentRunId = runId;
        return new ScheduledTaskAttempt(Token, timeout, _control.TimeProvider);
    }

    public void Dispose()
    {
        _control.End(this);
        _stop.Dispose();
    }
}

/// <summary>One attempt's token; <see cref="TimedOut"/> tells a time limit from a stop or shutdown.</summary>
internal sealed class ScheduledTaskAttempt : IDisposable
{
    private readonly CancellationTokenSource? _timeout;
    private readonly CancellationTokenSource? _linked;

    internal ScheduledTaskAttempt(CancellationToken executionToken, TimeSpan? timeout, TimeProvider timeProvider)
    {
        if (timeout is not { } limit || limit <= TimeSpan.Zero)
        {
            Token = executionToken;
            return;
        }

        _timeout = new CancellationTokenSource(limit, timeProvider);
        _linked = CancellationTokenSource.CreateLinkedTokenSource(executionToken, _timeout.Token);
        Token = _linked.Token;
    }

    public CancellationToken Token { get; }

    public bool TimedOut => _timeout?.IsCancellationRequested == true;

    public void Dispose()
    {
        _linked?.Dispose();
        _timeout?.Dispose();
    }
}
