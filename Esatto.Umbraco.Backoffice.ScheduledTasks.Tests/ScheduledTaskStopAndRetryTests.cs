using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Serilog.Events;
using Serilog.Parsing;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Infrastructure.Models;
using Umbraco.Cms.Infrastructure.Notifications;
using Umbraco.Cms.Infrastructure.Services;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

/// <summary>Retries, time limits and stopping a recurring task's run on its host.</summary>
public class RecurringStopAndRetryTests
{
    private static readonly string TaskId = ScheduledTaskIdentity.ForRecurring(typeof(ScriptedJob));

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 18, 8, 0, 0, TimeSpan.Zero));
    private readonly IRuntimeState _runtimeState = Substitute.For<IRuntimeState>();
    private readonly IMainDom _mainDom = Substitute.For<IMainDom>();
    private readonly IServerRoleAccessor _serverRole = Substitute.For<IServerRoleAccessor>();
    private readonly IEventAggregator _events = Substitute.For<IEventAggregator>();
    private readonly IEventMessagesFactory _eventMessages = Substitute.For<IEventMessagesFactory>();
    private readonly IScheduledTaskSettingsStore _settings = Substitute.For<IScheduledTaskSettingsStore>();
    private readonly ScheduledTaskRunLog _runLog;
    private readonly ScheduledTaskRunControl _control;
    private readonly ScriptedJob _job = new(TimeSpan.FromMinutes(5));

    public RecurringStopAndRetryTests()
    {
        _runtimeState.Level.Returns(RuntimeLevel.Run);
        _mainDom.IsMainDom.Returns(true);
        _serverRole.CurrentServerRole.Returns(ServerRole.Single);
        _eventMessages.Get().Returns(_ => new EventMessages());
        _runLog = new ScheduledTaskRunLog(_time, TestSupport.Options());
        _control = new ScheduledTaskRunControl(_runLog, _time);
    }

    private ManagedRecurringBackgroundJobHostedService<ScriptedJob> CreateHost() => new(
        _runtimeState,
        NullLogger<RecurringBackgroundJobHostedService<ScriptedJob>>.Instance,
        _mainDom,
        _serverRole,
        _events,
        _eventMessages,
        _job,
        _time,
        _settings,
        _runLog,
        TestSupport.Options(),
        TestSupport.Calendars,
        new RecurringJobHostRegistry(),
        _control);

    private void Policy(int retries, TimeSpan? timeout = null, bool isPaused = false)
        => _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, isPaused) with
        {
            RunPolicy = new ScheduledTaskRunPolicy(retries, TimeSpan.FromMinutes(1), timeout),
        });

    /// <summary>Runs a tick, moving the clock on in <paramref name="step"/>s while it waits.</summary>
    private async Task TickAsync(ManagedRecurringBackgroundJobHostedService<ScriptedJob> host, TimeSpan? step = null)
    {
        var tick = host.PerformExecuteAsync(CancellationToken.None);
        for (var i = 0; i < 100 && !tick.IsCompleted; i++)
        {
            await Task.Delay(10);
            if (!tick.IsCompleted) _time.Advance(step ?? TimeSpan.FromSeconds(30));
        }

        await tick;
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 500 && !condition(); i++) await Task.Delay(10);
        Assert.True(condition());
    }

    private Task Published<TNotification>(int times) where TNotification : INotification
        => _events.Received(times).PublishAsync(Arg.Any<TNotification>(), Arg.Any<CancellationToken>());

    [Fact]
    public async Task A_failed_run_is_retried_after_the_delay()
    {
        Policy(retries: 2);
        _job.Body = _ => _job.Runs == 1 ? throw new InvalidOperationException("Feed unreachable") : Task.CompletedTask;
        using var host = CreateHost();
        var started = _time.GetUtcNow();

        await TickAsync(host);

        Assert.Equal(2, _job.Runs);
        var runs = _runLog.GetRuns(TaskId);
        Assert.Collection(
            runs,
            retry =>
            {
                Assert.Equal(ScheduledTaskTrigger.Retry, retry.Trigger);
                Assert.Equal(ScheduledTaskRunOutcome.Succeeded, retry.Outcome);
                Assert.Equal(2, retry.Attempt);
                Assert.Equal(3, retry.MaxAttempts);
                Assert.True(retry.StartedUtc >= started.UtcDateTime.AddMinutes(1));
            },
            failed =>
            {
                Assert.Equal(ScheduledTaskTrigger.Schedule, failed.Trigger);
                Assert.Equal(ScheduledTaskRunOutcome.Failed, failed.Outcome);
                Assert.Equal(1, failed.Attempt);
                Assert.Null(failed.RetryAtUtc);
            });
        await Published<RecurringBackgroundJobExecutedNotification>(1);
        await Published<RecurringBackgroundJobFailedNotification>(0);
    }

    [Fact]
    public async Task While_it_waits_for_a_retry_the_task_shows_as_running_with_the_retry_pending()
    {
        Policy(retries: 1);
        _job.Body = _ => _job.Runs == 1 ? throw new InvalidOperationException("boom") : Task.CompletedTask;
        using var host = CreateHost();
        var now = _time.GetUtcNow().UtcDateTime;

        var tick = host.PerformExecuteAsync(CancellationToken.None);
        await WaitUntil(() => _runLog.GetPendingRetry(TaskId) is not null);

        Assert.True(host.IsRunning);
        Assert.Equal(now.AddMinutes(1), _runLog.GetPendingRetry(TaskId)!.RetryAtUtc);

        _time.Advance(TimeSpan.FromMinutes(1));
        await tick;
        Assert.Equal(2, _job.Runs);
    }

    [Fact]
    public async Task When_every_attempt_fails_the_run_fails_once()
    {
        Policy(retries: 2);
        _job.Body = _ => throw new InvalidOperationException("boom");
        using var host = CreateHost();

        await TickAsync(host);

        Assert.Equal(3, _job.Runs);
        Assert.Equal([3, 2, 1], _runLog.GetRuns(TaskId).Select(r => r.Attempt));
        Assert.All(_runLog.GetRuns(TaskId), r => Assert.Equal(ScheduledTaskRunOutcome.Failed, r.Outcome));
        Assert.Null(_runLog.GetPendingRetry(TaskId));
        await Published<RecurringBackgroundJobFailedNotification>(1);
    }

    [Fact]
    public async Task A_stop_during_the_wait_for_a_retry_cancels_the_retry()
    {
        Policy(retries: 2);
        _job.Body = _ => throw new InvalidOperationException("boom");
        using var host = CreateHost();

        var tick = host.PerformExecuteAsync(CancellationToken.None);
        await WaitUntil(() => _runLog.GetPendingRetry(TaskId) is not null);
        Assert.True(_control.RequestStop(TaskId, "Carl"));
        await tick;

        Assert.Equal(1, _job.Runs);
        Assert.Null(_runLog.GetPendingRetry(TaskId));
        Assert.False(host.IsRunning);
        await Published<RecurringBackgroundJobCanceledNotification>(1);
        await Published<RecurringBackgroundJobFailedNotification>(0);
    }

    [Fact]
    public async Task Pausing_ends_the_retries_of_a_scheduled_run()
    {
        Policy(retries: 2);
        _job.Body = _ =>
        {
            Policy(retries: 2, isPaused: true);
            throw new InvalidOperationException("boom");
        };
        using var host = CreateHost();

        await TickAsync(host);

        Assert.Equal(1, _job.Runs);
        Assert.Null(_runLog.GetPendingRetry(TaskId));
        await Published<RecurringBackgroundJobFailedNotification>(1);
    }

    [Fact]
    public async Task Pausing_during_the_wait_ends_the_retries_of_a_scheduled_run()
    {
        Policy(retries: 2);
        _job.Body = _ => throw new InvalidOperationException("boom");
        using var host = CreateHost();

        var tick = host.PerformExecuteAsync(CancellationToken.None);
        await WaitUntil(() => _runLog.GetPendingRetry(TaskId) is not null);
        Policy(retries: 2, isPaused: true);
        _time.Advance(TimeSpan.FromMinutes(1));
        await tick;

        Assert.Equal(1, _job.Runs);
        Assert.Null(_runLog.GetPendingRetry(TaskId));
    }

    [Fact]
    public async Task A_run_started_from_the_dashboard_keeps_retrying_while_paused()
    {
        Policy(retries: 1, isPaused: true);
        _job.Body = _ => _job.Runs == 1 ? throw new InvalidOperationException("boom") : Task.CompletedTask;
        using var host = CreateHost();

        Assert.True(host.RequestRun("Carl"));
        await TickAsync(host);

        Assert.Equal(2, _job.Runs);
        var runs = _runLog.GetRuns(TaskId);
        Assert.Equal([ScheduledTaskTrigger.Retry, ScheduledTaskTrigger.Manual], runs.Select(r => r.Trigger));
        Assert.Equal("Carl", runs[1].TriggeredBy);
    }

    [Fact]
    public async Task A_run_past_its_time_limit_fails_as_timed_out()
    {
        Policy(retries: 0, timeout: TimeSpan.FromMinutes(1));
        _job.Body = token => Task.Delay(Timeout.InfiniteTimeSpan, token);
        using var host = CreateHost();

        await TickAsync(host);

        var run = Assert.Single(_runLog.GetRuns(TaskId));
        Assert.Equal(ScheduledTaskRunOutcome.Failed, run.Outcome);
        Assert.Equal("Timed out after 1 minute.", run.Message);
        await Published<RecurringBackgroundJobFailedNotification>(1);
    }

    [Fact]
    public async Task A_timed_out_run_is_retried()
    {
        Policy(retries: 1, timeout: TimeSpan.FromMinutes(1));
        _job.Body = token => _job.Runs == 1 ? Task.Delay(Timeout.InfiniteTimeSpan, token) : Task.CompletedTask;
        using var host = CreateHost();

        await TickAsync(host);

        Assert.Equal(
            [ScheduledTaskRunOutcome.Succeeded, ScheduledTaskRunOutcome.Failed],
            _runLog.GetRuns(TaskId).Select(r => r.Outcome));
    }

    [Fact]
    public async Task A_stopped_run_is_canceled_and_not_retried()
    {
        Policy(retries: 2);
        var started = new TaskCompletionSource();
        _job.Body = token =>
        {
            started.TrySetResult();
            return Task.Delay(Timeout.InfiniteTimeSpan, token);
        };
        using var host = CreateHost();

        var tick = host.PerformExecuteAsync(CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(_control.RequestStop(TaskId, "Carl"));
        await tick.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, _job.Runs);
        var run = Assert.Single(_runLog.GetRuns(TaskId));
        Assert.Equal(ScheduledTaskRunOutcome.Canceled, run.Outcome);
        Assert.Equal("Stopped by Carl.", run.Message);
        Assert.Equal("Carl", run.StopRequestedBy);
        await Published<RecurringBackgroundJobCanceledNotification>(1);
    }

    [Fact]
    public async Task What_the_job_logs_while_it_runs_is_kept_with_the_run()
    {
        var sink = new ScheduledTaskLogCapture();
        _job.Body = _ =>
        {
            sink.Emit(LogEvent(LogEventLevel.Information, "Imported 120 products"));
            sink.Emit(LogEvent(LogEventLevel.Debug, "Below the minimum level"));
            return Task.CompletedTask;
        };
        using var host = CreateHost();

        await TickAsync(host);
        sink.Emit(LogEvent(LogEventLevel.Warning, "After the run"));

        Assert.EndsWith("INF Imported 120 products", Assert.Single(_runLog.GetRuns(TaskId)).Log);
    }

    private static LogEvent LogEvent(LogEventLevel level, string message)
        => new(DateTimeOffset.UtcNow, level, null, new MessageTemplateParser().Parse(message), []);
}

