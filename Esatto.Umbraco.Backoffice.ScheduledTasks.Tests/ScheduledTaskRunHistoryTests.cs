using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

/// <summary>The run log with the database behind it, attempts, retries and the captured log.</summary>
public class ScheduledTaskRunHistoryTests
{
    private const string TaskId = "recurring:My.Job";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 18, 8, 0, 0, TimeSpan.Zero));
    private readonly InMemoryRunStore _store = new();
    private readonly ScheduledTasksOptions _options = new() { RunHistoryLimit = 25 };

    private ScheduledTaskRunLog CreateLog(bool withStore = true)
        => new(_time, TestSupport.Options(_options), withStore ? _store : null);

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private static ScheduledTaskRun SavedRun(
        string taskId,
        DateTime startedUtc,
        ScheduledTaskRunOutcome outcome = ScheduledTaskRunOutcome.Succeeded,
        string server = "OTHER-SERVER")
        => new(Guid.NewGuid(), taskId, ScheduledTaskTrigger.Schedule, null, server, startedUtc,
            outcome == ScheduledTaskRunOutcome.Running ? null : startedUtc.AddSeconds(1), outcome, null, null, 1, startedUtc);

    [Fact]
    public void A_run_records_its_attempt_number_and_how_many_are_allowed()
    {
        var log = CreateLog();

        log.Start(TaskId, ScheduledTaskTrigger.Retry, null, attempt: 2, maxAttempts: 4, persistAll: true);

        var run = Assert.Single(log.GetRuns(TaskId));
        Assert.Equal(2, run.Attempt);
        Assert.Equal(4, run.MaxAttempts);
        Assert.Equal(ScheduledTaskTrigger.Retry, run.Trigger);
    }

    [Fact]
    public void A_flush_saves_the_changed_runs_in_one_batch_and_only_once()
    {
        var log = CreateLog();
        var first = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null);
        log.Succeed(TaskId, first);
        log.Start(TaskId, ScheduledTaskTrigger.Manual, "Carl");

        log.Flush();
        log.Flush();

        Assert.Equal(1, _store.SaveCalls);
        Assert.Equal(2, _store.Runs.Count);
        Assert.Equal(ScheduledTaskRunOutcome.Succeeded, _store.Runs[first].Outcome);
    }

    [Fact]
    public void Runs_from_before_the_table_exists_are_saved_once_it_does()
    {
        _store.IsAvailable = false;
        var log = CreateLog();
        var runId = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null);
        log.Succeed(TaskId, runId);

        log.Flush();
        Assert.Empty(_store.Runs);

        _store.IsAvailable = true;
        log.Flush();
        Assert.Contains(runId, _store.Runs.Keys);
    }

    [Fact]
    public void Nothing_is_saved_when_run_history_persistence_is_off()
    {
        _options.PersistRunHistory = false;
        var log = CreateLog();
        log.Succeed(TaskId, log.Start(TaskId, ScheduledTaskTrigger.Schedule, null));

        log.Flush();

        Assert.Equal(0, _store.SaveCalls);
    }

    [Fact]
    public void With_persistence_off_changed_runs_are_not_kept_waiting_for_a_flush()
    {
        _options.PersistRunHistory = false;
        var log = CreateLog();
        log.Succeed(TaskId, log.Start(TaskId, ScheduledTaskTrigger.Schedule, null));
        log.Flush();

        _options.PersistRunHistory = true;
        var later = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null);
        log.Flush();

        Assert.Equal([later], _store.Runs.Keys);
    }

    [Fact]
    public void A_frequent_tasks_routine_run_stays_in_memory_but_a_failure_is_saved()
    {
        var log = CreateLog();
        var routine = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null, 1, 1, persistAll: false);
        log.Succeed(TaskId, routine);
        var failed = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null, 1, 1, persistAll: false);
        log.Fail(TaskId, failed, new Exception("boom"));

        log.Flush();

        Assert.Equal([failed], _store.Runs.Keys);
        Assert.Equal(2, log.GetRuns(TaskId).Count);
    }

    [Fact]
    public void Only_notable_runs_of_frequent_tasks_are_persisted()
    {
        var routine = new ScheduledTaskRun(
            Guid.NewGuid(), TaskId, ScheduledTaskTrigger.Schedule, null, "WEB1", Now, Now, ScheduledTaskRunOutcome.Succeeded, null, null, 1, Now)
        {
            Transient = true,
        };

        Assert.False(ScheduledTaskRunLog.ShouldPersist(routine));
        Assert.True(ScheduledTaskRunLog.ShouldPersist(routine with { Transient = false }));
        Assert.True(ScheduledTaskRunLog.ShouldPersist(routine with { Outcome = ScheduledTaskRunOutcome.Failed }));
        Assert.True(ScheduledTaskRunLog.ShouldPersist(routine with { Outcome = ScheduledTaskRunOutcome.Canceled }));
        Assert.True(ScheduledTaskRunLog.ShouldPersist(routine with { RetryAtUtc = Now }));
        Assert.True(ScheduledTaskRunLog.ShouldPersist(routine with { Attempt = 2 }));
        Assert.True(ScheduledTaskRunLog.ShouldPersist(routine with { StopRequestedBy = "Carl" }));
    }

    [Fact]
    public void Runs_a_failed_save_could_not_write_are_written_with_the_next_batch()
    {
        var log = CreateLog();
        var runId = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null);
        _store.FailSaves = true;

        log.Flush();
        Assert.Empty(_store.Runs);

        _store.FailSaves = false;
        log.Flush();
        Assert.Contains(runId, _store.Runs.Keys);
    }

    [Fact]
    public void A_flush_trims_the_saved_history_at_most_once_a_minute_per_task()
    {
        _options.RunHistoryLimit = 3;
        var log = CreateLog();

        log.Start(TaskId, ScheduledTaskTrigger.Schedule, null);
        log.Flush();
        log.Start(TaskId, ScheduledTaskTrigger.Manual, null);
        log.Flush();
        _time.Advance(TimeSpan.FromMinutes(1));
        log.Start(TaskId, ScheduledTaskTrigger.Manual, null);
        log.Flush();

        Assert.Equal([(TaskId, 3), (TaskId, 3)], _store.Prunes);
    }

    [Fact]
    public void The_history_merges_other_servers_saved_runs_with_this_servers_newest_first()
    {
        var log = CreateLog();
        var older = SavedRun(TaskId, Now.AddHours(-2));
        _store.Runs[older.Id] = older;
        var local = log.Start(TaskId, ScheduledTaskTrigger.Manual, "Carl");
        _time.Advance(TimeSpan.FromSeconds(5));
        log.AppendLog(TaskId, local, "working");

        var runs = log.GetRuns(TaskId);

        Assert.Equal([local, older.Id], runs.Select(r => r.Id));
        Assert.Equal("working", runs[0].Log);
    }

    [Fact]
    public void This_servers_copy_of_a_saved_run_wins_because_it_is_fresher()
    {
        var log = CreateLog();
        var runId = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null);
        log.Flush();
        log.ReportProgress(TaskId, 40, "Halfway there");

        var run = Assert.Single(log.GetRuns(TaskId));

        Assert.Equal(runId, run.Id);
        Assert.Equal(40, run.Progress);
    }

    [Fact]
    public void When_the_database_cannot_be_read_the_history_in_memory_is_shown()
    {
        var log = CreateLog();
        var runId = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null);
        _store.FailReads = true;

        Assert.Equal(runId, Assert.Single(log.GetRuns(TaskId)).Id);
    }

    [Fact]
    public void After_a_restart_the_last_run_comes_from_the_database()
    {
        var saved = SavedRun(TaskId, Now.AddHours(-1), ScheduledTaskRunOutcome.Failed);
        _store.Runs[saved.Id] = saved;

        var stats = CreateLog().GetStats(TaskId);

        Assert.Equal(saved.Id, stats.Last?.Id);
        Assert.Null(stats.Current);
    }

    [Fact]
    public void A_failed_run_with_a_retry_time_is_the_pending_retry()
    {
        var log = CreateLog(withStore: false);
        var runId = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null, 1, 3, persistAll: true);
        log.Fail(TaskId, runId, new Exception("boom"));

        log.SetRetry(TaskId, runId, Now.AddMinutes(1));

        var pending = log.GetPendingRetry(TaskId);
        Assert.Equal(runId, pending?.Id);
        Assert.Equal(Now.AddMinutes(1), pending?.RetryAtUtc);
        Assert.Equal(runId, log.GetStats(TaskId).PendingRetry?.Id);
    }

    [Fact]
    public void A_retry_is_saved_at_once_so_every_server_knows()
    {
        var log = CreateLog();
        var runId = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null, 1, 2, persistAll: true);
        log.Fail(TaskId, runId, new Exception("boom"));

        log.SetRetry(TaskId, runId, Now.AddMinutes(1));

        Assert.Equal(Now.AddMinutes(1), _store.Runs[runId].RetryAtUtc);
    }

    [Fact]
    public void Clearing_a_retry_of_a_run_this_server_does_not_have_clears_the_saved_one()
    {
        var saved = SavedRun(TaskId, Now.AddMinutes(-1), ScheduledTaskRunOutcome.Failed) with { RetryAtUtc = Now.AddMinutes(1) };
        _store.Runs[saved.Id] = saved;
        var log = CreateLog();

        log.SetRetry(TaskId, saved.Id, null);

        Assert.Null(_store.Runs[saved.Id].RetryAtUtc);
    }

    // A distributed retry can be picked up by any server, so the store is consulted; a recurring
    // task waits for its retry in-process, so only this server's own runs count (below).
    private const string DistributedTaskId = "distributed:MyJob";

    [Fact]
    public void A_newer_run_on_another_server_means_the_retry_is_no_longer_pending()
    {
        var log = CreateLog();
        var runId = log.Start(DistributedTaskId, ScheduledTaskTrigger.Schedule, null, 1, 2, persistAll: true);
        log.Fail(DistributedTaskId, runId, new Exception("boom"));
        log.SetRetry(DistributedTaskId, runId, Now.AddMinutes(1));
        _time.Advance(TimeSpan.FromMinutes(1));

        // The retry ran (and succeeded) elsewhere.
        var retried = SavedRun(DistributedTaskId, Now) with { Attempt = 2 };
        _store.Runs[retried.Id] = retried;

        Assert.Null(log.GetPendingRetry(DistributedTaskId, fresh: true));
    }

    [Fact]
    public void A_pending_retry_saved_by_another_server_is_found_with_a_fresh_read()
    {
        var saved = SavedRun(DistributedTaskId, Now.AddMinutes(-1), ScheduledTaskRunOutcome.Failed) with { RetryAtUtc = Now.AddMinutes(1) };
        _store.Runs[saved.Id] = saved;

        Assert.Equal(saved.Id, CreateLog().GetPendingRetry(DistributedTaskId, fresh: true)?.Id);
    }

    [Fact]
    public void A_recurring_tasks_retry_waits_in_process_so_other_servers_runs_dont_count()
    {
        // Another server's saved pending retry: it waits there, not here.
        var saved = SavedRun(TaskId, Now.AddMinutes(-1), ScheduledTaskRunOutcome.Failed) with { RetryAtUtc = Now.AddMinutes(1) };
        _store.Runs[saved.Id] = saved;
        Assert.Null(CreateLog().GetPendingRetry(TaskId, fresh: true));

        // This server's own pending retry still counts, store or no store.
        var log = CreateLog();
        var runId = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null, 1, 2, persistAll: true);
        log.Fail(TaskId, runId, new Exception("boom"));
        log.SetRetry(TaskId, runId, Now.AddMinutes(1));
        Assert.Equal(runId, log.GetPendingRetry(TaskId)?.Id);
    }

    [Fact]
    public void A_retry_cancelled_or_cleared_on_another_server_is_dropped_here_too()
    {
        var log = CreateLog();
        var runId = log.Start(DistributedTaskId, ScheduledTaskTrigger.Schedule, null, 1, 2, persistAll: true);
        log.Fail(DistributedTaskId, runId, new Exception("boom"));
        log.SetRetry(DistributedTaskId, runId, Now.AddMinutes(1));

        // Cancelled there: the saved run has no retry any more.
        _store.Runs[runId] = _store.Runs[runId] with { RetryAtUtc = null };
        Assert.Null(log.GetPendingRetry(DistributedTaskId, fresh: true));
        // And it stays dropped on cached reads afterwards.
        Assert.Null(log.GetPendingRetry(DistributedTaskId));

        var again = log.Start(DistributedTaskId, ScheduledTaskTrigger.Schedule, null, 1, 2, persistAll: true);
        log.Fail(DistributedTaskId, again, new Exception("boom"));
        log.SetRetry(DistributedTaskId, again, Now.AddMinutes(1));

        // History cleared there: the saved runs are gone altogether.
        _store.Runs.Clear();
        Assert.Null(log.GetPendingRetry(DistributedTaskId, fresh: true));
    }

    [Fact]
    public void A_new_run_supersedes_a_pending_retry()
    {
        var log = CreateLog(withStore: false);
        var failed = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null, 1, 2, persistAll: true);
        log.Fail(TaskId, failed, new Exception("boom"));
        log.SetRetry(TaskId, failed, Now.AddMinutes(1));

        log.Start(TaskId, ScheduledTaskTrigger.Manual, "Carl");

        Assert.Null(log.GetRuns(TaskId).Single(r => r.Id == failed).RetryAtUtc);
        Assert.Null(log.GetPendingRetry(TaskId));
    }

    [Fact]
    public void A_runs_log_is_shown_while_it_runs_and_kept_when_it_finishes()
    {
        var log = CreateLog(withStore: false);
        var runId = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null);

        log.AppendLog(TaskId, runId, "first");
        log.AppendLog(TaskId, runId, "second");
        Assert.Equal("first\nsecond", log.GetStats(TaskId).Current?.Log);

        log.Succeed(TaskId, runId);
        Assert.Equal("first\nsecond", Assert.Single(log.GetRuns(TaskId)).Log);
    }

    [Theory]
    [InlineData(3, "a\nb\n… 1 more line not kept")]
    [InlineData(5, "a\nb\n… 3 more lines not kept")]
    [InlineData(2, "a\nb")]
    public void Lines_past_the_limit_are_counted_but_not_kept(int lines, string expected)
    {
        _options.RunLogMaxLines = 2;
        var log = CreateLog(withStore: false);
        var runId = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null);

        foreach (var line in "abcde"[..lines]) log.AppendLog(TaskId, runId, line.ToString());
        log.Succeed(TaskId, runId);

        Assert.Equal(expected, Assert.Single(log.GetRuns(TaskId)).Log);
    }

    [Fact]
    public void A_log_line_marks_the_run_for_the_next_flush()
    {
        var log = CreateLog();
        var runId = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null);
        log.Flush();

        log.AppendLog(TaskId, runId, "progress");
        log.Flush();

        Assert.Equal("progress", _store.Runs[runId].Log);
    }

    [Fact]
    public void A_stop_request_is_recorded_on_a_running_run_only()
    {
        var log = CreateLog(withStore: false);
        var finished = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null);
        log.Succeed(TaskId, finished);
        var running = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null);

        log.MarkStopRequested(TaskId, finished, "Carl");
        log.MarkStopRequested(TaskId, running, null);

        var runs = log.GetRuns(TaskId);
        Assert.Null(runs.Single(r => r.Id == finished).StopRequestedBy);
        Assert.Equal("someone", runs.Single(r => r.Id == running).StopRequestedBy);
    }

    [Fact]
    public void A_run_in_progress_is_never_trimmed_from_the_history()
    {
        _options.RunHistoryLimit = 2;
        var log = CreateLog(withStore: false);
        var running = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null);

        for (var i = 0; i < 3; i++)
        {
            _time.Advance(TimeSpan.FromMinutes(1));
            log.Skip(TaskId, $"reason {i}", ScheduledTaskTrigger.Manual, null);
        }

        var runs = log.GetRuns(TaskId);
        Assert.Contains(runs, r => r.Id == running);
        Assert.True(log.ReportProgress(TaskId, 50, null));
    }

    [Fact]
    public void Clearing_the_history_also_deletes_the_saved_runs()
    {
        var saved = SavedRun(TaskId, Now.AddHours(-1));
        _store.Runs[saved.Id] = saved;
        var log = CreateLog();

        log.Clear(TaskId);

        Assert.Empty(log.GetRuns(TaskId));
        Assert.Equal([TaskId], _store.Deleted);
    }
}

