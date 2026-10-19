using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Infrastructure.Models;
using Umbraco.Cms.Infrastructure.Services;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

public class ScheduledTaskServiceTests
{
    private static readonly string RecurringId = ScheduledTaskIdentity.ForRecurring(typeof(ScriptedJob));
    private const string DistributedName = "NightlyExport";
    private static readonly string DistributedId = ScheduledTaskIdentity.ForDistributed(DistributedName);

    private readonly ScriptedJob _recurring = new(TimeSpan.FromMinutes(5));
    private readonly ScriptedDistributedJob _distributed = new(DistributedName, TimeSpan.FromHours(1));
    private readonly RecurringJobHostRegistry _registry = new();
    private readonly IDistributedJobService _distributedJobService = Substitute.For<IDistributedJobService>();
    private readonly IDistributedJobTable _table = Substitute.For<IDistributedJobTable>();
    private readonly IScheduledTaskSettingsStore _settings = Substitute.For<IScheduledTaskSettingsStore>();
    private readonly ScheduledTaskRunLog _runLog = new(new FakeTimeProvider(), TestSupport.Options());
    private readonly IServerRoleAccessor _serverRole = Substitute.For<IServerRoleAccessor>();
    private readonly IManagedRecurringJobHost _host = Substitute.For<IManagedRecurringJobHost>();

    public ScheduledTaskServiceTests()
    {
        _serverRole.CurrentServerRole.Returns(ServerRole.Single);
        _settings.GetAll().Returns(new Dictionary<string, ScheduledTaskSetting>());
        _table.GetAll().Returns(new Dictionary<string, DistributedBackgroundJobModel>());
        _host.TaskId.Returns(RecurringId);
        _host.EffectivePeriod.Returns(_recurring.Period);
        _registry.Register(_host);
    }

    private readonly IHostApplicationLifetime _lifetime = Substitute.For<IHostApplicationLifetime>();

    private ScheduledTaskService CreateService(
        ScheduledTasksOptions? options = null,
        IDistributedBackgroundJob? distributed = null,
        IDistributedJobService? distributedJobService = null) => new(
        [_recurring],
        [distributed ?? _distributed],
        _registry,
        distributedJobService ?? _distributedJobService,
        _table,
        _settings,
        _runLog,
        _serverRole,
        _lifetime,
        new ManualDistributedRunTracker(NullLogger<ManualDistributedRunTracker>.Instance),
        new ScheduledTaskPropertyValues(NullLogger<ScheduledTaskPropertyValues>.Instance),
        TestSupport.Calendars,
        TestSupport.Options(options),
        NullLogger<ScheduledTaskService>.Instance);

    [Fact]
    public void Lists_recurring_and_distributed_tasks()
    {
        var tasks = CreateService().GetAll();

        Assert.Collection(
            tasks,
            t =>
            {
                Assert.Equal(DistributedId, t.Id);
                Assert.Equal("Nightly export", t.Name);
                Assert.Equal(ScheduledTaskKind.Distributed, t.Kind);
            },
            t =>
            {
                Assert.Equal(RecurringId, t.Id);
                Assert.Equal("Scripted", t.Name);
                Assert.Equal(ScheduledTaskKind.Recurring, t.Kind);
                Assert.True(t.IsManaged);
                Assert.True(t.RunsOnThisServer);
                Assert.False(t.IsUmbraco);
            });
    }

    [Fact]
    public void Run_now_asks_the_recurring_host()
    {
        _host.RequestRun("Carl").Returns(true);

        var result = CreateService().RunNow(RecurringId, "Carl");

        Assert.Equal(ScheduledTaskCommandStatus.Ok, result.Status);
        _host.Received(1).RequestRun("Carl");
    }

    [Fact]
    public void Run_now_is_refused_on_a_server_role_the_job_does_not_run_on()
    {
        _serverRole.CurrentServerRole.Returns(ServerRole.Subscriber);

        var result = CreateService().RunNow(RecurringId, "Carl");

        Assert.Equal(ScheduledTaskCommandStatus.Conflict, result.Status);
        _host.DidNotReceive().RequestRun(Arg.Any<string?>());
    }

    [Fact]
    public void Run_now_reports_a_recurring_job_that_is_already_running()
    {
        _host.RequestRun(Arg.Any<string?>()).Returns(false);

        Assert.Equal(ScheduledTaskCommandStatus.Conflict, CreateService().RunNow(RecurringId, null).Status);
    }

    [Theory]
    [InlineData(nameof(DistributedTakeResult.NotRegistered))]
    [InlineData(nameof(DistributedTakeResult.AlreadyRunning))]
    public void Run_now_reports_a_distributed_job_it_could_not_take(string takeResult)
    {
        _table.TryTake(DistributedName).Returns(Enum.Parse<DistributedTakeResult>(takeResult));

        Assert.Equal(ScheduledTaskCommandStatus.Conflict, CreateService().RunNow(DistributedId, null).Status);
    }