/// <summary>Retries and stopping of distributed tasks, which are retried through Umbraco's job table.</summary>
public class DistributedStopAndRetryTests
{
    private const string JobName = "NightlyExport";
    private static readonly string TaskId = ScheduledTaskIdentity.ForDistributed(JobName);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 18, 8, 0, 0, TimeSpan.Zero));
    private readonly FakeSettingsStore _settings = new();
    private readonly ScheduledTaskRunLog _runLog;
    private readonly ScheduledTaskRunControl _control;
    private readonly ScriptedDistributedJob _job = new(JobName, TimeSpan.FromHours(1));

    public DistributedStopAndRetryTests()
    {
        _runLog = new ScheduledTaskRunLog(_time, TestSupport.Options());
        _control = new ScheduledTaskRunControl(_runLog, _time);
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private void Setting(int retries = 2, bool isPaused = false, TimeSpan? timeout = null)
        => _settings.Cached[TaskId] = _settings.Stored[TaskId] = TestSupport.Setting(TaskId, isPaused) with
        {
            RunPolicy = new ScheduledTaskRunPolicy(retries, TimeSpan.FromMinutes(2), timeout),
        };

    private TrackedDistributedJob Track(
        ScheduledTaskTrigger trigger = ScheduledTaskTrigger.Schedule, ScheduledTaskRun? retryOf = null)
        => new(_job, trigger, null, _settings, _runLog, NullLogger.Instance, null, _control, null, _time, retryOf);

    private ScheduledTaskRun FailedRunWithRetry(int attempt = 1, int maxAttempts = 3)
    {
        var runId = _runLog.Start(TaskId, ScheduledTaskTrigger.Schedule, null, attempt, maxAttempts, persistAll: true);
        _runLog.Fail(TaskId, runId, new Exception("boom"));
        _runLog.SetRetry(TaskId, runId, Now);
        return _runLog.GetPendingRetry(TaskId)!;
    }

    private ScheduledTaskRun RunById(Guid id) => _runLog.GetRuns(TaskId).Single(r => r.Id == id);

    private IDistributedJobTable Table(DateTime lastRun)
    {
        var table = Substitute.For<IDistributedJobTable>();
        table.PollPeriod.Returns(TimeSpan.FromSeconds(5));
        table.GetAll().Returns(new Dictionary<string, DistributedBackgroundJobModel>
        {
            [JobName] = new() { Name = JobName, Period = _job.Period, LastRun = lastRun, LastAttemptedRun = lastRun },
        });
        return table;
    }

    private ManagedDistributedJobService Decorator(IDistributedJobService inner, IDistributedJobTable table)
        => new(inner, table, _settings, _runLog, [_job], TestSupport.Calendars,
            NullLogger<ManagedDistributedJobService>.Instance, _time, _control);

    [Fact]
    public async Task A_failed_run_schedules_its_retry_and_is_rethrown()
    {
        Setting(retries: 2);
        _job.Body = _ => throw new InvalidOperationException("API down");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Track().ExecuteAsync(CancellationToken.None));

        var run = Assert.Single(_runLog.GetRuns(TaskId));
        Assert.Equal(1, run.Attempt);
        Assert.Equal(3, run.MaxAttempts);
        Assert.Equal(Now.AddMinutes(2), run.RetryAtUtc);
        Assert.Equal(run.Id, _runLog.GetPendingRetry(TaskId)?.Id);
    }

    [Fact]
    public async Task A_retry_is_the_next_attempt_and_no_longer_pending()
    {
        Setting(retries: 2);
        var failed = FailedRunWithRetry();

        await Track(ScheduledTaskTrigger.Retry, failed).ExecuteAsync(CancellationToken.None);

        var retry = _runLog.GetRuns(TaskId)[0];
        Assert.Equal(ScheduledTaskTrigger.Retry, retry.Trigger);
        Assert.Equal(2, retry.Attempt);
        Assert.Equal(3, retry.MaxAttempts);
        Assert.Equal(ScheduledTaskRunOutcome.Succeeded, retry.Outcome);
        Assert.Null(RunById(failed.Id).RetryAtUtc);
        Assert.Null(_runLog.GetPendingRetry(TaskId));
    }

    [Fact]
    public async Task The_last_attempt_does_not_schedule_another()
    {
        Setting(retries: 2);
        var failed = FailedRunWithRetry(attempt: 2, maxAttempts: 3);
        _job.Body = _ => throw new InvalidOperationException("still down");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Track(ScheduledTaskTrigger.Retry, failed).ExecuteAsync(CancellationToken.None));

        var last = _runLog.GetRuns(TaskId)[0];
        Assert.Equal(3, last.Attempt);
        Assert.Null(last.RetryAtUtc);
        Assert.Null(_runLog.GetPendingRetry(TaskId));
    }

    [Fact]
    public async Task Pausing_drops_a_pending_retry_without_running_it()
    {
        Setting(retries: 2, isPaused: true);
        var failed = FailedRunWithRetry();

        await Track(ScheduledTaskTrigger.Retry, failed).ExecuteAsync(CancellationToken.None);

        Assert.Equal(0, _job.Runs);
        Assert.Null(RunById(failed.Id).RetryAtUtc);
    }

    [Fact]
    public async Task A_stopped_run_is_not_retried_and_not_rethrown()
    {
        Setting(retries: 2);
        _job.Body = token =>
        {
            _control.RequestStop(TaskId, "Carl");
            throw new OperationCanceledException(token);
        };

        await Track().ExecuteAsync(CancellationToken.None);

        var run = Assert.Single(_runLog.GetRuns(TaskId));
        Assert.Equal(ScheduledTaskRunOutcome.Canceled, run.Outcome);
        Assert.Equal("Stopped by Carl.", run.Message);
        Assert.Null(_runLog.GetPendingRetry(TaskId));
    }

    [Fact]
    public async Task A_run_past_its_time_limit_is_rethrown_as_a_timeout()
    {
        Setting(retries: 0, timeout: TimeSpan.FromMinutes(5));
        _job.Body = token =>
        {
            _time.Advance(TimeSpan.FromMinutes(5));
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        };

        var thrown = await Assert.ThrowsAsync<TimeoutException>(() => Track().ExecuteAsync(CancellationToken.None));

        Assert.Equal("Timed out after 5 minutes.", thrown.Message);
    }

    [Fact]
    public async Task A_run_cut_off_by_shutdown_is_rethrown_as_canceled()
    {
        using var shutdown = new CancellationTokenSource();
        _job.Body = token =>
        {
            shutdown.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Track().ExecuteAsync(shutdown.Token));

        Assert.Equal(ScheduledTaskRunOutcome.Canceled, Assert.Single(_runLog.GetRuns(TaskId)).Outcome);
    }

    [Fact]
    public async Task A_due_retry_is_handed_out_as_a_retry()
    {
        Setting(retries: 2);
        var failed = FailedRunWithRetry();
        var inner = Substitute.For<IDistributedJobService>();
        inner.TryTakeRunnableAsync().Returns(_job);

        var taken = await Decorator(inner, Table(Now)).TryTakeRunnableAsync();
        await taken!.ExecuteAsync(CancellationToken.None);

        var retry = _runLog.GetRuns(TaskId)[0];
        Assert.Equal(ScheduledTaskTrigger.Retry, retry.Trigger);
        Assert.Equal(failed.Attempt + 1, retry.Attempt);
    }

    [Fact]
    public async Task A_retry_that_is_not_due_yet_leaves_the_run_a_scheduled_one()
    {
        Setting(retries: 2);
        var runId = _runLog.Start(TaskId, ScheduledTaskTrigger.Schedule, null, 1, 3, persistAll: true);
        _runLog.Fail(TaskId, runId, new Exception("boom"));
        _runLog.SetRetry(TaskId, runId, Now.AddMinutes(10));
        var inner = Substitute.For<IDistributedJobService>();
        inner.TryTakeRunnableAsync().Returns(_job);

        var taken = await Decorator(inner, Table(Now)).TryTakeRunnableAsync();
        await taken!.ExecuteAsync(CancellationToken.None);

        var newest = _runLog.GetRuns(TaskId)[0];
        Assert.Equal(ScheduledTaskTrigger.Schedule, newest.Trigger);
        Assert.Equal(1, newest.Attempt);
    }

    [Fact]
    public void A_row_with_a_pending_retry_is_made_due_at_the_retry_time()
    {
        Setting(retries: 2);
        var runId = _runLog.Start(TaskId, ScheduledTaskTrigger.Schedule, null, 1, 3, persistAll: true);
        _runLog.Fail(TaskId, runId, new Exception("boom"));
        _runLog.SetRetry(TaskId, runId, Now.AddMinutes(2));
        var table = Table(lastRun: Now.AddSeconds(-30));

        Decorator(Substitute.For<IDistributedJobService>(), table).ApplySchedules([JobName]);

        table.Received(1).SetPeriods(Arg.Is<IReadOnlyDictionary<string, TimeSpan>>(p => p[JobName] == TimeSpan.FromSeconds(150)));
    }

    [Fact]
    public void A_paused_task_keeps_its_own_period_despite_a_pending_retry()
    {
        Setting(retries: 2, isPaused: true);
        var runId = _runLog.Start(TaskId, ScheduledTaskTrigger.Schedule, null, 1, 3, persistAll: true);
        _runLog.Fail(TaskId, runId, new Exception("boom"));
        _runLog.SetRetry(TaskId, runId, Now.AddMinutes(2));
        var table = Table(lastRun: Now);

        Decorator(Substitute.For<IDistributedJobService>(), table).ApplySchedules([JobName]);

        table.Received(1).SetPeriods(Arg.Is<IReadOnlyDictionary<string, TimeSpan>>(p => p[JobName] == _job.Period));
    }
}
