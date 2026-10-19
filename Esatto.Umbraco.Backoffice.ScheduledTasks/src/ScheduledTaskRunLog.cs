using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

public enum ScheduledTaskRunOutcome
{
    Running,
    Succeeded,
    Failed,
    Skipped,
    Canceled,
}

public enum ScheduledTaskTrigger
{
    /// <summary>The task's own schedule.</summary>
    Schedule,

    /// <summary>Run now, from the dashboard.</summary>
    Manual,

    /// <summary>Triggered from code through <c>IRecurringBackgroundJobTrigger&lt;TJob&gt;</c>.</summary>
    Code,

    /// <summary>Another attempt after a failed run (see <see cref="ScheduledTaskRunPolicy"/>).</summary>
    Retry,
}

/// <summary>
/// One execution (or skipped tick) of a task. Consecutive identical scheduled skips are
/// folded into one entry: <see cref="Occurrences"/> counts them and
/// <see cref="LastOccurrenceUtc"/> is the latest. <see cref="Progress"/> (0–100) and
/// <see cref="ProgressMessage"/> are the latest the job reported through <see cref="IScheduledTaskProgress"/>.
/// </summary>
public sealed record ScheduledTaskRun(
    Guid Id,
    string TaskId,
    ScheduledTaskTrigger Trigger,
    string? TriggeredBy,
    string Server,
    DateTime StartedUtc,
    DateTime? FinishedUtc,
    ScheduledTaskRunOutcome Outcome,
    string? Message,
    string? Details,
    int Occurrences,
    DateTime LastOccurrenceUtc,
    double? Progress = null,
    string? ProgressMessage = null)
{
    public TimeSpan? Duration => FinishedUtc - StartedUtc;

    /// <summary>Which attempt this is (1 = the first; more after failures, see <see cref="ScheduledTaskRunPolicy"/>).</summary>
    public int Attempt { get; init; } = 1;

    /// <summary>How many attempts the run's policy allowed when it started.</summary>
    public int MaxAttempts { get; init; } = 1;

    /// <summary>For a failed run: when it's tried again. Null when no retry is pending.</summary>
    public DateTime? RetryAtUtc { get; init; }

    /// <summary>Who asked the run to stop (a job only stops if it observes its cancellation token).</summary>
    public string? StopRequestedBy { get; init; }

    /// <summary>What the job (and the code it called) logged while it ran, one entry per line.</summary>
    public string? Log { get; init; }

    /// <summary>
    /// Kept in memory only unless it turns out notable (failed, stopped, retried): runs of tasks that
    /// run very often (see <see cref="ScheduledTasksOptions.PersistAllRunsFromPeriod"/>).
    /// </summary>
    internal bool Transient { get; init; }
}

/// <summary>Counters since startup plus the current and most recent finished run.</summary>
public sealed record ScheduledTaskRunStats(int Runs, int Failures, ScheduledTaskRun? Current, ScheduledTaskRun? Last)
{
    /// <summary>The failed run whose retry is pending (its <see cref="ScheduledTaskRun.RetryAtUtc"/>), if any.</summary>
    public ScheduledTaskRun? PendingRetry { get; init; }
}

/// <summary>
/// Records task runs. The runs on this server are kept in memory (the live view: what's running,
/// its progress and log) and, unless <see cref="ScheduledTasksOptions.PersistRunHistory"/> is off,
/// written to the database in batches, so the history survives restarts and shows every server's
/// runs. Capped per task by <see cref="ScheduledTasksOptions.RunHistoryLimit"/>.
/// </summary>
public interface IScheduledTaskRunLog
{
    Guid Start(string taskId, ScheduledTaskTrigger trigger, string? triggeredBy);

    /// <summary>Starts attempt <paramref name="attempt"/> of <paramref name="maxAttempts"/>.</summary>
    /// <param name="persistAll">False for tasks that run very often: only failed, stopped or retried runs are written to the database.</param>
    Guid Start(string taskId, ScheduledTaskTrigger trigger, string? triggeredBy, int attempt, int maxAttempts, bool persistAll)
        => Start(taskId, trigger, triggeredBy);

    void Succeed(string taskId, Guid runId);

    /// <summary>Succeeded, with a remark (e.g. it ran past its time limit).</summary>
    void Succeed(string taskId, Guid runId, string? message) => Succeed(taskId, runId);