/// <summary>The <c>ScheduledTaskRuns</c> table in memory, as the SQL store behaves.</summary>
internal sealed class InMemoryRunStore : IScheduledTaskRunStore
{
    public Dictionary<Guid, ScheduledTaskRun> Runs { get; } = [];

    public bool IsAvailable { get; set; } = true;

    public bool FailSaves { get; set; }

    public bool FailReads { get; set; }

    public int SaveCalls { get; private set; }

    public List<(string TaskId, int Keep)> Prunes { get; } = [];

    public List<string> Deleted { get; } = [];

    public List<(string Server, DateTime StartedBeforeUtc)> Interrupted { get; } = [];

    public List<DateTime> PrunedOlderThan { get; } = [];

    public void Save(IReadOnlyCollection<ScheduledTaskRun> runs)
    {
        if (FailSaves) throw new InvalidOperationException("database unreachable");
        SaveCalls++;
        foreach (var run in runs)
        {
            // Never clears a stop request made from another server.
            var stopRequestedBy = run.StopRequestedBy ?? Runs.GetValueOrDefault(run.Id)?.StopRequestedBy;
            Runs[run.Id] = run with { StopRequestedBy = stopRequestedBy };
        }
    }

    public IReadOnlyList<ScheduledTaskRun> GetRuns(string taskId, int limit)
    {
        if (FailReads) throw new InvalidOperationException("database unreachable");
        return Runs.Values.Where(r => r.TaskId == taskId).OrderByDescending(r => r.StartedUtc).Take(limit).ToList();
    }

