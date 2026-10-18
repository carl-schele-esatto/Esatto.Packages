using Esatto.Umbraco.Backoffice.ScheduledTasks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

public class ScheduledTaskServiceStopTests
{
    private readonly ServiceHarness _harness = new();

    private static ScheduledTaskRun RunningOn(string server, string taskId = "")
        => new(Guid.NewGuid(), taskId, ScheduledTaskTrigger.Schedule, null, server, DateTime.UtcNow, null,
            ScheduledTaskRunOutcome.Running, null, null, 1, DateTime.UtcNow);

    [Fact]
    public void An_unknown_task_cannot_be_stopped()
        => Assert.Equal(ScheduledTaskCommandStatus.NotFound, _harness.Create().Stop("recurring:Nope", "Carl").Status);

    [Fact]
    public void A_task_that_is_not_running_anywhere_cannot_be_stopped()
    {
        var result = _harness.Create().Stop(ServiceHarness.RecurringId, "Carl");

        Assert.Equal(ScheduledTaskCommandStatus.Conflict, result.Status);
        Assert.Equal("The task isn't running.", result.Message);
        _harness.Audit.DidNotReceiveWithAnyArgs().Write(default!, default!, default);
    }

    [Fact]
    public void A_run_on_this_server_is_asked_to_stop()
    {
        using var execution = _harness.Control.Begin(ServiceHarness.RecurringId, CancellationToken.None);

        var result = _harness.Create().Stop(ServiceHarness.RecurringId, "Carl");

        Assert.Equal(ScheduledTaskCommandStatus.Ok, result.Status);
        Assert.True(execution.Token.IsCancellationRequested);
        Assert.Equal("Carl", execution.StopRequestedBy);
        _harness.Audit.Received(1).Write(ServiceHarness.RecurringId, ScheduledTaskAuditActions.StopRequested, "Carl", null);
    }

    [Fact]
    public void Who_asked_a_run_to_stop_is_shown_on_the_task()
    {
        using var execution = _harness.Control.Begin(ServiceHarness.RecurringId, CancellationToken.None);
        var service = _harness.Create();

        service.Stop(ServiceHarness.RecurringId, "Carl");

        Assert.Equal("Carl", service.Get(ServiceHarness.RecurringId)?.StopRequestedBy);
        Assert.Equal("Carl", service.Get(ServiceHarness.RecurringId)!.ToDto().StopRequestedBy);
    }

    [Fact]
    public void Runs_on_other_servers_are_asked_to_stop_through_the_database()
    {
        _harness.RunStore.IsAvailable.Returns(true);
        var elsewhere = RunningOn("WEB2");
        var here = RunningOn(Environment.MachineName);
        _harness.RunStore.GetActive(ServiceHarness.DistributedId).Returns([elsewhere, here]);
        _harness.RunStore.RequestStop(elsewhere.Id, "Carl").Returns(true);

        var result = _harness.Create().Stop(ServiceHarness.DistributedId, "Carl");

        Assert.Equal(ScheduledTaskCommandStatus.Ok, result.Status);
        _harness.RunStore.DidNotReceive().RequestStop(here.Id, Arg.Any<string>());
        _harness.Audit.Received(1).Write(ServiceHarness.DistributedId, ScheduledTaskAuditActions.StopRequested, "Carl", "On WEB2");
    }

    [Fact]
    public void A_run_elsewhere_that_finished_meanwhile_is_not_a_stop()
    {
        _harness.RunStore.IsAvailable.Returns(true);
        _harness.RunStore.GetActive(ServiceHarness.DistributedId).Returns([RunningOn("WEB2")]);

        var result = _harness.Create().Stop(ServiceHarness.DistributedId, "Carl");

        Assert.Equal(ScheduledTaskCommandStatus.Conflict, result.Status);
    }

    [Fact]
    public void A_database_that_cannot_be_reached_still_stops_the_run_here()
    {
        _harness.RunStore.IsAvailable.Returns(true);
        _harness.RunStore.GetActive(Arg.Any<string>()).Throws(new InvalidOperationException("database unreachable"));
        using var execution = _harness.Control.Begin(ServiceHarness.RecurringId, CancellationToken.None);

        var result = _harness.Create().Stop(ServiceHarness.RecurringId, "Carl");

        Assert.Equal(ScheduledTaskCommandStatus.Ok, result.Status);
        Assert.True(execution.IsStopRequested);
    }