    void Fail(string taskId, Guid runId, Exception exception);

    /// <summary>Failed, with <paramref name="message"/> instead of the exception's own (e.g. "Timed out after 10 minutes").</summary>
    void Fail(string taskId, Guid runId, Exception exception, string? message) => Fail(taskId, runId, exception);

    /// <summary>Canceled because the application is shutting down.</summary>
    void Cancel(string taskId, Guid runId);

    /// <summary>Canceled for <paramref name="reason"/> (e.g. "Stopped by Carl").</summary>
    void Cancel(string taskId, Guid runId, string? reason) => Cancel(taskId, runId);

    void Skip(string taskId, string reason, ScheduledTaskTrigger trigger, string? triggeredBy);

    /// <param name="persistAll">False for tasks that run very often: the skip isn't written to the database.</param>
    void Skip(string taskId, string reason, ScheduledTaskTrigger trigger, string? triggeredBy, bool persistAll)
        => Skip(taskId, reason, trigger, triggeredBy);

    /// <summary>
    /// Sets the progress of the task's run in progress on this server. A null percent keeps the
    /// previous one (message only). Returns false when no run of the task is in progress here.
    /// </summary>
    bool ReportProgress(string taskId, double? percent, string? message);

    /// <summary>Adds a line to a run's log (see <see cref="ScheduledTasksOptions.RunLogMaxLines"/>).</summary>
    void AppendLog(string taskId, Guid runId, string line) { }

    /// <summary>Records who asked a running run to stop.</summary>
    void MarkStopRequested(string taskId, Guid runId, string? requestedBy) { }

    /// <summary>
    /// Records when a failed run is tried again (null: no retry after all), and saves it at once so
    /// every server knows.
    /// </summary>
    void SetRetry(string taskId, Guid runId, DateTime? retryAtUtc) { }

    /// <summary>The task's failed run whose retry is pending, on any server; null when none is.</summary>
    /// <param name="fresh">Read the database now rather than a few seconds' cache, for decisions.</param>
    ScheduledTaskRun? GetPendingRetry(string taskId, bool fresh = false) => null;

    /// <summary>Runs for a task, newest first: every server's saved runs plus this server's latest.</summary>
    IReadOnlyList<ScheduledTaskRun> GetRuns(string taskId);

    ScheduledTaskRunStats GetStats(string taskId);

    /// <summary>Forgets a task's finished runs (here and in the database) and resets its counters. A run in progress is kept.</summary>
    void Clear(string taskId);
}

internal sealed class ScheduledTaskRunLog : IScheduledTaskRunLog
{
    private const int MaxDetailsLength = 8000;
    private const int MaxMessageLength = 2000;
    private const int MaxProgressMessageLength = 500;
    private const int MaxLogLineLength = 2000;

    private static readonly ScheduledTaskRunStats NoStats = new(0, 0, null, null);

    // How long the latest saved run per task is cached for the dashboard (one query per poll otherwise).
    private static readonly TimeSpan LatestCacheDuration = TimeSpan.FromSeconds(10);

    // How often one task's saved runs are trimmed to the history limit.
    private static readonly TimeSpan PruneInterval = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, TaskHistory> _histories = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly IOptionsMonitor<ScheduledTasksOptions> _options;
    private readonly IScheduledTaskRunStore? _store;
    private readonly ILogger _logger;
    private readonly string _server = Environment.MachineName;

    // Runs changed since the last flush: run id → task id.
    private readonly ConcurrentDictionary<Guid, string> _dirty = new();
    private readonly ConcurrentDictionary<string, long> _prunedAt = new(StringComparer.Ordinal);
    private readonly Lock _flushLock = new();

    // Frequent tasks' runs saved while running (see Flush): their end is saved however it turns out.
    private readonly ConcurrentDictionary<Guid, byte> _written = new();

    // A frequent task's run in progress longer than this is saved, so other servers can see and stop it.
    private static readonly TimeSpan LongRunning = TimeSpan.FromSeconds(30);

    // Changes waiting for a table that never comes (a failed migration) are dropped beyond this.
    private const int MaxPendingChanges = 10_000;

    private volatile LatestRuns? _latest;
    private long _storeWarnedAt;