    public ScheduledTaskRun? GetLatest(string taskId)
        => Runs.Values
            .Where(r => r.TaskId == taskId && r.Outcome is not (ScheduledTaskRunOutcome.Running or ScheduledTaskRunOutcome.Skipped))
            .OrderByDescending(r => r.StartedUtc)
            .FirstOrDefault();

    public IReadOnlyDictionary<string, ScheduledTaskRun> GetLatestPerTask()
    {
        if (FailReads) throw new InvalidOperationException("database unreachable");
        return Runs.Values.Select(r => r.TaskId).Distinct().ToDictionary(id => id, id => GetLatest(id)!)
            .Where(p => p.Value is not null)
            .ToDictionary(p => p.Key, p => p.Value);
    }

    public IReadOnlyList<ScheduledTaskRun> GetRunning(string taskId)
        => Runs.Values.Where(r => r.TaskId == taskId && r.Outcome == ScheduledTaskRunOutcome.Running).ToList();

    public bool RequestStop(Guid runId, string requestedBy)
    {
        if (!Runs.TryGetValue(runId, out var run) || run.Outcome != ScheduledTaskRunOutcome.Running) return false;
        Runs[runId] = run with { StopRequestedBy = requestedBy };
        return true;
    }

    public IReadOnlyDictionary<Guid, string> GetStopRequests(string server)
        => Runs.Values
            .Where(r => r.Server == server && r.Outcome == ScheduledTaskRunOutcome.Running && r.StopRequestedBy is not null)
            .ToDictionary(r => r.Id, r => r.StopRequestedBy!);

    public bool ClearRetry(Guid runId)
    {
        if (!Runs.TryGetValue(runId, out var run)) return false;
        Runs[runId] = run with { RetryAtUtc = null };
        return true;
    }

    public void Delete(string taskId)
    {
        Deleted.Add(taskId);
        foreach (var run in Runs.Values.Where(r => r.TaskId == taskId && r.Outcome != ScheduledTaskRunOutcome.Running).ToList())
            Runs.Remove(run.Id);
    }

    public void Prune(string taskId, int keep) => Prunes.Add((taskId, keep));

    public void PruneOlderThan(DateTime cutoffUtc) => PrunedOlderThan.Add(cutoffUtc);

    public int MarkInterrupted(string server, DateTime startedBeforeUtc)
    {
        Interrupted.Add((server, startedBeforeUtc));
        return 0;
    }
}