    [Fact]
    public void Stopping_a_distributed_task_waiting_for_its_retry_cancels_the_retry()
    {
        var runLog = _harness.RunLog;
        var runId = runLog.Start(ServiceHarness.DistributedId, ScheduledTaskTrigger.Schedule, null, 1, 3, persistAll: true);
        runLog.Fail(ServiceHarness.DistributedId, runId, new Exception("boom"));
        runLog.SetRetry(ServiceHarness.DistributedId, runId, DateTime.UtcNow.AddMinutes(1));

        var result = _harness.Create().Stop(ServiceHarness.DistributedId, "Carl");

        Assert.Equal(ScheduledTaskCommandStatus.Ok, result.Status);
        Assert.Equal("Retry canceled.", result.Message);
        Assert.Null(runLog.GetPendingRetry(ServiceHarness.DistributedId));
        // The row goes back to its schedule instead of coming due at the retry time.
        _harness.Table.Received(1).SetPeriods(Arg.Is<IReadOnlyDictionary<string, TimeSpan>>(p =>
            p[ServiceHarness.DistributedName] == TimeSpan.FromHours(1)));
        _harness.Audit.Received(1).Write(ServiceHarness.DistributedId, ScheduledTaskAuditActions.RetryCanceled, "Carl", null);
    }
}

public class ScheduledTaskServiceRunPolicyTests
{
    private readonly ServiceHarness _harness = new();

    private ScheduledTaskRunPolicy? Saved(string taskId)
        => _harness.Settings.ChangeSavedFor(taskId)(ScheduledTaskSettingState.Default).RunPolicy;

    [Fact]
    public void An_unknown_task_has_no_run_policy()
        => Assert.Equal(
            ScheduledTaskCommandStatus.NotFound,
            _harness.Create().SetRunPolicy("recurring:Nope", new ScheduledTaskRunPolicy(1, null, null), "Carl").Status);

    [Fact]
    public void Values_outside_the_allowed_range_are_refused_and_not_saved()
    {
        var result = _harness.Create().SetRunPolicy(ServiceHarness.RecurringId, new ScheduledTaskRunPolicy(11, null, null), "Carl");

        Assert.Equal(ScheduledTaskCommandStatus.Invalid, result.Status);
        Assert.Equal("Retries must be between 0 and 10.", result.Message);
        _harness.Settings.DidNotReceiveWithAnyArgs().Update(default!, default!, default);
    }

    [Fact]
    public void A_new_policy_is_saved()
    {
        var policy = new ScheduledTaskRunPolicy(3, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10));

        var result = _harness.Create().SetRunPolicy(ServiceHarness.RecurringId, policy, "Carl");