    public ScheduledTaskRunLog(
        TimeProvider timeProvider,
        IOptionsMonitor<ScheduledTasksOptions> options,
        IScheduledTaskRunStore? store = null,
        ILogger<ScheduledTaskRunLog>? logger = null)
    {
        _timeProvider = timeProvider;
        _options = options;
        _store = store;
        _logger = logger ?? NullLogger<ScheduledTaskRunLog>.Instance;
    }

    public Guid Start(string taskId, ScheduledTaskTrigger trigger, string? triggeredBy)
        => Start(taskId, trigger, triggeredBy, 1, 1, persistAll: true);

    public Guid Start(string taskId, ScheduledTaskTrigger trigger, string? triggeredBy, int attempt, int maxAttempts, bool persistAll)
    {
        var now = UtcNow();
        var run = new ScheduledTaskRun(
            Guid.NewGuid(), taskId, trigger, Truncate(triggeredBy, 255), _server,
            now, null, ScheduledTaskRunOutcome.Running, null, null, 1, now)
        {
            Attempt = Math.Max(1, attempt),
            MaxAttempts = Math.Max(Math.Max(1, attempt), maxAttempts),
            Transient = !persistAll,
        };

        var history = History(taskId);
        lock (history)
        {
            history.Runs++;
            // A new run supersedes a retry this server had pending (it is that retry, or replaces it).
            ClearPendingRetries(history);
            history.Add(run, Limit());
            history.Logs[run.Id] = new LogBuffer();
        }

        MarkDirty(run);
        return run.Id;
    }

    public void Succeed(string taskId, Guid runId)
        => Finish(taskId, runId, ScheduledTaskRunOutcome.Succeeded, null, null);

    public void Succeed(string taskId, Guid runId, string? message)
        => Finish(taskId, runId, ScheduledTaskRunOutcome.Succeeded, message, null);

    public void Fail(string taskId, Guid runId, Exception exception)
        => Fail(taskId, runId, exception, null);

    public void Fail(string taskId, Guid runId, Exception exception, string? message)
        => Finish(
            taskId,
            runId,
            ScheduledTaskRunOutcome.Failed,
            message ?? $"{exception.GetType().Name}: {exception.Message}",
            Truncate(exception.ToString(), MaxDetailsLength));

    public void Cancel(string taskId, Guid runId)
        => Cancel(taskId, runId, "Canceled because the application is shutting down.");

    public void Cancel(string taskId, Guid runId, string? reason)
        => Finish(taskId, runId, ScheduledTaskRunOutcome.Canceled, reason, null);

    public void Skip(string taskId, string reason, ScheduledTaskTrigger trigger, string? triggeredBy)
        => Skip(taskId, reason, trigger, triggeredBy, persistAll: true);

    public void Skip(string taskId, string reason, ScheduledTaskTrigger trigger, string? triggeredBy, bool persistAll)
    {
        var now = UtcNow();
        var history = History(taskId);
        ScheduledTaskRun entry;
        lock (history)
        {
            // A job that can't run here is skipped on every tick; fold repeats into one entry.
            var newest = history.Entries.Count > 0 ? history.Entries[0] : null;
            if (trigger == ScheduledTaskTrigger.Schedule
                && newest is { Outcome: ScheduledTaskRunOutcome.Skipped, Trigger: ScheduledTaskTrigger.Schedule }
                && string.Equals(newest.Message, reason, StringComparison.Ordinal))
            {
                entry = newest with
                {
                    Occurrences = newest.Occurrences + 1,
                    LastOccurrenceUtc = now,
                    FinishedUtc = now,
                };
                history.Entries[0] = entry;
            }
            else
            {
                entry = new ScheduledTaskRun(
                    Guid.NewGuid(), taskId, trigger, Truncate(triggeredBy, 255), _server,
                    now, now, ScheduledTaskRunOutcome.Skipped, Truncate(reason, MaxMessageLength), null, 1, now)
                {
                    Transient = !persistAll,
                };
                history.Add(entry, Limit());
            }
        }

        MarkDirty(entry);
    }

