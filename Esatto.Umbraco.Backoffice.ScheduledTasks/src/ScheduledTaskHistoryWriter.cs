using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// Every <see cref="ScheduledTasksOptions.RunHistoryFlushInterval"/>: writes this server's new and
/// changed runs to the database, picks up stop requests made on other servers for runs in progress
/// here, and (hourly) deletes runs and change log entries past their retention. At startup it
/// closes runs this server left "running" when it last stopped; at shutdown it writes what's left.
/// </summary>
internal sealed class ScheduledTaskHistoryWriter : BackgroundService, IHostedLifecycleService
{
    private static readonly TimeSpan RetentionInterval = TimeSpan.FromHours(1);

    private readonly IScheduledTaskRunLog _runLog;
    private readonly IScheduledTaskRunStore _store;
    private readonly ScheduledTaskRunControl _runControl;
    private readonly IScheduledTaskAuditLog _auditLog;
    private readonly IOptionsMonitor<ScheduledTasksOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ScheduledTaskHistoryWriter> _logger;
    private readonly DateTime _startedUtc;
    private readonly string _server = Environment.MachineName;

    private bool _interruptedClosed;
    private long _prunedAt;

    public ScheduledTaskHistoryWriter(
        IScheduledTaskRunLog runLog,
        IScheduledTaskRunStore store,
        ScheduledTaskRunControl runControl,
        IScheduledTaskAuditLog auditLog,
        IOptionsMonitor<ScheduledTasksOptions> options,
        TimeProvider timeProvider,
        ILogger<ScheduledTaskHistoryWriter> logger)
    {
        _runLog = runLog;
        _store = store;
        _runControl = runControl;
        _auditLog = auditLog;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
        _startedUtc = timeProvider.GetUtcNow().UtcDateTime;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // As Umbraco does for its job hosts: don't let the loop capture the host's ExecutionContext.
        using (ExecutionContext.IsFlowSuppressed() ? null : (IDisposable?)ExecutionContext.SuppressFlow())
        {
            return base.StartAsync(cancellationToken);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.CurrentValue.EffectiveRunHistoryFlushInterval, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            Tick();
        }
    }

    /// <summary>One round of the work above; does nothing until the package's tables exist.</summary>
    internal void Tick()
    {
        if (!_store.IsAvailable) return;

        Try(CloseInterruptedRuns, "close the runs interrupted by the last shutdown");
        if (_options.CurrentValue.PersistRunHistory) (_runLog as ScheduledTaskRunLog)?.Flush();
        Try(PickUpStopRequests, "check for stop requests");
        Try(SendHeartbeat, "mark its runs as alive");
        Try(Prune, "delete old run history");
    }

    // How long a saved run may go without its server's heartbeat before any server closes it: the
    // server is gone (a replaced container, a scaled-in instance) and never comes back to close it.
    internal static readonly TimeSpan AbandonedAfter = TimeSpan.FromMinutes(15);

    private void SendHeartbeat()
    {
        if (_runControl.HasExecutions && _options.CurrentValue.PersistRunHistory)
            _store.Heartbeat(_server, _timeProvider.GetUtcNow().UtcDateTime);
    }

    private void CloseInterruptedRuns()
    {
        if (_interruptedClosed) return;
        var closed = _store.MarkInterrupted(_server, _startedUtc);
        _interruptedClosed = true;
        if (closed > 0) _logger.LogInformation("Marked {Count} scheduled task run(s) interrupted by the last shutdown as canceled", closed);
    }

    private void PickUpStopRequests()
    {
        // Only runs in progress here can be stopped here; don't query for nothing.
        if (!_runControl.HasExecutions) return;
        foreach (var (runId, requestedBy) in _store.GetStopRequests(_server))
            _runControl.RequestStopOfRun(runId, requestedBy);
    }

    private void Prune()
    {
        var last = Interlocked.Read(ref _prunedAt);
        if (last != 0 && _timeProvider.GetElapsedTime(last) < RetentionInterval) return;
        Interlocked.Exchange(ref _prunedAt, _timeProvider.GetTimestamp());

        var options = _options.CurrentValue;
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (options.RunHistoryRetention > TimeSpan.Zero) _store.PruneOlderThan(now - options.RunHistoryRetention);
        if (options.AuditRetention > TimeSpan.Zero && _auditLog is ScheduledTaskAuditLog audit) audit.PruneOlderThan(now - options.AuditRetention);

        // Runs left behind by servers that no longer exist, and retries long overdue (e.g. of a
        // paused task): no server will finish them, so they'd show as running/pending forever.
        var closed = _store.CloseAbandoned(now - AbandonedAfter);
        if (closed > 0) _logger.LogInformation("Closed {Count} scheduled task run(s) abandoned by servers that no longer report", closed);
    }

    private void Try(Action action, string what)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Scheduled Tasks could not {What}", what);
        }
    }

    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>After every hosted service (the jobs included) has stopped: write the last runs.</summary>
    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        if (_store.IsAvailable && _options.CurrentValue.PersistRunHistory)
            Try(() => (_runLog as ScheduledTaskRunLog)?.Flush(), "save the last runs");
        return Task.CompletedTask;
    }
}
