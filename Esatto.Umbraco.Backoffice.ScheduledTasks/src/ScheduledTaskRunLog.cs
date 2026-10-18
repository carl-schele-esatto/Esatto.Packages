using System.Collections.Concurrent;
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
}

/// <summary>Counters since startup plus the current and most recent finished run.</summary>
public sealed record ScheduledTaskRunStats(int Runs, int Failures, ScheduledTaskRun? Current, ScheduledTaskRun? Last);

/// <summary>
/// Records task runs on this server. History lives in memory (it resets on restart) and is
/// capped per task by <see cref="ScheduledTasksOptions.RunHistoryLimit"/>; a database log would
/// add a write every few seconds for Umbraco's own high-frequency jobs.
/// </summary>
public interface IScheduledTaskRunLog
{
    Guid Start(string taskId, ScheduledTaskTrigger trigger, string? triggeredBy);

    void Succeed(string taskId, Guid runId);

    void Fail(string taskId, Guid runId, Exception exception);

    void Cancel(string taskId, Guid runId);

    void Skip(string taskId, string reason, ScheduledTaskTrigger trigger, string? triggeredBy);

    /// <summary>
    /// Sets the progress of the task's run in progress on this server. A null percent keeps the
    /// previous one (message only). Returns false when no run of the task is in progress here.
    /// </summary>
    bool ReportProgress(string taskId, double? percent, string? message);

    /// <summary>Runs for a task, newest first.</summary>
    IReadOnlyList<ScheduledTaskRun> GetRuns(string taskId);

    ScheduledTaskRunStats GetStats(string taskId);

    /// <summary>Forgets a task's finished runs and resets its counters. A run in progress is kept.</summary>
    void Clear(string taskId);
}

internal sealed class ScheduledTaskRunLog : IScheduledTaskRunLog
{
    private const int MaxDetailsLength = 8000;
    private const int MaxProgressMessageLength = 500;

    private static readonly ScheduledTaskRunStats NoStats = new(0, 0, null, null);

    private readonly ConcurrentDictionary<string, TaskHistory> _histories = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly IOptionsMonitor<ScheduledTasksOptions> _options;
    private readonly string _server = Environment.MachineName;

    public ScheduledTaskRunLog(TimeProvider timeProvider, IOptionsMonitor<ScheduledTasksOptions> options)
    {
        _timeProvider = timeProvider;
        _options = options;
    }

    public Guid Start(string taskId, ScheduledTaskTrigger trigger, string? triggeredBy)
    {
        var now = UtcNow();
        var run = new ScheduledTaskRun(
            Guid.NewGuid(), taskId, trigger, triggeredBy, _server,
            now, null, ScheduledTaskRunOutcome.Running, null, null, 1, now);

        var history = History(taskId);
        lock (history)
        {
            history.Runs++;
            history.Add(run, Limit());
        }

        return run.Id;
    }

    public void Succeed(string taskId, Guid runId)
        => Finish(taskId, runId, ScheduledTaskRunOutcome.Succeeded, null, null);

    public void Fail(string taskId, Guid runId, Exception exception)
        => Finish(
            taskId,
            runId,
            ScheduledTaskRunOutcome.Failed,
            $"{exception.GetType().Name}: {exception.Message}",
            Truncate(exception.ToString(), MaxDetailsLength));

    public void Cancel(string taskId, Guid runId)
        => Finish(taskId, runId, ScheduledTaskRunOutcome.Canceled, "Canceled because the application is shutting down.", null);

    public void Skip(string taskId, string reason, ScheduledTaskTrigger trigger, string? triggeredBy)
    {
        var now = UtcNow();
        var history = History(taskId);
        lock (history)
        {
            // A job that can't run here is skipped on every tick; fold repeats into one entry.
            var newest = history.Entries.Count > 0 ? history.Entries[0] : null;
            if (trigger == ScheduledTaskTrigger.Schedule
                && newest is { Outcome: ScheduledTaskRunOutcome.Skipped, Trigger: ScheduledTaskTrigger.Schedule }
                && string.Equals(newest.Message, reason, StringComparison.Ordinal))
            {
                history.Entries[0] = newest with
                {
                    Occurrences = newest.Occurrences + 1,
                    LastOccurrenceUtc = now,
                    FinishedUtc = now,
                };
                return;
            }

            history.Add(
                new ScheduledTaskRun(
                    Guid.NewGuid(), taskId, trigger, triggeredBy, _server,
                    now, now, ScheduledTaskRunOutcome.Skipped, reason, null, 1, now),
                Limit());
        }
    }

    public bool ReportProgress(string taskId, double? percent, string? message)
    {
        if (!_histories.TryGetValue(taskId, out var history)) return false;
        lock (history)
        {
            var index = history.Entries.FindIndex(r => r.Outcome == ScheduledTaskRunOutcome.Running);
            if (index < 0) return false;

            var run = history.Entries[index];
            history.Entries[index] = run with
            {
                Progress = percent is { } value && double.IsFinite(value) ? Math.Clamp(value, 0, 100) : run.Progress,
                ProgressMessage = message is null ? run.ProgressMessage : Truncate(message, MaxProgressMessageLength),
            };
            return true;
        }
    }

    public IReadOnlyList<ScheduledTaskRun> GetRuns(string taskId)
    {
        if (!_histories.TryGetValue(taskId, out var history)) return [];
        lock (history) return history.Entries.ToArray();
    }

    public ScheduledTaskRunStats GetStats(string taskId)
    {
        if (!_histories.TryGetValue(taskId, out var history)) return NoStats;
        lock (history)
        {
            return new ScheduledTaskRunStats(
                history.Runs,
                history.Failures,
                history.Entries.FirstOrDefault(r => r.Outcome == ScheduledTaskRunOutcome.Running),
                history.Entries.FirstOrDefault(r => r.Outcome != ScheduledTaskRunOutcome.Running));
        }
    }

    public void Clear(string taskId)
    {
        if (!_histories.TryGetValue(taskId, out var history)) return;
        lock (history)
        {
            history.Entries.RemoveAll(r => r.Outcome != ScheduledTaskRunOutcome.Running);
            history.Runs = history.Entries.Count;
            history.Failures = 0;
        }
    }

    private void Finish(string taskId, Guid runId, ScheduledTaskRunOutcome outcome, string? message, string? details)
    {
        var now = UtcNow();
        var history = History(taskId);
        lock (history)
        {
            var index = history.Entries.FindIndex(r => r.Id == runId);
            // Already finished (e.g. a notification handler threw after the job succeeded): keep the first outcome.
            if (index >= 0 && history.Entries[index].Outcome != ScheduledTaskRunOutcome.Running) return;

            if (outcome == ScheduledTaskRunOutcome.Failed) history.Failures++;
            if (index < 0) return; // Trimmed out of a very small history while running.

            history.Entries[index] = history.Entries[index] with
            {
                Outcome = outcome,
                FinishedUtc = now,
                LastOccurrenceUtc = now,
                Message = message,
                Details = details,
            };
        }
    }

    private TaskHistory History(string taskId) => _histories.GetOrAdd(taskId, _ => new TaskHistory());

    private int Limit() => Math.Max(1, _options.CurrentValue.RunHistoryLimit);

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "…";

    private sealed class TaskHistory
    {
        public List<ScheduledTaskRun> Entries { get; } = [];

        public int Runs { get; set; }

        public int Failures { get; set; }

        public void Add(ScheduledTaskRun run, int limit)
        {
            Entries.Insert(0, run);
            if (Entries.Count > limit) Entries.RemoveRange(limit, Entries.Count - limit);
        }
    }
}