    public bool ReportProgress(string taskId, double? percent, string? message)
    {
        if (!_histories.TryGetValue(taskId, out var history)) return false;
        ScheduledTaskRun updated;
        lock (history)
        {
            var index = history.Entries.FindIndex(r => r.Outcome == ScheduledTaskRunOutcome.Running);
            if (index < 0) return false;

            var run = history.Entries[index];
            updated = run with
            {
                Progress = percent is { } value && double.IsFinite(value) ? Math.Clamp(value, 0, 100) : run.Progress,
                ProgressMessage = message is null ? run.ProgressMessage : Truncate(message, MaxProgressMessageLength),
            };
            history.Entries[index] = updated;
        }

        MarkDirty(updated);
        return true;
    }

    public void AppendLog(string taskId, Guid runId, string line)
    {
        if (!_histories.TryGetValue(taskId, out var history)) return;
        var maxLines = Math.Max(0, _options.CurrentValue.RunLogMaxLines);
        lock (history)
        {
            if (!history.Logs.TryGetValue(runId, out var buffer)) return; // finished, or trimmed
            buffer.Append(Truncate(line, MaxLogLineLength)!, maxLines);
        }

        if (TracksChanges) _dirty.TryAdd(runId, taskId);
    }

    public void MarkStopRequested(string taskId, Guid runId, string? requestedBy)
        => Update(taskId, runId, r => r.Outcome == ScheduledTaskRunOutcome.Running
            ? r with { StopRequestedBy = Truncate(requestedBy ?? "someone", 255) }
            : r);

    public void SetRetry(string taskId, Guid runId, DateTime? retryAtUtc)
    {
        var updated = Update(taskId, runId, r => r.Outcome == ScheduledTaskRunOutcome.Failed ? r with { RetryAtUtc = retryAtUtc } : r);
        if (updated is null && retryAtUtc is null && _store is not null)
        {
            // Not in this server's memory (it failed on another server, or before a restart): clear the saved one.
            TryStore(store => { store.ClearRetry(runId); }, "clear a pending retry");
        }

        // Other servers decide on it (a distributed retry may run anywhere): don't wait for the next batch.
        Flush();
        _latest = null;
    }

    public ScheduledTaskRun? GetPendingRetry(string taskId, bool fresh = false)
    {
        ScheduledTaskRun? local = null;
        _histories.TryGetValue(taskId, out var history);
        if (history is not null)
        {
            lock (history) local = history.Entries.FirstOrDefault(r => r.Outcome != ScheduledTaskRunOutcome.Skipped);
        }

        // A recurring task waits for its retry in-process: only this server's own runs say whether it
        // does here (each server runs its own instance of the job).
        if (ScheduledTaskIdentity.IsRecurring(taskId) || !StoreAvailable) return Pending(local);

        // For a distributed task the newest run on any server decides: a retry scheduled here may
        // have been run, superseded or cancelled on another server since.
        ScheduledTaskRun? saved;
        var readFresh = false;
        if (fresh)
        {
            try
            {
                saved = _store!.GetLatest(taskId);
                readFresh = true;
            }
            catch (Exception ex)
            {
                WarnStore(ex, "read the latest run");
                saved = null;
            }
        }
        else
        {
            saved = LatestSaved().GetValueOrDefault(taskId);
        }

        // A distributed retry is saved the moment it's scheduled, so the database is authoritative:
        // one it doesn't have (or has without a retry) was cancelled, run or cleared on another server.
        var dbAnswered = readFresh || _latest is not null;
        if (local is { Outcome: ScheduledTaskRunOutcome.Failed, RetryAtUtc: not null } pending
            && dbAnswered
            && (saved?.Id == pending.Id ? saved.RetryAtUtc is null : saved is null || saved.StartedUtc <= pending.StartedUtc))
        {
            ForgetRetry(history!, pending.Id);
            local = local with { RetryAtUtc = null };
        }

        var newest = saved is not null && (local is null || saved.StartedUtc > local.StartedUtc) ? saved : local;
        return Pending(newest);

        static ScheduledTaskRun? Pending(ScheduledTaskRun? run)
            => run is { Outcome: ScheduledTaskRunOutcome.Failed, RetryAtUtc: not null } ? run : null;
    }

    // Takes back a retry another server cleared, without writing it again.
    private static void ForgetRetry(TaskHistory history, Guid runId)
    {
        lock (history)
        {
            var index = history.Entries.FindIndex(r => r.Id == runId);
            if (index >= 0) history.Entries[index] = history.Entries[index] with { RetryAtUtc = null };
        }
    }