    [Fact]
    public async Task Run_now_runs_a_taken_distributed_job_and_finishes_it()
    {
        _table.TryTake(DistributedName).Returns(DistributedTakeResult.Taken);
        var finished = new TaskCompletionSource();
        _distributedJobService.FinishAsync(DistributedName).Returns(_ =>
        {
            finished.SetResult();
            return Task.CompletedTask;
        });

        var result = CreateService().RunNow(DistributedId, "Carl");
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(ScheduledTaskCommandStatus.Ok, result.Status);
        Assert.Equal(1, _distributed.Runs);
    }

    [Fact]
    public void A_distributed_task_flagged_as_running_too_long_is_reported_as_possibly_stuck()
    {
        var attempted = DateTime.UtcNow.AddMinutes(-20);
        var row = new DistributedBackgroundJobModel
        {
            Name = DistributedName,
            Period = TimeSpan.FromHours(1),
            IsRunning = true,
            LastAttemptedRun = attempted,
            LastRun = attempted.AddHours(-1),
        };
        _table.GetAll().Returns(new Dictionary<string, DistributedBackgroundJobModel> { [DistributedName] = row });
        _table.IsRunning(row, Arg.Any<DateTime>()).Returns(true);
        _table.IsPossiblyStuck(row, Arg.Any<DateTime>()).Returns(true);

        var task = CreateService().GetAll().Single(t => t.Id == DistributedId);

        Assert.True(task.IsRunning);
        Assert.True(task.IsPossiblyStuck);
        Assert.Equal(attempted, task.RunningSinceUtc);
    }

    [Fact]
    public void A_stuck_distributed_task_can_be_reset()
    {
        _table.ResetRunning(DistributedName).Returns(DistributedResetResult.Reset);

        var result = CreateService().ResetRun(DistributedId);

        Assert.Equal(ScheduledTaskCommandStatus.Ok, result.Status);
        _table.Received(1).ResetRunning(DistributedName);
    }

    [Fact]
    public void Reset_is_refused_while_the_task_runs_on_this_server()
    {
        _runLog.Start(DistributedId, ScheduledTaskTrigger.Schedule, null);

        Assert.Equal(ScheduledTaskCommandStatus.Conflict, CreateService().ResetRun(DistributedId).Status);
        _table.DidNotReceiveWithAnyArgs().ResetRunning(default!, default);
    }

    [Fact]
    public void Reset_only_applies_to_distributed_tasks()
    {
        var service = CreateService();

        Assert.Equal(ScheduledTaskCommandStatus.Invalid, service.ResetRun(RecurringId).Status);
        Assert.Equal(ScheduledTaskCommandStatus.NotFound, service.ResetRun("distributed:Nope").Status);
    }

    [Fact]
    public void Unknown_ids_are_not_found()
    {
        var service = CreateService();

        Assert.Equal(ScheduledTaskCommandStatus.NotFound, service.RunNow("recurring:Nope", null).Status);
        Assert.Equal(ScheduledTaskCommandStatus.NotFound, service.SetPaused("distributed:Nope", true, null).Status);
        Assert.Equal(ScheduledTaskCommandStatus.NotFound, service.SetPeriod("nope", TimeSpan.FromMinutes(1), null).Status);
        Assert.Null(service.GetRuns("nope"));
    }

    private static readonly ScheduledTaskSettingState Customised = new(true, TimeSpan.FromMinutes(1), """{"olderThanDays":30}""");

    [Fact]
    public void Pausing_and_resuming_change_only_the_pause()
    {
        CreateService().SetPaused(RecurringId, false, "Carl");

        Assert.Equal(Customised with { IsPaused = false }, _settings.ChangeSavedFor(RecurringId)(Customised));
    }

    [Fact]
    public void A_new_recurring_interval_is_saved_and_applied_to_the_host()
    {
        var result = CreateService().SetPeriod(RecurringId, TimeSpan.FromMinutes(2), "Carl");

        Assert.Equal(ScheduledTaskCommandStatus.Ok, result.Status);
        Assert.Equal(Customised with { PeriodOverride = TimeSpan.FromMinutes(2) }, _settings.ChangeSavedFor(RecurringId)(Customised));
        _host.Received(1).SyncWithSettings();
    }

    [Fact]
    public void Setting_the_default_interval_clears_the_override()
    {
        CreateService().SetPeriod(RecurringId, _recurring.Period, "Carl");

        Assert.Equal(Customised with { PeriodOverride = null }, _settings.ChangeSavedFor(RecurringId)(Customised));
    }

