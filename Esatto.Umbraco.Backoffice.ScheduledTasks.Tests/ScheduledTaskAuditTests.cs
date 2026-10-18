using System.Text.Json.Nodes;
using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Infrastructure.Models;
using Umbraco.Cms.Infrastructure.Services;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

public sealed class FeedProperties
{
    public string? Feed { get; set; }

    public int Timeout { get; set; } = 30;
}

public sealed class FeedJob : RecurringBackgroundJobBase, IScheduledTaskWithProperties<FeedProperties>
{
    public FeedJob() : base(TimeSpan.FromHours(1)) { }

    public override Task RunJobAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// A <see cref="ScheduledTaskService"/> over a recurring <see cref="ScriptedJob"/> (with a host), a
/// <see cref="RetryingJob"/>, a <see cref="FeedJob"/> with properties and a distributed job, with
/// settings kept in <see cref="Stored"/>, a change log and run store to inspect.
/// </summary>
internal sealed class ServiceHarness
{
    public const string DistributedName = "NightlyExport";

    public static readonly string RecurringId = ScheduledTaskIdentity.ForRecurring(typeof(ScriptedJob));
    public static readonly string RetryingId = ScheduledTaskIdentity.ForRecurring(typeof(RetryingJob));
    public static readonly string FeedId = ScheduledTaskIdentity.ForRecurring(typeof(FeedJob));
    public static readonly string DistributedId = ScheduledTaskIdentity.ForDistributed(DistributedName);

    public ServiceHarness()
    {
        RunLog = new ScheduledTaskRunLog(Time, TestSupport.Options());
        Control = new ScheduledTaskRunControl(RunLog, Time);
        Settings.Get(Arg.Any<string>()).Returns(c => Stored.GetValueOrDefault(c.Arg<string>()));
        Settings.GetAll().Returns(_ => Stored);
        Table.GetAll().Returns(new Dictionary<string, DistributedBackgroundJobModel>());
        Table.PollPeriod.Returns(TimeSpan.FromSeconds(5));
        ServerRole.CurrentServerRole.Returns(global::Umbraco.Cms.Core.Sync.ServerRole.Single);
        Host.TaskId.Returns(RecurringId);
        Host.EffectivePeriod.Returns(TimeSpan.FromMinutes(5));
        Registry.Register(Host);
    }

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 18, 8, 0, 0, TimeSpan.Zero));

    public Dictionary<string, ScheduledTaskSetting> Stored { get; } = new(StringComparer.Ordinal);

    public IScheduledTaskSettingsStore Settings { get; } = Substitute.For<IScheduledTaskSettingsStore>();

    public ScheduledTaskRunLog RunLog { get; }

    public ScheduledTaskRunControl Control { get; }

    public IScheduledTaskAuditLog Audit { get; } = Substitute.For<IScheduledTaskAuditLog>();

    public IScheduledTaskRunStore RunStore { get; } = Substitute.For<IScheduledTaskRunStore>();

    public IDistributedJobTable Table { get; } = Substitute.For<IDistributedJobTable>();

    public IServerRoleAccessor ServerRole { get; } = Substitute.For<IServerRoleAccessor>();

    public IManagedRecurringJobHost Host { get; } = Substitute.For<IManagedRecurringJobHost>();

    public RecurringJobHostRegistry Registry { get; } = new();

    public ScriptedDistributedJob Distributed { get; } = new(DistributedName, TimeSpan.FromHours(1));

    public ScheduledTaskService Create(bool withAuditLog = true) => new(
        [new ScriptedJob(TimeSpan.FromMinutes(5)), new RetryingJob(), new FeedJob()],
        [Distributed],
        Registry,
        Substitute.For<IDistributedJobService>(),
        Table,
        Settings,
        RunLog,
        ServerRole,
        Substitute.For<IHostApplicationLifetime>(),
        new ManualDistributedRunTracker(NullLogger<ManualDistributedRunTracker>.Instance),
        new ScheduledTaskPropertyValues(NullLogger<ScheduledTaskPropertyValues>.Instance),
        TestSupport.Calendars,
        TestSupport.Options(),
        NullLogger<ScheduledTaskService>.Instance,
        withAuditLog ? Audit : null,
        Control,
        RunStore);

    public void Store(string taskId, Func<ScheduledTaskSetting, ScheduledTaskSetting>? change = null)
    {
        var setting = TestSupport.Setting(taskId);
        Stored[taskId] = change is null ? setting : change(setting);
    }
}

public class ScheduledTaskAuditTests
{
    private readonly ServiceHarness _harness = new();

    private IScheduledTaskAuditLog Audit => _harness.Audit;