    public IReadOnlyList<ScheduledTaskRun> GetRuns(string taskId)
    {
        var limit = Limit();
        var local = LocalRuns(taskId);
        var saved = StoreAvailable ? TryStore(store => store.GetRuns(taskId, limit), "read the run history") : null;
        if (saved is null || saved.Count == 0) return local;

        // This server's own entries are the freshest (progress, log); the database adds the rest.
        var byId = saved.ToDictionary(r => r.Id);
        foreach (var run in local) byId[run.Id] = run;
        return byId.Values
            .OrderByDescending(r => r.StartedUtc)
            .ThenByDescending(r => r.LastOccurrenceUtc)
            .Take(limit)
            .ToArray();
    }

    public ScheduledTaskRunStats GetStats(string taskId)
    {
        ScheduledTaskRunStats stats = NoStats;
        if (_histories.TryGetValue(taskId, out var history))
        {
            lock (history)
            {
                stats = new ScheduledTaskRunStats(
                    history.Runs,
                    history.Failures,
                    WithLog(history, history.Entries.FirstOrDefault(r => r.Outcome == ScheduledTaskRunOutcome.Running)),
                    history.Entries.FirstOrDefault(r => r.Outcome != ScheduledTaskRunOutcome.Running));
            }
        }

        // After a restart, or for a distributed task another server ran: the latest saved run.
        if (LatestSaved().GetValueOrDefault(taskId) is { } saved && (stats.Last is null || saved.StartedUtc > stats.Last.StartedUtc))
            stats = stats with { Last = saved };

        return stats with { PendingRetry = GetPendingRetry(taskId) };
    }

    public void Clear(string taskId)
    {
        if (_histories.TryGetValue(taskId, out var history))
        {
            lock (history)
            {
                var finished = history.Entries.Where(r => r.Outcome != ScheduledTaskRunOutcome.Running).ToList();
                history.Entries.RemoveAll(r => r.Outcome != ScheduledTaskRunOutcome.Running);
                foreach (var run in finished) _dirty.TryRemove(run.Id, out _);
                history.Runs = history.Entries.Count;
                history.Failures = 0;
            }
        }

        if (StoreAvailable) TryStore(store => store.Delete(taskId), "clear the run history");
        _latest = null;
    }

    /// <summary>
    /// Writes the runs changed since the last flush to the database (called every
    /// <see cref="ScheduledTasksOptions.RunHistoryFlushInterval"/> and at shutdown), and trims
    /// each task's saved history to <see cref="ScheduledTasksOptions.RunHistoryLimit"/>.
    /// </summary>
    internal void Flush()
    {
        if (!StoreAvailable)
        {
            // The table isn't there (yet): changes wait for it, but not without end.
            if (_store is not null && _dirty.Count > MaxPendingChanges)
            {
                _dirty.Clear();
                _logger.LogWarning("Scheduled Tasks dropped run history waiting to be saved: its table still isn't available");
            }

            return;
        }

        lock (_flushLock)
        {
            var batch = new List<ScheduledTaskRun>();
            foreach (var (runId, taskId) in _dirty.ToArray())
            {
                _dirty.TryRemove(runId, out _);
                if (Snapshot(taskId, runId) is { } run && (ShouldPersist(run) || _written.ContainsKey(run.Id))) batch.Add(run);
            }

            // A frequent task's run that is taking a while is saved too, so another server can see and stop it.
            var longSince = UtcNow() - LongRunning;
            foreach (var history in _histories.Values)
            {
                lock (history)
                {
                    batch.AddRange(history.Entries
                        .Where(r => r is { Transient: true, Outcome: ScheduledTaskRunOutcome.Running } && r.StartedUtc < longSince
                            && !_written.ContainsKey(r.Id) && batch.All(b => b.Id != r.Id))
                        .Select(r => WithLog(history, r)!));
                }
            }

            if (batch.Count == 0) return;

            try
            {
                _store!.Save(batch);
            }
            catch (Exception ex)
            {
                // Try again with the next batch (a later change of the run re-marks it anyway).
                foreach (var run in batch) _dirty.TryAdd(run.Id, run.TaskId);
                WarnStore(ex, "save the run history");
                return;
            }

            // A frequent task's run saved while running must have its end saved too.
            foreach (var run in batch)
            {
                if (run.Transient && run.Outcome == ScheduledTaskRunOutcome.Running) _written[run.Id] = 0;
                else _written.TryRemove(run.Id, out _);
            }

            var limit = Limit();
            foreach (var taskId in batch.Select(r => r.TaskId).Distinct(StringComparer.Ordinal))
            {
                var now = _timeProvider.GetTimestamp();
                if (_prunedAt.TryGetValue(taskId, out var last) && _timeProvider.GetElapsedTime(last) < PruneInterval) continue;
                _prunedAt[taskId] = now;
                TryStore(store => store.Prune(taskId, limit), "trim the run history");
            }
        }
    }

