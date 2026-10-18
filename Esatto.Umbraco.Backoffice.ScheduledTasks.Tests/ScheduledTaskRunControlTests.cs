using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

/// <summary>Stopping executions, attempt time limits, and how an attempt's end is recorded.</summary>
public class ScheduledTaskRunControlTests
{
    private const string TaskId = "recurring:My.Job";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 18, 8, 0, 0, TimeSpan.Zero));
    private readonly ScheduledTaskRunLog _runLog;
    private readonly ScheduledTaskRunControl _control;

    public ScheduledTaskRunControlTests()
    {
        _runLog = new ScheduledTaskRunLog(_time, TestSupport.Options());
        _control = new ScheduledTaskRunControl(_runLog, _time);
    }

    private Task<AttemptResult> RunAsync(
        ScheduledTaskExecution execution, Func<CancellationToken, Task> body, TimeSpan? timeout = null)
    {
        var runId = _runLog.Start(TaskId, ScheduledTaskTrigger.Schedule, null);
        return ScheduledTaskAttempts.RunAsync(_runLog, execution, TaskId, runId, timeout, LogLevel.None, body);
    }

    private ScheduledTaskRun Run => Assert.Single(_runLog.GetRuns(TaskId));

    [Fact]
    public void Nothing_can_be_stopped_when_nothing_runs()
    {
        Assert.False(_control.RequestStop(TaskId, "Carl"));
        Assert.False(_control.HasExecutions);
        Assert.Null(_control.StopRequestedBy(TaskId));
    }

    [Fact]
    public void A_stop_cancels_the_executions_token_and_is_recorded_on_its_run()
    {
        using var execution = _control.Begin(TaskId, CancellationToken.None);
        var runId = _runLog.Start(TaskId, ScheduledTaskTrigger.Schedule, null);
        using var attempt = execution.BeginAttempt(runId, null);

        Assert.True(_control.RequestStop(TaskId, "Carl"));

        Assert.True(attempt.Token.IsCancellationRequested);
        Assert.Equal("Carl", _control.StopRequestedBy(TaskId));
        Assert.Equal("Carl", Run.StopRequestedBy);
    }

    [Fact]
    public void The_first_to_ask_for_a_stop_is_the_one_recorded()
    {
        using var execution = _control.Begin(TaskId, CancellationToken.None);

        Assert.True(_control.RequestStop(TaskId, "Carl"));
        Assert.True(_control.RequestStop(TaskId, "Anna"));

        Assert.Equal("Carl", execution.StopRequestedBy);
    }

    [Fact]
    public void A_stop_requested_on_another_server_finds_the_execution_by_its_run()
    {
        using var execution = _control.Begin(TaskId, CancellationToken.None);
        var runId = _runLog.Start(TaskId, ScheduledTaskTrigger.Schedule, null);
        using var attempt = execution.BeginAttempt(runId, null);

        Assert.False(_control.RequestStopOfRun(Guid.NewGuid(), "Carl"));
        Assert.True(_control.RequestStopOfRun(runId, "Carl"));
        Assert.True(execution.Token.IsCancellationRequested);
    }

    [Fact]
    public void An_execution_is_tracked_until_it_is_disposed()
    {
        var execution = _control.Begin(TaskId, CancellationToken.None);
        Assert.True(_control.IsExecuting(TaskId));
        Assert.True(_control.HasExecutions);

        execution.Dispose();

        Assert.False(_control.IsExecuting(TaskId));
        Assert.False(_control.RequestStop(TaskId, "Carl"));
    }

    [Fact]
    public void Disposing_an_older_execution_does_not_forget_a_newer_one()
    {
        var older = _control.Begin(TaskId, CancellationToken.None);
        using var newer = _control.Begin(TaskId, CancellationToken.None);

        older.Dispose();

        Assert.True(_control.IsExecuting(TaskId));
        Assert.True(_control.RequestStop(TaskId, "Carl"));
        Assert.Equal("Carl", newer.StopRequestedBy);
    }

    [Fact]
    public void Shutdown_cancels_the_execution_but_is_not_a_stop()
    {
        using var shutdown = new CancellationTokenSource();
        using var execution = _control.Begin(TaskId, shutdown.Token);

        shutdown.Cancel();

        Assert.True(execution.Token.IsCancellationRequested);
        Assert.True(execution.IsShuttingDown);
        Assert.False(execution.IsStopRequested);
    }

    [Fact]
    public void An_attempts_token_is_canceled_when_its_time_limit_passes()
    {
        using var execution = _control.Begin(TaskId, CancellationToken.None);
        using var attempt = execution.BeginAttempt(Guid.NewGuid(), TimeSpan.FromMinutes(10));

        _time.Advance(TimeSpan.FromMinutes(9));
        Assert.False(attempt.Token.IsCancellationRequested);

        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.True(attempt.Token.IsCancellationRequested);
        Assert.True(attempt.TimedOut);
        Assert.False(execution.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task An_attempt_stopped_from_the_dashboard_is_canceled_and_says_by_whom()
    {
        using var execution = _control.Begin(TaskId, CancellationToken.None);

        var result = await RunAsync(execution, async token =>
        {
            execution.RequestStop("Carl");
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        });

        Assert.Equal(AttemptOutcome.Stopped, result.Outcome);
        Assert.Equal(ScheduledTaskRunOutcome.Canceled, Run.Outcome);
        Assert.Equal("Stopped by Carl.", Run.Message);
    }

    [Fact]
    public async Task Whatever_a_stopped_job_throws_counts_as_stopping()
    {
        using var execution = _control.Begin(TaskId, CancellationToken.None);

        var result = await RunAsync(execution, _ =>
        {
            execution.RequestStop("Carl");
            throw new InvalidOperationException("half-written file");
        });

        Assert.Equal(AttemptOutcome.Stopped, result.Outcome);
        Assert.Equal(0, _runLog.GetStats(TaskId).Failures);
    }

    [Fact]
    public async Task An_attempt_cut_off_by_shutdown_is_canceled()
    {
        using var shutdown = new CancellationTokenSource();
        using var execution = _control.Begin(TaskId, shutdown.Token);

        var result = await RunAsync(execution, token =>
        {
            shutdown.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        });

        Assert.Equal(AttemptOutcome.ShutDown, result.Outcome);
        Assert.Equal("Canceled because the application is shutting down.", Run.Message);
    }

    [Fact]
    public async Task An_attempt_past_its_time_limit_fails_with_a_timeout()
    {
        using var execution = _control.Begin(TaskId, CancellationToken.None);

        var result = await RunAsync(
            execution,
            token =>
            {
                _time.Advance(TimeSpan.FromMinutes(10));
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
            TimeSpan.FromMinutes(10));

        Assert.Equal(AttemptOutcome.Failed, result.Outcome);
        var timeout = Assert.IsType<TimeoutException>(result.Exception?.SourceException);
        Assert.IsAssignableFrom<OperationCanceledException>(timeout.InnerException);
        Assert.Equal(ScheduledTaskRunOutcome.Failed, Run.Outcome);
        Assert.Equal("Timed out after 10 minutes.", Run.Message);
    }

    [Fact]
    public async Task A_job_that_ignores_its_time_limit_succeeds_with_a_remark()
    {
        using var execution = _control.Begin(TaskId, CancellationToken.None);

        var result = await RunAsync(execution, _ => { _time.Advance(TimeSpan.FromMinutes(2)); return Task.CompletedTask; }, TimeSpan.FromMinutes(1));

        Assert.Equal(AttemptOutcome.Succeeded, result.Outcome);
        Assert.Equal(
            "Finished after its time limit of 1 minute: the job doesn't stop when asked to.",
            Run.Message);
    }

    [Fact]
    public async Task A_job_that_ignores_a_stop_succeeds_with_a_remark()
    {
        using var execution = _control.Begin(TaskId, CancellationToken.None);

        var result = await RunAsync(execution, _ => { execution.RequestStop("Carl"); return Task.CompletedTask; });

        Assert.Equal(AttemptOutcome.Succeeded, result.Outcome);
        Assert.Equal("Finished although Carl asked it to stop: the job doesn't stop when asked to.", Run.Message);
    }

    [Theory]
    [InlineData(30, false, false)]
    [InlineData(60, false, true)]
    [InlineData(30, true, true)]
    [InlineData(-1, false, true)]
    public void Only_tasks_that_run_very_often_keep_routine_runs_in_memory_only(double periodSeconds, bool onCalendar, bool persistsAll)
    {
        var period = periodSeconds < 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(periodSeconds);

        Assert.Equal(persistsAll, ScheduledTaskAttempts.PersistsAllRuns(period, onCalendar, new ScheduledTasksOptions()));
    }
}