    [Fact]
    public void Pausing_and_resuming_are_recorded()
    {
        var service = _harness.Create();

        service.SetPaused(ServiceHarness.RecurringId, true, "Carl");
        _harness.Store(ServiceHarness.RecurringId, s => s with { IsPaused = true });
        service.SetPaused(ServiceHarness.RecurringId, false, "Anna");

        Audit.Received(1).Write(ServiceHarness.RecurringId, ScheduledTaskAuditActions.Paused, "Carl", null);
        Audit.Received(1).Write(ServiceHarness.RecurringId, ScheduledTaskAuditActions.Resumed, "Anna", null);
    }

    [Fact]
    public void Pausing_a_task_that_is_already_paused_is_not_recorded_again()
    {
        _harness.Store(ServiceHarness.RecurringId, s => s with { IsPaused = true });

        _harness.Create().SetPaused(ServiceHarness.RecurringId, true, "Carl");

        Audit.DidNotReceiveWithAnyArgs().Write(default!, default!, default);
    }

    [Fact]
    public void A_new_interval_is_recorded_with_before_and_after()
    {
        _harness.Create().SetPeriod(ServiceHarness.RecurringId, TimeSpan.FromMinutes(10), "Carl");

        Audit.Received(1).Write(
            ServiceHarness.RecurringId, ScheduledTaskAuditActions.IntervalChanged, "Carl", "Every 5 minutes → Every 10 minutes");
    }

    [Fact]
    public void Going_back_to_the_tasks_own_interval_is_recorded_as_a_reset()
    {
        _harness.Store(ServiceHarness.RecurringId, s => s with { PeriodOverride = TimeSpan.FromHours(2) });

        _harness.Create().SetPeriod(ServiceHarness.RecurringId, null, "Carl");

        Audit.Received(1).Write(
            ServiceHarness.RecurringId, ScheduledTaskAuditActions.ScheduleReset, "Carl", "Every 2 hours → Every 5 minutes");
    }

    [Fact]
    public void A_calendar_schedule_is_recorded_in_words()
    {
        _harness.Create().SetSchedule(ServiceHarness.RecurringId, Calendars.MondaysAtSeven(), "Carl");

        Audit.Received(1).Write(
            ServiceHarness.RecurringId,
            ScheduledTaskAuditActions.ScheduleChanged,
            "Carl",
            "Every 5 minutes → Every Monday at 07:00 (Europe/Stockholm)");
    }

    [Fact]
    public void Saved_properties_are_recorded_by_the_aliases_that_changed()
    {
        _harness.Create().SetProperties(
            ServiceHarness.FeedId, new Dictionary<string, JsonNode?> { ["feed"] = "https://example.com/feed", ["timeout"] = 30 }, "Carl");

        Audit.Received(1).Write(ServiceHarness.FeedId, ScheduledTaskAuditActions.PropertiesChanged, "Carl", "feed");
    }

    [Fact]
    public void Resetting_properties_is_recorded()
    {
        _harness.Create().SetProperties(ServiceHarness.FeedId, null, "Carl");

        Audit.Received(1).Write(ServiceHarness.FeedId, ScheduledTaskAuditActions.PropertiesReset, "Carl", null);
    }

    [Fact]
    public void A_run_started_from_the_dashboard_is_recorded_but_a_refused_one_is_not()
    {
        var service = _harness.Create();

        _harness.Host.RequestRun("Carl").Returns(false);
        service.RunNow(ServiceHarness.RecurringId, "Carl");
        Audit.DidNotReceiveWithAnyArgs().Write(default!, default!, default);

        _harness.Host.RequestRun("Carl").Returns(true);
        service.RunNow(ServiceHarness.RecurringId, "Carl");
        Audit.Received(1).Write(ServiceHarness.RecurringId, ScheduledTaskAuditActions.RunStarted, "Carl", null);
    }

    [Fact]
    public void Clearing_the_history_is_recorded()
    {
        _harness.Create().ClearRuns(ServiceHarness.RecurringId, "Carl");

        Audit.Received(1).Write(ServiceHarness.RecurringId, ScheduledTaskAuditActions.HistoryCleared, "Carl", null);
    }

    [Fact]
    public void Only_a_reset_that_happened_is_recorded()
    {
        var service = _harness.Create();

        _harness.Table.ResetRunning(ServiceHarness.DistributedName, null).Returns(DistributedResetResult.NotRunning);
        service.ResetRun(ServiceHarness.DistributedId, null, "Carl");
        Audit.DidNotReceiveWithAnyArgs().Write(default!, default!, default);

        _harness.Table.ResetRunning(ServiceHarness.DistributedName, null).Returns(DistributedResetResult.Reset);
        service.ResetRun(ServiceHarness.DistributedId, null, "Carl");
        Audit.Received(1).Write(ServiceHarness.DistributedId, ScheduledTaskAuditActions.RunReset, "Carl", null);
    }

    [Fact]
    public void A_new_run_policy_is_recorded_with_before_and_after()
    {
        _harness.Create().SetRunPolicy(
            ServiceHarness.RecurringId, new ScheduledTaskRunPolicy(3, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10)), "Carl");