    /// <summary>Whether a run goes to the database: always, unless it's a frequent task's routine run.</summary>
    internal static bool ShouldPersist(ScheduledTaskRun run)
        => !run.Transient
           || run.Outcome is ScheduledTaskRunOutcome.Failed or ScheduledTaskRunOutcome.Canceled
           || run.RetryAtUtc is not null
           || run.Attempt > 1
           || run.StopRequestedBy is not null;

    /// <summary>The run id of this server's run in progress for the task, if any.</summary>
    internal Guid? CurrentRunId(string taskId)
    {
        if (!_histories.TryGetValue(taskId, out var history)) return null;
        lock (history) return history.Entries.FirstOrDefault(r => r.Outcome == ScheduledTaskRunOutcome.Running)?.Id;
    }

    // Changes are only queued for the database when they will be written (before the migration has run they wait for it).
    private bool TracksChanges => _store is not null && _options.CurrentValue.PersistRunHistory;

    private bool StoreAvailable => _store is { IsAvailable: true } && _options.CurrentValue.PersistRunHistory;

    private IReadOnlyList<ScheduledTaskRun> LocalRuns(string taskId)
    {
        if (!_histories.TryGetValue(taskId, out var history)) return [];
        lock (history) return history.Entries.Select(r => WithLog(history, r)!).ToArray();
    }

    private ScheduledTaskRun? Snapshot(string taskId, Guid runId)
    {
        if (!_histories.TryGetValue(taskId, out var history)) return null;
        lock (history) return WithLog(history, history.Entries.FirstOrDefault(r => r.Id == runId));
    }

    // A running run's log lives in its buffer until it finishes.
    private static ScheduledTaskRun? WithLog(TaskHistory history, ScheduledTaskRun? run)
        => run is not null && history.Logs.TryGetValue(run.Id, out var buffer) ? run with { Log = buffer.ToText() } : run;

    private IReadOnlyDictionary<string, ScheduledTaskRun> LatestSaved()
    {
        if (!StoreAvailable) return LatestRuns.Empty.Runs;

        var cached = _latest;
        if (cached is not null && _timeProvider.GetElapsedTime(cached.LoadedAt) < LatestCacheDuration) return cached.Runs;

        var loaded = TryStore(store => store.GetLatestPerTask(), "read the latest runs");
        // On failure keep serving the previous map until the next attempt; a map is only ever a
        // successful read, so a task missing from it genuinely has no saved runs.
        if (loaded is not null) _latest = new LatestRuns(loaded, _timeProvider.GetTimestamp());
        else if (cached is not null) _latest = cached with { LoadedAt = _timeProvider.GetTimestamp() };
        return _latest?.Runs ?? LatestRuns.Empty.Runs;
    }

    private ScheduledTaskRun? Update(string taskId, Guid runId, Func<ScheduledTaskRun, ScheduledTaskRun> change)
    {
        if (!_histories.TryGetValue(taskId, out var history)) return null;
        ScheduledTaskRun updated;
        lock (history)
        {
            var index = history.Entries.FindIndex(r => r.Id == runId);
            if (index < 0) return null;
            updated = change(history.Entries[index]);
            history.Entries[index] = updated;
        }

        MarkDirty(updated);
        return updated;
    }

