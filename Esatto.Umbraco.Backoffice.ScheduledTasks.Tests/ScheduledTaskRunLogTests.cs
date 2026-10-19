using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

public class ScheduledTaskRunLogTests
{
    private const string TaskId = "recurring:My.Job";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 18, 8, 0, 0, TimeSpan.Zero));

    private ScheduledTaskRunLog CreateLog(int limit = 25)
        => new(_time, TestSupport.Options(new ScheduledTasksOptions { RunHistoryLimit = limit }));

    [Fact]
    public void A_finished_run_records_outcome_and_duration()
    {
        var log = CreateLog();

        var runId = log.Start(TaskId, ScheduledTaskTrigger.Manual, "Carl");
        _time.Advance(TimeSpan.FromSeconds(3));
        log.Succeed(TaskId, runId);

        var run = Assert.Single(log.GetRuns(TaskId));
        Assert.Equal(ScheduledTaskRunOutcome.Succeeded, run.Outcome);
        Assert.Equal(ScheduledTaskTrigger.Manual, run.Trigger);
        Assert.Equal("Carl", run.TriggeredBy);
        Assert.Equal(TimeSpan.FromSeconds(3), run.Duration);
        Assert.Equal(Environment.MachineName, run.Server);
    }

    [Fact]
    public void A_failed_run_keeps_the_exception_and_counts_as_a_failure()
    {
        var log = CreateLog();

        var runId = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null);
        log.Fail(TaskId, runId, new InvalidOperationException("Feed unreachable"));

        var run = Assert.Single(log.GetRuns(TaskId));
        Assert.Equal(ScheduledTaskRunOutcome.Failed, run.Outcome);
        Assert.Equal("InvalidOperationException: Feed unreachable", run.Message);
        Assert.Contains("Feed unreachable", run.Details);
        Assert.Equal(new ScheduledTaskRunStats(1, 1, null, run), log.GetStats(TaskId));
    }

    [Fact]
    public void The_first_outcome_wins_when_a_run_is_finished_twice()
    {
        var log = CreateLog();

        var runId = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null);
        log.Succeed(TaskId, runId);
        log.Fail(TaskId, runId, new Exception("notification handler blew up"));

        Assert.Equal(ScheduledTaskRunOutcome.Succeeded, Assert.Single(log.GetRuns(TaskId)).Outcome);
        Assert.Equal(0, log.GetStats(TaskId).Failures);
    }

    [Fact]
    public void Repeated_scheduled_skips_for_the_same_reason_fold_into_one_entry()
    {
        var log = CreateLog();

        log.Skip(TaskId, "Not on this role", ScheduledTaskTrigger.Schedule, null);
        _time.Advance(TimeSpan.FromMinutes(1));
        log.Skip(TaskId, "Not on this role", ScheduledTaskTrigger.Schedule, null);
        _time.Advance(TimeSpan.FromMinutes(1));
        log.Skip(TaskId, "Not on this role", ScheduledTaskTrigger.Schedule, null);

        var run = Assert.Single(log.GetRuns(TaskId));
        Assert.Equal(3, run.Occurrences);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, run.LastOccurrenceUtc);
    }

    [Fact]
    public void Manual_skips_and_new_reasons_are_listed_separately()
    {
        var log = CreateLog();

        log.Skip(TaskId, "Not on this role", ScheduledTaskTrigger.Schedule, null);
        log.Skip(TaskId, "Not on this role", ScheduledTaskTrigger.Manual, "Carl");
        log.Skip(TaskId, "Not MainDom", ScheduledTaskTrigger.Schedule, null);

        Assert.Equal(3, log.GetRuns(TaskId).Count);
    }

    [Fact]
    public void History_is_capped_and_newest_first()
    {
        var log = CreateLog(limit: 2);

        foreach (var _ in Enumerable.Range(0, 4))
        {
            var runId = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null);
            log.Succeed(TaskId, runId);
            _time.Advance(TimeSpan.FromMinutes(1));
        }

        var runs = log.GetRuns(TaskId);
        Assert.Equal(2, runs.Count);
        Assert.True(runs[0].StartedUtc > runs[1].StartedUtc);
        Assert.Equal(4, log.GetStats(TaskId).Runs);
    }

    [Fact]
    public void Stats_separate_the_current_run_from_the_last_finished_one()
    {
        var log = CreateLog();

        var first = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null);
        log.Succeed(TaskId, first);
        var second = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null);

        var stats = log.GetStats(TaskId);
        Assert.Equal(second, stats.Current?.Id);
        Assert.Equal(first, stats.Last?.Id);
    }

    [Fact]
    public void Clear_forgets_finished_runs_but_keeps_the_one_in_progress()
    {
        var log = CreateLog();

        var finished = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null);
        log.Fail(TaskId, finished, new Exception("boom"));
        var running = log.Start(TaskId, ScheduledTaskTrigger.Schedule, null);

        log.Clear(TaskId);

        Assert.Equal(running, Assert.Single(log.GetRuns(TaskId)).Id);
        Assert.Equal(0, log.GetStats(TaskId).Failures);
    }

    [Fact]
    public void Unknown_tasks_have_no_runs()
    {
        var log = CreateLog();

        Assert.Empty(log.GetRuns("recurring:Nope"));
        Assert.Equal(new ScheduledTaskRunStats(0, 0, null, null), log.GetStats("recurring:Nope"));
    }
}