        Audit.Received(1).Write(
            ServiceHarness.RecurringId,
            ScheduledTaskAuditActions.RunPolicyChanged,
            "Carl",
            "no retries · no time limit → 3 retries, 2 minutes apart · time limit 10 minutes");
    }

    [Fact]
    public void The_change_log_of_a_task_is_read_from_the_audit_log()
    {
        var entry = new ScheduledTaskAuditEntry(ServiceHarness.RecurringId, DateTime.UtcNow, "Carl", ScheduledTaskAuditActions.Paused, null, "WEB1");
        Audit.Get(ServiceHarness.RecurringId).Returns([entry]);

        Assert.Equal([entry], _harness.Create().GetChanges(ServiceHarness.RecurringId)!);
    }

    [Fact]
    public void An_unknown_task_has_no_change_log()
        => Assert.Null(_harness.Create().GetChanges("recurring:Nope"));

    [Fact]
    public void A_change_log_that_cannot_be_read_is_empty()
    {
        Audit.Get(ServiceHarness.RecurringId).Throws(new InvalidOperationException("database unreachable"));

        Assert.Empty(_harness.Create().GetChanges(ServiceHarness.RecurringId)!);
    }

    [Fact]
    public void Without_an_audit_log_changes_still_work_and_the_change_log_is_empty()
    {
        var service = _harness.Create(withAuditLog: false);

        Assert.Equal(ScheduledTaskCommandStatus.Ok, service.SetPaused(ServiceHarness.RecurringId, true, "Carl").Status);
        Assert.Empty(service.GetChanges(ServiceHarness.RecurringId)!);
    }
}

public class ScheduledTaskDescriptionsTests
{
    private static ScheduledTaskCalendar Calendar(ScheduledTaskCalendarKind kind) => new()
    {
        Kind = kind,
        Time = new TimeOnly(6, 30),
        TimeZone = "UTC",
    };

    [Fact]
    public void An_interval_is_described_by_its_period()
    {
        Assert.Equal("Every 2 hours", ScheduledTaskDescriptions.Schedule(null, TimeSpan.FromHours(2)));
        Assert.Equal("On demand only", ScheduledTaskDescriptions.Schedule(null, Timeout.InfiniteTimeSpan));
    }

    [Fact]
    public void A_calendar_takes_precedence_over_the_interval()
        => Assert.Equal(
            "Every Monday at 07:00 (Europe/Stockholm)",
            ScheduledTaskDescriptions.Schedule(Calendars.MondaysAtSeven(), TimeSpan.FromHours(2)));

    [Fact]
    public void Weekdays_are_listed_from_Monday_once_each()
        => Assert.Equal(
            "Every Monday, Thursday and Sunday at 06:30 (UTC)",
            ScheduledTaskDescriptions.Calendar(Calendar(ScheduledTaskCalendarKind.Weekly) with
            {
                Weekdays = [DayOfWeek.Sunday, DayOfWeek.Thursday, DayOfWeek.Monday, DayOfWeek.Thursday],
            }));

    [Fact]
    public void Each_kind_of_calendar_is_described()
    {
        Assert.Equal("Every day at 06:30 (UTC)", ScheduledTaskDescriptions.Calendar(Calendar(ScheduledTaskCalendarKind.Daily)));
        Assert.Equal(
            "Every month on day 15 at 06:30 (UTC)",
            ScheduledTaskDescriptions.Calendar(Calendar(ScheduledTaskCalendarKind.Monthly) with { DayOfMonth = 15 }));
        Assert.Equal(
            "Every year on 24 December at 06:30 (UTC)",
            ScheduledTaskDescriptions.Calendar(Calendar(ScheduledTaskCalendarKind.Yearly) with { DayOfMonth = 24, Month = 12 }));
        Assert.Equal(
            "Cron */5 * * * * (UTC)",
            ScheduledTaskDescriptions.Calendar(Calendar(ScheduledTaskCalendarKind.Cron) with { Cron = "*/5 * * * *" }));
    }

    [Fact]
    public void A_change_to_the_same_description_is_just_the_description()
    {
        Assert.Equal("Every hour", ScheduledTaskDescriptions.Change("Every hour", "Every hour"));
        Assert.Equal("Every hour → Every day", ScheduledTaskDescriptions.Change("Every hour", "Every day"));
    }

    [Theory]
    [InlineData(null, """{"feed":"a","timeout":5}""", "feed, timeout")]
    [InlineData("""{"feed":"a","timeout":5}""", """{"timeout":5,"feed":"b"}""", "feed")]
    [InlineData("""{"feed":"a"}""", null, "feed")]
    [InlineData("""{"feed":"a"}""", """{"feed":"a"}""", "No values changed")]
    [InlineData("not json", "[1]", "No values changed")]
    public void Changed_properties_are_listed_by_alias(string? before, string? after, string expected)
        => Assert.Equal(expected, ScheduledTaskDescriptions.ChangedProperties(before, after));
}
