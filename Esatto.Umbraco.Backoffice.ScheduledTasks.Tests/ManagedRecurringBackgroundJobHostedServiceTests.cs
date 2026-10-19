using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Infrastructure.HostedServices;
using Umbraco.Cms.Infrastructure.Notifications;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

public class ManagedRecurringBackgroundJobHostedServiceTests
{
    private static readonly string TaskId = ScheduledTaskIdentity.ForRecurring(typeof(ScriptedJob));

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 18, 8, 0, 0, TimeSpan.Zero));
    private readonly IRuntimeState _runtimeState = Substitute.For<IRuntimeState>();
    private readonly IMainDom _mainDom = Substitute.For<IMainDom>();
    private readonly IServerRoleAccessor _serverRole = Substitute.For<IServerRoleAccessor>();
    private readonly IEventAggregator _events = Substitute.For<IEventAggregator>();
    private readonly IEventMessagesFactory _eventMessages = Substitute.For<IEventMessagesFactory>();
    private readonly IScheduledTaskSettingsStore _settings = Substitute.For<IScheduledTaskSettingsStore>();
    private readonly RecurringJobHostRegistry _registry = new();
    private readonly ScheduledTaskRunLog _runLog;
    private readonly ScriptedJob _job = new(TimeSpan.FromMinutes(5));

    public ManagedRecurringBackgroundJobHostedServiceTests()
    {
        _runtimeState.Level.Returns(RuntimeLevel.Run);
        _mainDom.IsMainDom.Returns(true);
        _serverRole.CurrentServerRole.Returns(ServerRole.Single);
        _eventMessages.Get().Returns(_ => new EventMessages());
        _runLog = new ScheduledTaskRunLog(_time, TestSupport.Options());
    }

    private readonly ErrorCountingLogger _logger = new();

    private ManagedRecurringBackgroundJobHostedService<ScriptedJob> CreateHost() => new(
        _runtimeState,
        _logger,
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
        _registry);

    /// <summary>
    /// Runs one tick. A paused tick backs off on the (fake) clock before it returns, so the clock is
    /// moved on while the tick is waiting.
    /// </summary>
    private async Task TickAsync(ManagedRecurringBackgroundJobHostedService<ScriptedJob> host)
    {
        var tick = host.PerformExecuteAsync(CancellationToken.None);
        for (var attempt = 0; attempt < 20 && !tick.IsCompleted; attempt++)
        {
            await Task.Delay(10);
            if (!tick.IsCompleted) _time.Advance(TimeSpan.FromSeconds(30));
        }

        await tick;
    }

    [Fact]
    public void The_host_registers_itself_under_the_job_type()
    {
        using var host = CreateHost();

        Assert.True(_registry.TryGet(TaskId, out var registered));
        Assert.Same(host, registered);
        Assert.Same(_job, registered.Job);
    }

    [Fact]
    public async Task A_scheduled_tick_runs_the_job_and_records_success()
    {
        using var host = CreateHost();

        await TickAsync(host);

        Assert.Equal(1, _job.Runs);
        var run = Assert.Single(_runLog.GetRuns(TaskId));
        Assert.Equal(ScheduledTaskRunOutcome.Succeeded, run.Outcome);
        Assert.Equal(ScheduledTaskTrigger.Schedule, run.Trigger);
        await _events.Received(1).PublishAsync(Arg.Any<RecurringBackgroundJobExecutedNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_paused_task_skips_scheduled_ticks_without_logging_them()
    {
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, isPaused: true));
        using var host = CreateHost();

        await TickAsync(host);

        Assert.Equal(0, _job.Runs);
        Assert.Empty(_runLog.GetRuns(TaskId));
        await _events.Received(1).PublishAsync(Arg.Any<RecurringBackgroundJobIgnoredNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_now_runs_a_paused_task_and_records_who_asked()
    {
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, isPaused: true));
        using var host = CreateHost();

        Assert.True(host.RequestRun("Carl"));
        await TickAsync(host);

        Assert.Equal(1, _job.Runs);
        var run = Assert.Single(_runLog.GetRuns(TaskId));
        Assert.Equal(ScheduledTaskTrigger.Manual, run.Trigger);
        Assert.Equal("Carl", run.TriggeredBy);
    }

    [Fact]
    public async Task A_failing_job_is_recorded_with_its_exception()
    {
        _job.Body = _ => throw new InvalidOperationException("SMTP down");
        using var host = CreateHost();

        await TickAsync(host);

        var run = Assert.Single(_runLog.GetRuns(TaskId));
        Assert.Equal(ScheduledTaskRunOutcome.Failed, run.Outcome);
        Assert.Equal("InvalidOperationException: SMTP down", run.Message);
        await _events.Received(1).PublishAsync(Arg.Any<RecurringBackgroundJobFailedNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_job_canceled_by_shutdown_is_recorded_as_canceled()
    {
        using var shutdown = new CancellationTokenSource();
        _job.Body = token =>
        {
            shutdown.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        };
        using var host = CreateHost();

        await host.PerformExecuteAsync(shutdown.Token);

        Assert.Equal(ScheduledTaskRunOutcome.Canceled, Assert.Single(_runLog.GetRuns(TaskId)).Outcome);
    }

    [Fact]
    public async Task A_server_whose_role_does_not_match_skips_and_says_why()
    {
        _serverRole.CurrentServerRole.Returns(ServerRole.Subscriber);
        using var host = CreateHost();

        await TickAsync(host);

        Assert.Equal(0, _job.Runs);
        var run = Assert.Single(_runLog.GetRuns(TaskId));
        Assert.Equal(ScheduledTaskRunOutcome.Skipped, run.Outcome);
        Assert.Contains("Subscriber", run.Message);
    }

    [Fact]
    public async Task Run_now_is_refused_while_the_job_is_running()
    {
        var release = new TaskCompletionSource();
        _job.Body = _ => release.Task;
        using var host = CreateHost();

        var running = host.PerformExecuteAsync(CancellationToken.None);
        Assert.True(host.IsRunning);
        Assert.False(host.RequestRun("Carl"));

        release.SetResult();
        await running;
        Assert.False(host.IsRunning);
    }

    [Fact]
    public async Task An_interval_override_reschedules_the_host()
    {
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromMinutes(1)));
        using var host = CreateHost();

        await TickAsync(host);

        Assert.Equal(TimeSpan.FromMinutes(1), host.EffectivePeriod);
        Assert.Equal(_time.GetUtcNow().UtcDateTime.AddMinutes(1), host.NextRunUtc);
    }

    [Fact]
    public void An_override_survives_the_job_changing_its_own_period()
    {
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromMinutes(1)));
        using var host = CreateHost();
        host.SyncWithSettings();

        _job.ChangeOwnPeriod(TimeSpan.FromHours(1));

        Assert.Equal(TimeSpan.FromMinutes(1), host.EffectivePeriod);
    }

    [Fact]
    public void Without_an_override_the_host_follows_the_job_period()
    {
        using var host = CreateHost();
        host.SyncWithSettings();

        _job.ChangeOwnPeriod(TimeSpan.FromHours(1));

        Assert.Equal(TimeSpan.FromHours(1), host.EffectivePeriod);
    }

    [Fact]
    public void Framework_timers_cannot_wait_longer_than_about_49_days()
    {
        // Why long intervals need the heartbeat: Umbraco's loop waits with exactly this call.
        using var ok = new CancellationTokenSource(TimeSpan.FromDays(49), TimeProvider.System);
        Assert.Throws<ArgumentOutOfRangeException>(() => new CancellationTokenSource(TimeSpan.FromDays(50), TimeProvider.System));
    }

    [Fact]
    public void A_long_interval_is_scheduled_as_an_hourly_heartbeat()
    {
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromDays(60)));
        using var host = CreateHost();

        host.SyncWithSettings();

        Assert.Equal(TimeSpan.FromDays(60), host.EffectivePeriod);
        Assert.Equal(ManagedRecurringBackgroundJobHostedService<ScriptedJob>.Heartbeat, host.TimerPeriod);
    }

    [Fact]
    public void A_job_whose_own_period_is_too_long_for_a_timer_starts_on_the_heartbeat()
    {
        var job = new ScriptedJob(TimeSpan.FromDays(90));
        using var host = new ManagedRecurringBackgroundJobHostedService<ScriptedJob>(
            _runtimeState, NullLogger<RecurringBackgroundJobHostedService<ScriptedJob>>.Instance, _mainDom, _serverRole,
            _events, _eventMessages, job, _time, _settings, _runLog, TestSupport.Options(), TestSupport.Calendars, _registry);

        Assert.Equal(TimeSpan.FromDays(90), host.EffectivePeriod);
        Assert.Equal(ManagedRecurringBackgroundJobHostedService<ScriptedJob>.Heartbeat, host.TimerPeriod);
    }

    [Fact]
    public void Going_back_to_a_short_interval_leaves_the_heartbeat()
    {
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromDays(60)));
        using var host = CreateHost();
        host.SyncWithSettings();

        _settings.Get(TaskId).Returns((ScheduledTaskSetting?)null);
        host.SyncWithSettings();

        Assert.Equal(_job.Period, host.EffectivePeriod);
        Assert.Equal(_job.Period, host.TimerPeriod);
    }

    [Fact]
    public async Task Heartbeat_ticks_before_the_interval_has_passed_are_skipped_silently()
    {
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromDays(60)));
        using var host = CreateHost();

        await TickAsync(host); // first tick after startup runs
        for (var hour = 1; hour < 60 * 24; hour++)
        {
            _time.Advance(TimeSpan.FromHours(1));
            await TickAsync(host);
        }

        Assert.Equal(1, _job.Runs);
        await _events.Received(1).PublishAsync(Arg.Any<RecurringBackgroundJobExecutingNotification>(), Arg.Any<CancellationToken>());

        _time.Advance(TimeSpan.FromHours(1)); // 60 days after the first run
        await TickAsync(host);

        Assert.Equal(2, _job.Runs);
        Assert.Equal(_time.GetUtcNow().UtcDateTime.AddDays(60), host.NextRunUtc);
    }

    [Fact]
    public async Task A_long_interval_task_that_could_not_run_is_retried_on_the_next_heartbeat()
    {
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromDays(60)));
        _serverRole.CurrentServerRole.Returns(ServerRole.Unknown); // e.g. before the server is registered
        using var host = CreateHost();

        await TickAsync(host);
        Assert.Equal(0, _job.Runs);

        _serverRole.CurrentServerRole.Returns(ServerRole.Single);
        _time.Advance(ManagedRecurringBackgroundJobHostedService<ScriptedJob>.Heartbeat);
        await TickAsync(host);

        Assert.Equal(1, _job.Runs);
    }

    [Fact]
    public async Task Resuming_an_overdue_long_interval_task_runs_it_on_the_next_heartbeat()
    {
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, isPaused: true, period: TimeSpan.FromDays(60)));
        using var host = CreateHost();
        await TickAsync(host);
        Assert.Equal(0, _job.Runs);

        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromDays(60)));
        _time.Advance(ManagedRecurringBackgroundJobHostedService<ScriptedJob>.Heartbeat);
        await TickAsync(host);

        Assert.Equal(1, _job.Runs);
    }

    [Fact]
    public async Task On_the_real_period_a_blocked_tick_still_uses_its_slot()
    {
        // Umbraco's own semantics for ordinary intervals are kept as they are.
        _serverRole.CurrentServerRole.Returns(ServerRole.Unknown);
        using var host = CreateHost();

        await TickAsync(host);

        Assert.Equal(_time.GetUtcNow().UtcDateTime + _job.Period, host.NextRunUtc);
    }

    [Fact]
    public async Task Run_now_does_not_wait_for_a_long_interval()
    {
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromDays(365)));
        using var host = CreateHost();
        await TickAsync(host);

        _time.Advance(TimeSpan.FromHours(1));
        host.RequestRun("Carl");
        await TickAsync(host);

        Assert.Equal(2, _job.Runs);
    }

    [Fact]
    public async Task Umbracos_timer_loop_survives_an_interval_longer_than_a_timer_can_wait()
    {
        // Regression: handing a 60-day period to Umbraco's loop faults it (ArgumentOutOfRangeException
        // from CancellationTokenSource), which silently stops the job until the site restarts.
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromDays(60)));
        _job.DelayOverride = TimeSpan.FromSeconds(10);
        using var host = CreateHost();
        host.SyncWithSettings();

        await host.StartAsync(CancellationToken.None);
        try
        {
            // The loop may arm its first timer after StartAsync returns; nudge the clock until it has run.
            var ran = false;
            for (var attempt = 0; attempt < 50 && !ran; attempt++)
            {
                _time.Advance(TimeSpan.FromSeconds(10));
                ran = await _job.RunCompleted.WaitAsync(TimeSpan.FromMilliseconds(100));
            }

            Assert.True(ran, "the first run never happened");
            await Task.Delay(200); // let the loop arm its next wait

            Assert.False(host.ExecuteTask!.IsFaulted, host.ExecuteTask.Exception?.ToString());

            _time.Advance(TimeSpan.FromHours(1)); // a heartbeat tick, not due
            await Task.Delay(200);

            Assert.False(host.ExecuteTask.IsFaulted, host.ExecuteTask.Exception?.ToString());
            Assert.Equal(1, _job.Runs);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task A_code_trigger_runs_on_a_long_interval_heartbeat()
    {
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromDays(60)));
        using var host = CreateHost();
        await TickAsync(host);

        _time.Advance(TimeSpan.FromHours(1));
        Assert.True(host.Trigger(NextExecutionStrategy.None));
        await TickAsync(host);

        Assert.Equal(2, _job.Runs);
        Assert.Equal(ScheduledTaskTrigger.Code, _runLog.GetRuns(TaskId)[0].Trigger);
    }

    [Fact]
    public async Task A_code_trigger_respects_the_pause()
    {
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, isPaused: true));
        using var host = CreateHost();

        host.Trigger(NextExecutionStrategy.None);
        await TickAsync(host);

        Assert.Equal(0, _job.Runs);
    }

    [Fact]
    public async Task A_dashboard_run_is_not_downgraded_by_a_code_trigger_before_the_tick()
    {
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, isPaused: true));
        using var host = CreateHost();

        host.RequestRun("Carl");
        host.Trigger(NextExecutionStrategy.None);
        await TickAsync(host);

        Assert.Equal(1, _job.Runs);
        Assert.Equal("Carl", Assert.Single(_runLog.GetRuns(TaskId)).TriggeredBy);
    }

    [Fact]
    public async Task A_code_trigger_after_a_dashboard_run_request_still_applies_its_strategy()
    {
        // Umbraco's loop applies the latest trigger's strategy; the kept schedule must do the same.
        using var host = CreateHost();
        await TickAsync(host);
        _time.Advance(TimeSpan.FromMinutes(2));

        host.RequestRun("Carl");
        host.Trigger(NextExecutionStrategy.Reset);
        await TickAsync(host);

        Assert.Equal("Carl", _runLog.GetRuns(TaskId)[0].TriggeredBy);
        Assert.Equal(_time.GetUtcNow().UtcDateTime + _job.Period, host.NextRunUtc);
    }

    [Fact]
    public async Task Run_now_before_the_first_scheduled_tick_starts_a_long_interval_schedule()
    {
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromDays(60)));
        using var host = CreateHost();

        host.RequestRun("Carl");
        await TickAsync(host);
        _time.Advance(TimeSpan.FromHours(1));
        await TickAsync(host); // first heartbeat: not due, the run above started the schedule

        Assert.Equal(1, _job.Runs);
        Assert.Equal(_time.GetUtcNow().UtcDateTime.AddHours(-1).AddDays(60), host.NextRunUtc);
    }

    [Fact]
    public async Task A_reset_trigger_restarts_the_schedule_from_the_triggered_run()
    {
        using var host = CreateHost();
        await TickAsync(host);
        _time.Advance(TimeSpan.FromMinutes(2));

        host.Trigger(NextExecutionStrategy.Reset);
        await TickAsync(host);

        Assert.Equal(_time.GetUtcNow().UtcDateTime + _job.Period, host.NextRunUtc);
    }

    [Fact]
    public async Task A_trigger_with_a_next_delay_moves_the_next_run()
    {
        using var host = CreateHost();
        await TickAsync(host);

        host.Trigger(TimeSpan.FromMinutes(1));
        await TickAsync(host);

        Assert.Equal(_time.GetUtcNow().UtcDateTime.AddMinutes(1), host.NextRunUtc);
    }

    [Fact]
    public async Task A_paused_task_with_a_zero_period_backs_off_instead_of_spinning()
    {
        var job = new ScriptedJob(TimeSpan.Zero);
        _settings.Get(ScheduledTaskIdentity.ForRecurring(typeof(ScriptedJob)))
            .Returns(TestSupport.Setting(TaskId, isPaused: true));
        using var host = new ManagedRecurringBackgroundJobHostedService<ScriptedJob>(
            _runtimeState, NullLogger<RecurringBackgroundJobHostedService<ScriptedJob>>.Instance, _mainDom, _serverRole,
            _events, _eventMessages, job, _time, _settings, _runLog, TestSupport.Options(), TestSupport.Calendars, _registry);

        var tick = host.PerformExecuteAsync(CancellationToken.None);
        await Task.Delay(50);
        Assert.False(tick.IsCompleted, "a paused tick should wait before the loop comes round again");

        host.RequestRun("Carl"); // wakes the back-off
        await tick.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task An_interval_changed_during_a_tick_reaches_the_timer_loop_on_the_next_sync()
    {
        // Umbraco's loop only notices a period change while it waits, and bases each wait on the
        // period it read before the previous one. A change applied inside a tick would otherwise
        // only take effect one (old) period later.
        _job.DelayOverride = TimeSpan.FromSeconds(10);
        using var host = CreateHost();
        await host.StartAsync(CancellationToken.None);
        try
        {
            await NudgeUntilRunAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(200); // waiting the job's own 5 minutes now

            _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromMinutes(1)));
            _time.Advance(_job.Period); // the next tick reads the new interval mid-tick
            Assert.True(await _job.RunCompleted.WaitAsync(TimeSpan.FromSeconds(10)), "the second run never happened");
            await Task.Delay(200);

            host.SyncWithSettings(); // what ScheduledTaskSettingsSynchronizer does
            await Task.Delay(200);

            _time.Advance(TimeSpan.FromMinutes(1));
            Assert.True(await _job.RunCompleted.WaitAsync(TimeSpan.FromSeconds(10)), "the new interval wasn't applied");
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    private async Task NudgeUntilRunAsync(TimeSpan step)
    {
        var ran = false;
        for (var attempt = 0; attempt < 50 && !ran; attempt++)
        {
            _time.Advance(step);
            ran = await _job.RunCompleted.WaitAsync(TimeSpan.FromMilliseconds(100));
        }

        Assert.True(ran, "the first run never happened");
    }

    [Fact]
    public async Task Before_the_first_run_the_next_run_is_start_plus_delay()
    {
        using var host = CreateHost();
        var started = _time.GetUtcNow().UtcDateTime;

        await host.StartAsync(CancellationToken.None);
        try
        {
            Assert.Equal(started + _job.Delay, host.NextRunUtc);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>Starts the host's real timer loop and waits for its first run (10 seconds after start).</summary>
    private async Task StartAndRunOnceAsync(ManagedRecurringBackgroundJobHostedService<ScriptedJob> host)
    {
        _job.DelayOverride = TimeSpan.FromSeconds(10);
        await host.StartAsync(CancellationToken.None);
        await NudgeUntilRunAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(200); // let the loop arm its next wait
    }

    /// <summary>Moves the clock on and reports whether the real loop ran the job.</summary>
    private async Task<bool> AdvanceAndRunAsync(TimeSpan by, int waitMs = 300)
    {
        _time.Advance(by);
        var ran = await _job.RunCompleted.WaitAsync(TimeSpan.FromMilliseconds(waitMs));
        await Task.Delay(100); // let the loop arm its next wait
        return ran;
    }

    private void AssertLoopHealthy(ManagedRecurringBackgroundJobHostedService<ScriptedJob> host)
    {
        Assert.False(host.ExecuteTask!.IsCompleted, host.ExecuteTask.Exception?.ToString() ?? "the timer loop ended");
        Assert.Equal(0, _logger.Errors);
    }

    [Fact]
    public async Task Repeated_replace_triggers_do_not_push_a_heartbeat_loop_past_a_timer()
    {
        // Regression: each Replace made Umbraco's hourly loop wait what was left plus another hour,
        // so ~1,200 triggers a minute apart pushed its wait past what a timer can wait, faulting it.
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromDays(60)));
        using var host = CreateHost();
        host.SyncWithSettings();
        await StartAndRunOnceAsync(host);
        try
        {
            for (var trigger = 0; trigger < 1250; trigger++)
            {
                _time.Advance(TimeSpan.FromMinutes(1));
                Assert.True(host.Trigger(NextExecutionStrategy.Replace));
                Assert.True(await _job.RunCompleted.WaitAsync(TimeSpan.FromSeconds(5)), $"trigger {trigger} didn't run");
            }

            await Task.Delay(200);
            AssertLoopHealthy(host);

            // The loop still answers: a custom delay runs on time.
            Assert.True(host.Trigger(TimeSpan.FromMinutes(5)));
            Assert.True(await _job.RunCompleted.WaitAsync(TimeSpan.FromSeconds(5)));
            await Task.Delay(200);
            Assert.True(await AdvanceAndRunAsync(TimeSpan.FromMinutes(5), 2000));
            AssertLoopHealthy(host);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Replace_triggers_beyond_a_timer_wait_off_the_heartbeat_run_on_the_replaced_schedule()
    {
        // Regression: two Replace triggers on a 20-day interval made Umbraco's loop wait 60 days.
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromDays(20)));
        using var host = CreateHost();
        host.SyncWithSettings();
        await StartAndRunOnceAsync(host);
        var firstRun = _time.GetUtcNow().UtcDateTime;
        try
        {
            for (var trigger = 0; trigger < 2; trigger++)
            {
                Assert.True(host.Trigger(NextExecutionStrategy.Replace));
                Assert.True(await _job.RunCompleted.WaitAsync(TimeSpan.FromSeconds(5)));
                await Task.Delay(200);
            }

            Assert.Equal(firstRun.AddDays(60), host.NextRunUtc);
            Assert.False(await AdvanceAndRunAsync(TimeSpan.FromDays(20)), "ran 40 days early");
            Assert.False(await AdvanceAndRunAsync(TimeSpan.FromDays(20)), "ran 20 days early");
            Assert.True(await AdvanceAndRunAsync(TimeSpan.FromDays(20), 2000), "didn't run when the replaced schedule was due");
            Assert.Equal(3 + 1, _job.Runs);
            Assert.Equal(_time.GetUtcNow().UtcDateTime.AddDays(20), host.NextRunUtc);
            AssertLoopHealthy(host);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Replace_triggers_while_paused_do_not_push_the_loop_past_a_timer()
    {
        // Regression: a paused (or blocked) triggered tick didn't move the kept schedule although
        // Umbraco's loop applied Replace anyway, so later Replace triggers were handed to the loop
        // as is and pushed its wait past what a timer can wait (3 x 20 days here).
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromDays(20)));
        using var host = CreateHost();
        host.SyncWithSettings();
        await StartAndRunOnceAsync(host);
        var firstRun = _time.GetUtcNow().UtcDateTime;
        try
        {
            _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, isPaused: true, period: TimeSpan.FromDays(20)));
            _time.Advance(TimeSpan.FromDays(17));
            for (var trigger = 0; trigger < 3; trigger++)
            {
                Assert.True(host.Trigger(NextExecutionStrategy.Replace));
                await Task.Delay(200);
            }

            _time.Advance(TimeSpan.FromDays(1)); // ends the last paused tick's back-off
            await Task.Delay(300);

            AssertLoopHealthy(host);
            Assert.Equal(1, _job.Runs);
            Assert.Equal(firstRun.AddDays(80), host.NextRunUtc);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task A_next_delay_beyond_a_timer_wait_runs_on_time_not_early()
    {
        // Regression: the delay was capped at 24 days, so the job ran 6 days early.
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromDays(4)));
        using var host = CreateHost();
        host.SyncWithSettings();
        await StartAndRunOnceAsync(host);
        try
        {
            Assert.True(host.Trigger(TimeSpan.FromDays(30)));
            Assert.True(await _job.RunCompleted.WaitAsync(TimeSpan.FromSeconds(5)));
            await Task.Delay(200);
            Assert.Equal(_time.GetUtcNow().UtcDateTime.AddDays(30), host.NextRunUtc);

            // The loop steps 22 + 4 + 4 days; only the last tick runs the job.
            Assert.False(await AdvanceAndRunAsync(TimeSpan.FromDays(22)));
            Assert.False(await AdvanceAndRunAsync(TimeSpan.FromDays(4)));
            Assert.True(await AdvanceAndRunAsync(TimeSpan.FromDays(4), 2000));
            Assert.Equal(3, _job.Runs);
            Assert.Equal(ScheduledTaskTrigger.Schedule, _runLog.GetRuns(TaskId)[0].Trigger);
            AssertLoopHealthy(host);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task A_short_next_delay_on_the_heartbeat_runs_after_that_delay()
    {
        // Regression: the loop got the hourly heartbeat instead, so a 5-minute delay took an hour.
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromDays(60)));
        using var host = CreateHost();
        host.SyncWithSettings();
        await StartAndRunOnceAsync(host);
        try
        {
            Assert.True(host.Trigger(TimeSpan.FromMinutes(5)));
            Assert.True(await _job.RunCompleted.WaitAsync(TimeSpan.FromSeconds(5)));
            await Task.Delay(200);
            Assert.Equal(_time.GetUtcNow().UtcDateTime.AddMinutes(5), host.NextRunUtc);

            Assert.True(await AdvanceAndRunAsync(TimeSpan.FromMinutes(5), 2000));
            Assert.Equal(_time.GetUtcNow().UtcDateTime.AddDays(60), host.NextRunUtc);
            AssertLoopHealthy(host);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task An_infinite_next_delay_on_the_heartbeat_leaves_only_triggers_until_the_interval_changes()
    {
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromDays(60)));
        using var host = CreateHost();
        await TickAsync(host);
        var firstRun = _time.GetUtcNow().UtcDateTime;

        _time.Advance(TimeSpan.FromHours(1));
        host.Trigger(Timeout.InfiniteTimeSpan);
        await TickAsync(host);
        Assert.Equal(2, _job.Runs);
        Assert.Null(host.NextRunUtc);

        for (var hour = 0; hour < 61 * 24; hour++)
        {
            _time.Advance(TimeSpan.FromHours(1));
            await TickAsync(host);
        }

        Assert.Equal(2, _job.Runs);

        // A real change of the interval resumes the schedule.
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromDays(30)));
        host.SyncWithSettings();
        Assert.Equal(firstRun.AddDays(30), host.NextRunUtc);
        _time.Advance(TimeSpan.FromHours(1));
        await TickAsync(host);
        Assert.Equal(3, _job.Runs);
    }

    [Fact]
    public async Task An_infinite_next_delay_on_the_heartbeat_keeps_the_loop_ticking_for_a_period_change()
    {
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromDays(60)));
        using var host = CreateHost();
        host.SyncWithSettings();
        await StartAndRunOnceAsync(host);
        try
        {
            Assert.True(host.Trigger(Timeout.InfiniteTimeSpan));
            Assert.True(await _job.RunCompleted.WaitAsync(TimeSpan.FromSeconds(5)));
            await Task.Delay(200);
            Assert.False(await AdvanceAndRunAsync(TimeSpan.FromDays(20)));

            // Now 5 minutes: the loop must notice even though it never left the heartbeat for long.
            _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromMinutes(5)));
            host.SyncWithSettings();
            await Task.Delay(200);
            Assert.True(await AdvanceAndRunAsync(TimeSpan.FromMinutes(5), 2000), "the new interval didn't resume the schedule");
            AssertLoopHealthy(host);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task An_infinite_next_delay_on_a_normal_interval_is_umbracos_manual_mode()
    {
        using var host = CreateHost();
        await StartAndRunOnceAsync(host);
        try
        {
            Assert.True(host.Trigger(Timeout.InfiniteTimeSpan));
            Assert.True(await _job.RunCompleted.WaitAsync(TimeSpan.FromSeconds(5)));
            await Task.Delay(200);

            Assert.Null(host.NextRunUtc);
            Assert.False(await AdvanceAndRunAsync(TimeSpan.FromDays(1)));

            // A Reset trigger resumes the schedule after its run.
            Assert.True(host.Trigger(NextExecutionStrategy.Reset));
            Assert.True(await _job.RunCompleted.WaitAsync(TimeSpan.FromSeconds(5)));
            await Task.Delay(200);
            Assert.Equal(_time.GetUtcNow().UtcDateTime + _job.Period, host.NextRunUtc);
            Assert.True(await AdvanceAndRunAsync(_job.Period, 2000));
            AssertLoopHealthy(host);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task A_negative_next_delay_is_rejected_like_umbraco_does()
    {
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromDays(60)));
        using var host = CreateHost();

        Assert.Throws<ArgumentOutOfRangeException>(() => host.Trigger(TimeSpan.FromMinutes(-5)));
        await TickAsync(host);

        // Nothing was recorded: the tick is an ordinary scheduled one.
        Assert.Equal(ScheduledTaskTrigger.Schedule, Assert.Single(_runLog.GetRuns(TaskId)).Trigger);
    }

    [Fact]
    public async Task Run_now_on_an_overdue_long_interval_task_takes_the_overdue_slot()
    {
        // Regression: Run now kept the overdue slot, so resuming ran the task again within the hour.
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromDays(60)));
        using var host = CreateHost();
        await TickAsync(host);

        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, isPaused: true, period: TimeSpan.FromDays(60)));
        _time.Advance(TimeSpan.FromDays(61));
        await TickAsync(host); // paused past the due date
        Assert.Equal(1, _job.Runs);

        host.RequestRun("Carl");
        await TickAsync(host);
        var manualRun = _time.GetUtcNow().UtcDateTime;
        Assert.Equal(2, _job.Runs);
        Assert.Equal(manualRun.AddDays(60), host.NextRunUtc);

        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromDays(60)));
        _time.Advance(TimeSpan.FromHours(1));
        await TickAsync(host);

        Assert.Equal(2, _job.Runs);
    }

    [Fact]
    public async Task Run_now_before_a_long_interval_is_due_keeps_the_schedule()
    {
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromDays(60)));
        using var host = CreateHost();
        await TickAsync(host);
        var firstRun = _time.GetUtcNow().UtcDateTime;

        _time.Advance(TimeSpan.FromDays(10));
        host.RequestRun("Carl");
        await TickAsync(host);

        Assert.Equal(firstRun.AddDays(60), host.NextRunUtc);
    }

    [Theory]
    [InlineData("20.00:00:00", "20.00:00:00", "20.00:00:00")] // a timer can wait it: as is
    [InlineData("30.00:00:00", "01:00:00", "24.00:00:00")]
    [InlineData("61.00:00:00", "20.00:00:00", "21.00:00:00")] // 61 - 2 x 20
    [InlineData("3650.00:00:00", "4.00:00:00", "22.00:00:00")]
    public void A_wait_beyond_a_timer_is_stepped_to_land_on_the_next_run(string untilNextRun, string timerPeriod, string expectedFirstWait)
    {
        var untilTarget = TimeSpan.Parse(untilNextRun);
        var period = TimeSpan.Parse(timerPeriod);

        var first = ManagedRecurringBackgroundJobHostedService<ScriptedJob>.LoopDelayUntil(untilTarget, period);

        Assert.Equal(TimeSpan.Parse(expectedFirstWait), first);
        Assert.Equal(0, (untilTarget - first).Ticks % period.Ticks);
    }

    [Fact]
    public async Task A_faulted_timer_loop_is_logged_and_restarted()
    {
        using var host = new FaultingHost(
            _runtimeState, _logger, _mainDom, _serverRole, _events, _eventMessages, _job, _time,
            _settings, _runLog, TestSupport.Options(), TestSupport.Calendars, _registry);
        await StartAndRunOnceAsync(host);
        try
        {
            // Hands Umbraco's loop a wait no timer can wait: it runs once more, then faults.
            host.ForceOverlongWait();
            Assert.True(await _job.RunCompleted.WaitAsync(TimeSpan.FromSeconds(5)));
            for (var attempt = 0; attempt < 50 && _logger.Errors == 0; attempt++) await Task.Delay(20);
            Assert.Equal(1, _logger.Errors);
            Assert.False(host.ExecuteTask!.IsCompleted, "the loop wasn't restarted");

            // Restarted after the back-off and the job's Delay, without an extra run off schedule...
            Assert.False(await AdvanceAndRunAsync(ManagedRecurringBackgroundJobHostedService<ScriptedJob>.LoopRestartBackOff));
            Assert.False(await AdvanceAndRunAsync(_job.DelayOverride));
            Assert.Equal(2, _job.Runs);

            // ...and the schedule carries on.
            Assert.True(await AdvanceAndRunAsync(_job.Period, 2000), "the restarted loop didn't run the job");
            Assert.Equal(1, _logger.Errors);
            Assert.False(host.ExecuteTask.IsCompleted);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    private sealed class FaultingHost(
        IRuntimeState runtimeState,
        Microsoft.Extensions.Logging.ILogger<RecurringBackgroundJobHostedService<ScriptedJob>> logger,
        IMainDom mainDom,
        IServerRoleAccessor serverRoleAccessor,
        IEventAggregator eventAggregator,
        IEventMessagesFactory eventMessagesFactory,
        ScriptedJob job,
        TimeProvider timeProvider,
        IScheduledTaskSettingsStore settings,
        IScheduledTaskRunLog runLog,
        Microsoft.Extensions.Options.IOptionsMonitor<ScheduledTasksOptions> options,
        ScheduledTaskCalendarEvaluator calendarEvaluator,
        RecurringJobHostRegistry registry)
        : ManagedRecurringBackgroundJobHostedService<ScriptedJob>(
            runtimeState, logger, mainDom, serverRoleAccessor, eventAggregator, eventMessagesFactory, job,
            timeProvider, settings, runLog, options, calendarEvaluator, registry)
    {
        public void ForceOverlongWait() => TriggerExecution(TimeSpan.FromDays(60));
    }
}

/// <summary>Counts errors logged by the host (the loop restart logs one per fault).</summary>
internal sealed class ErrorCountingLogger : Microsoft.Extensions.Logging.ILogger<RecurringBackgroundJobHostedService<ScriptedJob>>
{
    private int _errors;

    public int Errors => Volatile.Read(ref _errors);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

    public void Log<TState>(
        Microsoft.Extensions.Logging.LogLevel logLevel,
        Microsoft.Extensions.Logging.EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel >= Microsoft.Extensions.Logging.LogLevel.Error) Interlocked.Increment(ref _errors);
    }
}