        Assert.Equal(ScheduledTaskCommandStatus.Ok, result.Status);
        Assert.Equal(policy, Saved(ServiceHarness.RecurringId));
    }

    [Fact]
    public void Values_equal_to_the_jobs_own_defaults_are_not_stored()
    {
        // RetryingJob: 3 retries, 2 minutes apart, 10-minute limit.
        _harness.Create().SetRunPolicy(
            ServiceHarness.RetryingId, new ScheduledTaskRunPolicy(3, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10)), "Carl");

        Assert.Null(Saved(ServiceHarness.RetryingId));
    }

    [Fact]
    public void Only_the_values_that_differ_from_the_defaults_are_stored()
    {
        _harness.Create().SetRunPolicy(
            ServiceHarness.RetryingId, new ScheduledTaskRunPolicy(3, TimeSpan.FromMinutes(5), TimeSpan.Zero), "Carl");

        Assert.Equal(new ScheduledTaskRunPolicy(null, TimeSpan.FromMinutes(5), TimeSpan.Zero), Saved(ServiceHarness.RetryingId));
    }

    [Fact]
    public void Null_restores_the_jobs_defaults()
    {
        var result = _harness.Create().SetRunPolicy(ServiceHarness.RetryingId, null, "Carl");

        Assert.Equal("Retries and time limit back to the task's defaults.", result.Message);
        Assert.Null(_harness.Settings.ChangeSavedFor(ServiceHarness.RetryingId)(
            ScheduledTaskSettingState.Default with { RunPolicy = new ScheduledTaskRunPolicy(1, null, null) }).RunPolicy);
    }

    [Fact]
    public void A_task_shows_the_policy_it_runs_with_and_its_defaults()
    {
        _harness.Store(ServiceHarness.RetryingId, s => s with { RunPolicy = new ScheduledTaskRunPolicy(1, null, TimeSpan.Zero) });

        var dto = _harness.Create().Get(ServiceHarness.RetryingId)!.ToDto().RunPolicy!;

        Assert.Equal(new ScheduledTaskRunPolicyStateDto(1, 120, null, true, 3, 120, 600), dto);
    }

    [Fact]
    public void A_task_without_a_policy_runs_without_retries_or_limit()
    {
        var dto = _harness.Create().Get(ServiceHarness.RecurringId)!.ToDto().RunPolicy!;

        Assert.Equal(new ScheduledTaskRunPolicyStateDto(0, 60, null, false, 0, 60, null), dto);
    }
}

public class ScheduledTaskDtoTests
{
    private readonly ServiceHarness _harness = new();

    [Fact]
    public void A_pending_retry_shows_when_and_which_attempt_comes_next()
    {
        var runLog = _harness.RunLog;
        var retryAt = DateTime.UtcNow.AddMinutes(2);
        var runId = runLog.Start(ServiceHarness.DistributedId, ScheduledTaskTrigger.Schedule, null, 2, 4, persistAll: true);
        runLog.Fail(ServiceHarness.DistributedId, runId, new Exception("boom"));
        runLog.SetRetry(ServiceHarness.DistributedId, runId, retryAt);

        var dto = _harness.Create().Get(ServiceHarness.DistributedId)!.ToDto();

        Assert.Equal(retryAt, dto.RetryAtUtc);
        Assert.Equal(3, dto.NextAttempt);
        Assert.Equal(4, dto.MaxAttempts);
    }

    [Fact]
    public void The_overview_leaves_the_last_runs_log_out_but_the_run_list_has_it()
    {
        var runLog = _harness.RunLog;
        var runId = runLog.Start(ServiceHarness.RecurringId, ScheduledTaskTrigger.Schedule, null);
        runLog.AppendLog(ServiceHarness.RecurringId, runId, "Imported 120 products");
        runLog.Succeed(ServiceHarness.RecurringId, runId);
        var service = _harness.Create();

        var task = service.Get(ServiceHarness.RecurringId)!.ToDto();
        var run = Assert.Single(service.GetRuns(ServiceHarness.RecurringId)!).ToDto();

        Assert.Equal(runId, task.LastRun?.Id);
        Assert.Null(task.LastRun?.Log);
        Assert.Equal("Imported 120 products", run.Log);
    }

    [Fact]
    public void A_run_carries_its_attempts_retry_and_stop_request()
    {
        var at = new DateTime(2026, 10, 18, 8, 0, 0, DateTimeKind.Utc);
        var run = new ScheduledTaskRun(
            Guid.NewGuid(), ServiceHarness.RecurringId, ScheduledTaskTrigger.Retry, null, "WEB1", at, at.AddSeconds(2),
            ScheduledTaskRunOutcome.Failed, "boom", null, 1, at.AddSeconds(2))
        {
            Attempt = 2,
            MaxAttempts = 3,
            RetryAtUtc = at.AddMinutes(1),
            StopRequestedBy = "Carl",
        };

        var dto = run.ToDto();

        Assert.Equal("retry", dto.Trigger);
        Assert.Equal((2, 3), (dto.Attempt, dto.MaxAttempts));
        Assert.Equal(at.AddMinutes(1), dto.RetryAtUtc);
        Assert.Equal("Carl", dto.StopRequestedBy);
        Assert.Equal(2000, dto.DurationMs);
    }
}