    private void Finish(string taskId, Guid runId, ScheduledTaskRunOutcome outcome, string? message, string? details)
    {
        var now = UtcNow();
        var history = History(taskId);
        ScheduledTaskRun finished;
        lock (history)
        {
            var index = history.Entries.FindIndex(r => r.Id == runId);
            // Already finished (e.g. a notification handler threw after the job succeeded): keep the first outcome.
            if (index >= 0 && history.Entries[index].Outcome != ScheduledTaskRunOutcome.Running) return;

            if (outcome == ScheduledTaskRunOutcome.Failed) history.Failures++;
            history.Logs.Remove(runId, out var buffer);
            if (index < 0) return; // Trimmed out of a very small history while running.

            finished = history.Entries[index] with
            {
                Outcome = outcome,
                FinishedUtc = now,
                LastOccurrenceUtc = now,
                Message = Truncate(message, MaxMessageLength),
                Details = details,
                Log = buffer?.ToText(),
            };
            history.Entries[index] = finished;
        }

        MarkDirty(finished);
    }

    private void ClearPendingRetries(TaskHistory history)
    {
        for (var i = 0; i < history.Entries.Count; i++)
        {
            if (history.Entries[i] is { Outcome: ScheduledTaskRunOutcome.Failed, RetryAtUtc: not null } pending)
            {
                history.Entries[i] = pending with { RetryAtUtc = null };
                if (TracksChanges) _dirty.TryAdd(pending.Id, pending.TaskId);
            }
        }
    }

    private void MarkDirty(ScheduledTaskRun run)
    {
        if (TracksChanges) _dirty[run.Id] = run.TaskId;
    }

    private T? TryStore<T>(Func<IScheduledTaskRunStore, T?> action, string what) where T : class
    {
        if (_store is null) return null;
        try
        {
            return action(_store);
        }
        catch (Exception ex)
        {
            WarnStore(ex, what);
            return null;
        }
    }

    private void TryStore(Action<IScheduledTaskRunStore> action, string what)
        => TryStore<object>(store => { action(store); return store; }, what);

    // One warning a minute, not one per dashboard poll while the database is unreachable.
    private void WarnStore(Exception ex, string what)
    {
        var now = _timeProvider.GetTimestamp();
        var last = Interlocked.Read(ref _storeWarnedAt);
        if (last != 0 && _timeProvider.GetElapsedTime(last) < TimeSpan.FromMinutes(1)) return;
        Interlocked.Exchange(ref _storeWarnedAt, now);
        _logger.LogWarning(ex, "Scheduled Tasks could not {What}; the history on this server is still kept in memory.", what);
    }

    private TaskHistory History(string taskId) => _histories.GetOrAdd(taskId, _ => new TaskHistory());

    private int Limit() => Math.Max(1, _options.CurrentValue.RunHistoryLimit);

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private static string? Truncate(string? value, int maxLength)
        => value is null || value.Length <= maxLength ? value : value[..maxLength] + "…";

    private sealed record LatestRuns(IReadOnlyDictionary<string, ScheduledTaskRun> Runs, long LoadedAt)
    {
        public static readonly LatestRuns Empty = new(new Dictionary<string, ScheduledTaskRun>(StringComparer.Ordinal), 0);
    }

    private sealed class TaskHistory
    {
        public List<ScheduledTaskRun> Entries { get; } = [];

        /// <summary>The log of each run in progress, until it finishes.</summary>
        public Dictionary<Guid, LogBuffer> Logs { get; } = [];

        public int Runs { get; set; }

        public int Failures { get; set; }

        public void Add(ScheduledTaskRun run, int limit)
        {
            Entries.Insert(0, run);
            if (Entries.Count <= limit) return;

            // Never drop a run in progress (its log and outcome are still to come).
            for (var i = Entries.Count - 1; i >= limit && Entries.Count > limit; i--)
            {
                if (Entries[i].Outcome == ScheduledTaskRunOutcome.Running) continue;
                Entries.RemoveAt(i);
            }
        }
    }

    private sealed class LogBuffer
    {
        private readonly StringBuilder _text = new();
        private int _lines;
        private int _dropped;

        public void Append(string line, int maxLines)
        {
            if (_lines >= maxLines)
            {
                _dropped++;
                return;
            }

            if (_text.Length > 0) _text.Append('\n');
            _text.Append(line.Replace('\n', ' ').Replace("\r", string.Empty));
            _lines++;
        }

        public string? ToText()
        {
            if (_lines == 0 && _dropped == 0) return null;
            return _dropped == 0 ? _text.ToString() : $"{_text}\n… {_dropped} more line{(_dropped == 1 ? "" : "s")} not kept";
        }
    }
}