    [Fact]
    public void A_new_distributed_interval_is_written_to_the_job_table()
    {
        CreateService().SetPeriod(DistributedId, TimeSpan.FromMinutes(10), "Carl");

        _table.Received(1).SetPeriods(Arg.Is<IReadOnlyDictionary<string, TimeSpan>>(p => p[DistributedName] == TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public void Resetting_a_distributed_interval_restores_the_job_period_in_the_table()
    {
        CreateService().SetPeriod(DistributedId, null, "Carl");

        _table.Received(1).SetPeriods(Arg.Is<IReadOnlyDictionary<string, TimeSpan>>(p => p[DistributedName] == TimeSpan.FromHours(1)));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(11 * 365 * 24 * 3600)]
    public void Intervals_outside_the_allowed_range_are_rejected(double seconds)
    {
        var result = CreateService().SetPeriod(RecurringId, TimeSpan.FromSeconds(seconds), "Carl");

        Assert.Equal(ScheduledTaskCommandStatus.Invalid, result.Status);
        Assert.Equal("The interval must be between 1 second and 10 years.", result.Message);
        _settings.DidNotReceiveWithAnyArgs().Update(default!, default!, default);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(30)]
    [InlineData(730)]
    public void Week_month_and_year_long_intervals_are_accepted(int days)
    {
        var result = CreateService().SetPeriod(DistributedId, TimeSpan.FromDays(days), "Carl");

        Assert.Equal(ScheduledTaskCommandStatus.Ok, result.Status);
        Assert.Equal(TimeSpan.FromDays(days), _settings.ChangeSavedFor(DistributedId)(ScheduledTaskSettingState.Default).PeriodOverride);
    }

    [Fact]
    public void Reset_only_clears_the_run_the_dashboard_showed()
    {
        var shown = DateTime.UtcNow.AddMinutes(-7);
        _table.ResetRunning(DistributedName, shown).Returns(DistributedResetResult.Reset);

        Assert.Equal(ScheduledTaskCommandStatus.Ok, CreateService().ResetRun(DistributedId, shown).Status);
        _table.Received(1).ResetRunning(DistributedName, shown);
    }

    [Fact]
    public void Reset_of_a_run_that_was_picked_up_again_is_refused()
    {
        // Umbraco reclaimed the stale row and another server started a fresh run 12 seconds ago.
        var pickedUp = DateTime.UtcNow.AddSeconds(-12);
        _table.ResetRunning(DistributedName, Arg.Any<DateTime?>()).Returns(DistributedResetResult.NotStuck);
        _table.GetAll().Returns(new Dictionary<string, DistributedBackgroundJobModel>
        {
            [DistributedName] = new() { Name = DistributedName, Period = TimeSpan.FromMinutes(1), IsRunning = true, LastAttemptedRun = pickedUp },
        });

        var result = CreateService().ResetRun(DistributedId, DateTime.UtcNow.AddMinutes(-6));

        Assert.Equal(ScheduledTaskCommandStatus.Conflict, result.Status);
        Assert.Equal("It was picked up again 12 seconds ago; it isn't stuck.", result.Message);
    }

    [Fact]
    public void Run_now_of_a_distributed_task_is_refused_while_the_site_shuts_down()
    {
        using var stopping = new CancellationTokenSource();
        stopping.Cancel();
        _lifetime.ApplicationStopping.Returns(stopping.Token);

        Assert.Equal(ScheduledTaskCommandStatus.Conflict, CreateService().RunNow(DistributedId, "Carl").Status);
        _table.DidNotReceiveWithAnyArgs().TryTake(default!);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4.9)]
    public void Distributed_intervals_shorter_than_Umbracos_poll_are_rejected(double seconds)
    {
        _table.PollPeriod.Returns(TimeSpan.FromSeconds(5));

        var result = CreateService().SetPeriod(DistributedId, TimeSpan.FromSeconds(seconds), "Carl");

        Assert.Equal(ScheduledTaskCommandStatus.Invalid, result.Status);
        Assert.Equal("Distributed tasks can't run more often than Umbraco checks for them (every 5 seconds).", result.Message);
        _settings.DidNotReceiveWithAnyArgs().Update(default!, default!, default);
        _table.DidNotReceiveWithAnyArgs().SetPeriods(default!);
    }

    [Fact]
    public void The_poll_floor_applies_to_distributed_tasks_only()
    {
        _table.PollPeriod.Returns(TimeSpan.FromSeconds(5));
        var service = CreateService();

        Assert.Equal(ScheduledTaskCommandStatus.Ok, service.SetPeriod(DistributedId, TimeSpan.FromSeconds(5), "Carl").Status);
        Assert.Equal(ScheduledTaskCommandStatus.Ok, service.SetPeriod(RecurringId, TimeSpan.FromSeconds(2), "Carl").Status);
    }

    [Fact]
    public void A_calendar_is_refused_for_a_distributed_task_aligned_to_the_clock()
    {
        var service = CreateService(distributed: new AlignedDistributedJob(DistributedName, TimeSpan.FromMinutes(10)));

        var result = service.SetSchedule(DistributedId, Calendars.MondaysAtSeven(savedUtc: DateTime.MinValue), "Carl");

        Assert.Equal(ScheduledTaskCommandStatus.Invalid, result.Status);
        Assert.Equal("This task aligns its runs to the clock, so it can't use a calendar schedule. Use an interval instead.", result.Message);
        _settings.DidNotReceiveWithAnyArgs().Update(default!, default!, default);
    }

    [Fact]
    public void Run_now_of_a_calendar_task_takes_its_row_with_a_running_period()
    {
        // Daily 07:00 and it's 05:00: the row's period while it runs covers the two hours to the next occurrence,
        // so Umbraco doesn't take a long manual run for a stale one and start it on another server.
        _settings.Get(DistributedId).Returns(TestSupport.Setting(DistributedId, calendar: new ScheduledTaskCalendar
        {
            Kind = ScheduledTaskCalendarKind.Daily,
            Time = new TimeOnly(7, 0),
            TimeZone = "UTC",
            SavedUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        }));
        _table.TryTake(DistributedName, Arg.Any<TimeSpan?>()).Returns(DistributedTakeResult.Taken);
        var managed = new ManagedDistributedJobService(
            _distributedJobService, _table, _settings, _runLog, [_distributed], TestSupport.Calendars,
            NullLogger<ManagedDistributedJobService>.Instance, new FakeTimeProvider(new DateTimeOffset(2026, 10, 19, 5, 0, 0, TimeSpan.Zero)));

        var result = CreateService(distributedJobService: managed).RunNow(DistributedId, "Carl");

        Assert.Equal(ScheduledTaskCommandStatus.Ok, result.Status);
        _table.Received(1).TryTake(DistributedName, TimeSpan.FromHours(2));
    }

    [Fact]
    public void Tasks_say_whether_they_can_use_a_calendar_and_their_shortest_interval()
    {
        _table.PollPeriod.Returns(TimeSpan.FromSeconds(5));

        var aligned = CreateService(distributed: new AlignedDistributedJob(DistributedName, TimeSpan.FromMinutes(10))).Get(DistributedId)!.ToDto();
        Assert.Equal("This task aligns its runs to the clock, so it can't use a calendar schedule.", aligned.CalendarUnavailableReason);
        Assert.Equal(5, aligned.MinimumPeriodSeconds);

        var service = CreateService();
        Assert.Null(service.Get(DistributedId)!.ToDto().CalendarUnavailableReason);
        var recurring = service.Get(RecurringId)!.ToDto();
        Assert.Null(recurring.CalendarUnavailableReason);
        Assert.Equal(1, recurring.MinimumPeriodSeconds);
    }

    [Fact]
    public void An_aligned_tasks_next_run_is_its_next_clock_boundary()
    {
        var lastRun = new DateTime(2026, 10, 18, 7, 0, 3, DateTimeKind.Utc);
        _table.GetAll().Returns(new Dictionary<string, DistributedBackgroundJobModel>
        {
            [DistributedName] = new() { Name = DistributedName, Period = TimeSpan.FromMinutes(10), LastRun = lastRun, LastAttemptedRun = lastRun },
        });

        var task = CreateService(distributed: new AlignedDistributedJob(DistributedName, TimeSpan.FromMinutes(10))).Get(DistributedId)!;

        Assert.Equal(new DateTime(2026, 10, 18, 7, 10, 0, DateTimeKind.Utc), task.NextRunUtc);
    }

    [Theory]
    [InlineData(1, "1 second")]
    [InlineData(90, "90 seconds")]
    [InlineData(120, "2 minutes")]
    [InlineData(5400, "90 minutes")]
    [InlineData(7200, "2 hours")]
    [InlineData(86400, "1 day")]
    [InlineData(14 * 86400, "2 weeks")]
    [InlineData(30 * 86400, "1 month")]
    [InlineData(90 * 86400, "3 months")]
    [InlineData(365 * 86400, "1 year")]
    [InlineData(3650 * 86400, "10 years")]
    [InlineData(45 * 86400, "45 days")]
    public void Periods_are_described_in_their_largest_whole_unit(double seconds, string expected)
        => Assert.Equal(expected, ScheduledTaskService.DescribePeriod(TimeSpan.FromSeconds(seconds)));
}
