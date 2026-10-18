using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Infrastructure.Models;
using Umbraco.Cms.Infrastructure.Persistence.Repositories;
using Umbraco.Cms.Infrastructure.Services;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

public class DistributedJobTests
{
    private const string JobName = "NightlyExport";
    private static readonly string TaskId = ScheduledTaskIdentity.ForDistributed(JobName);

    private readonly IScheduledTaskSettingsStore _settings = Substitute.For<IScheduledTaskSettingsStore>();
    private readonly ScheduledTaskRunLog _runLog = new(new FakeTimeProvider(), TestSupport.Options());
    private readonly ScriptedDistributedJob _job = new(JobName, TimeSpan.FromHours(1));

    private TrackedDistributedJob Track(ScheduledTaskTrigger trigger, string? triggeredBy = null)
        => new(_job, trigger, triggeredBy, _settings, _runLog, NullLogger.Instance);

    [Fact]
    public async Task A_tracked_job_records_its_run()
    {
        await Track(ScheduledTaskTrigger.Schedule).ExecuteAsync(CancellationToken.None);

        Assert.Equal(1, _job.Runs);
        Assert.Equal(ScheduledTaskRunOutcome.Succeeded, Assert.Single(_runLog.GetRuns(TaskId)).Outcome);
    }

    [Fact]
    public async Task A_paused_job_is_skipped_on_schedule_but_runs_on_demand()
    {
        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, isPaused: true));

        await Track(ScheduledTaskTrigger.Schedule).ExecuteAsync(CancellationToken.None);
        Assert.Equal(0, _job.Runs);

        await Track(ScheduledTaskTrigger.Manual, "Carl").ExecuteAsync(CancellationToken.None);
        Assert.Equal(1, _job.Runs);
        Assert.Equal("Carl", Assert.Single(_runLog.GetRuns(TaskId)).TriggeredBy);
    }

    [Fact]
    public async Task A_failure_is_recorded_and_rethrown_for_Umbraco_to_log()
    {
        _job.Body = _ => throw new TimeoutException("API slow");

        await Assert.ThrowsAsync<TimeoutException>(() => Track(ScheduledTaskTrigger.Schedule).ExecuteAsync(CancellationToken.None));

        Assert.Equal("TimeoutException: API slow", Assert.Single(_runLog.GetRuns(TaskId)).Message);
    }

    [Fact]
    public async Task The_decorator_wraps_jobs_Umbraco_hands_out()
    {
        var inner = Substitute.For<IDistributedJobService>();
        inner.TryTakeRunnableAsync().Returns(_job);
        var service = CreateDecorator(inner, Substitute.For<IDistributedJobTable>());

        var taken = await service.TryTakeRunnableAsync();
        await taken!.ExecuteAsync(CancellationToken.None);

        Assert.IsType<TrackedDistributedJob>(taken);
        Assert.Equal(JobName, taken.Name);
        Assert.Single(_runLog.GetRuns(TaskId));
    }

    [Fact]
    public async Task After_Umbraco_resyncs_the_table_interval_overrides_are_written_back()
    {
        var inner = Substitute.For<IDistributedJobService>();
        var table = Substitute.For<IDistributedJobTable>();
        _settings.GetAll().Returns(new Dictionary<string, ScheduledTaskSetting>
        {
            [TaskId] = TestSupport.Setting(TaskId, period: TimeSpan.FromMinutes(10)),
            ["distributed:Unregistered"] = TestSupport.Setting("distributed:Unregistered", period: TimeSpan.FromMinutes(1)),
            ["recurring:Some.Job"] = TestSupport.Setting("recurring:Some.Job", period: TimeSpan.FromMinutes(1)),
        });
        var service = CreateDecorator(inner, table);

        await service.EnsureJobsAsync();

        await inner.Received(1).EnsureJobsAsync();
        table.Received(1).SetPeriods(Arg.Is<IReadOnlyDictionary<string, TimeSpan>>(p =>
            p.Count == 1 && p[JobName] == TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public async Task No_job_is_handed_out_before_the_settings_have_been_read()
    {
        // Otherwise a paused job runs in the window before the package's migration.
        var store = new FakeSettingsStore { IsWaitingForDatabase = true };
        var inner = Substitute.For<IDistributedJobService>();
        inner.TryTakeRunnableAsync().Returns(_job);
        var service = CreateDecorator(inner, Substitute.For<IDistributedJobTable>(), store);

        Assert.Null(await service.TryTakeRunnableAsync());
        await inner.DidNotReceive().TryTakeRunnableAsync();

        store.IsWaitingForDatabase = false;
        Assert.NotNull(await service.TryTakeRunnableAsync());
    }

    [Fact]
    public async Task Overrides_are_not_reset_by_a_startup_sync_before_the_settings_can_be_read_and_are_applied_once_they_can()
    {
        var store = new FakeSettingsStore { IsWaitingForDatabase = true };
        var table = Substitute.For<IDistributedJobTable>();
        table.GetAll().Returns(new Dictionary<string, DistributedBackgroundJobModel>());
        var service = CreateDecorator(Substitute.For<IDistributedJobService>(), table, store);

        await service.EnsureJobsAsync();
        table.DidNotReceiveWithAnyArgs().SetPeriods(default!);

        store.IsWaitingForDatabase = false;
        store.Stored[TaskId] = TestSupport.Setting(TaskId, period: TimeSpan.FromMinutes(10));
        await service.TryTakeRunnableAsync();

        table.Received(1).SetPeriods(Arg.Is<IReadOnlyDictionary<string, TimeSpan>>(p => p[JobName] == TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public async Task A_deferred_startup_sync_is_not_cleared_by_one_jobs_finish()
    {
        // A dashboard "Run now" can finish before the poller's next take; the other jobs still need theirs.
        var other = new ScriptedDistributedJob("OtherJob", TimeSpan.FromHours(1));
        var otherId = ScheduledTaskIdentity.ForDistributed(other.Name);
        var store = new FakeSettingsStore { IsWaitingForDatabase = true };
        var table = TableWithRow(_job.Period);
        var service = new ManagedDistributedJobService(
            Substitute.For<IDistributedJobService>(), table, store, _runLog, [_job, other], TestSupport.Calendars,
            NullLogger<ManagedDistributedJobService>.Instance);

        await service.EnsureJobsAsync();
        store.IsWaitingForDatabase = false;
        store.Stored[otherId] = TestSupport.Setting(otherId, period: TimeSpan.FromMinutes(10));
        await service.FinishAsync(JobName);
        await service.TryTakeRunnableAsync();

        table.Received(1).SetPeriods(Arg.Is<IReadOnlyDictionary<string, TimeSpan>>(p =>
            p.ContainsKey("OtherJob") && p["OtherJob"] == TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public async Task A_startup_sync_that_fails_is_retried_on_the_next_poll()
    {
        var store = Substitute.For<IScheduledTaskSettingsStore>();
        store.GetAll().Returns(new Dictionary<string, ScheduledTaskSetting>
        {
            [TaskId] = TestSupport.Setting(TaskId, period: TimeSpan.FromMinutes(10)),
        });
        var calls = 0;
        store.When(s => s.Refresh()).Do(_ => { if (calls++ == 0) throw new InvalidOperationException("database unreachable"); });
        var table = TableWithRow(_job.Period);
        var service = CreateDecorator(Substitute.For<IDistributedJobService>(), table, store);

        await Assert.ThrowsAsync<InvalidOperationException>(service.EnsureJobsAsync); // Umbraco logs it
        table.DidNotReceiveWithAnyArgs().SetPeriods(default!);

        await service.TryTakeRunnableAsync();
        table.Received(1).SetPeriods(Arg.Is<IReadOnlyDictionary<string, TimeSpan>>(p => p[JobName] == TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public async Task A_finish_writes_the_period_from_the_stored_setting_not_this_servers_stale_cache()
    {
        // This server still caches the calendar; an editor elsewhere has since switched to 1 hour.
        var store = new FakeSettingsStore();
        store.Cached[TaskId] = TestSupport.Setting(TaskId, calendar: Calendars.MondaysAtSeven());
        store.Stored[TaskId] = TestSupport.Setting(TaskId, period: TimeSpan.FromMinutes(90));
        var table = TableWithRow(TimeSpan.FromHours(7));
        var service = CreateDecorator(Substitute.For<IDistributedJobService>(), table, store);

        await service.FinishAsync(JobName);

        table.Received(1).SetPeriods(Arg.Is<IReadOnlyDictionary<string, TimeSpan>>(p => p[JobName] == TimeSpan.FromMinutes(90)));
    }

    [Fact]
    public async Task A_finish_corrects_a_stale_period_back_to_the_jobs_own()
    {
        // A stale server wrote a calendar period for a task that has since gone back to its defaults.
        var table = TableWithRow(TimeSpan.FromHours(7));
        var service = CreateDecorator(Substitute.For<IDistributedJobService>(), table, new FakeSettingsStore());

        await service.FinishAsync(JobName);

        table.Received(1).SetPeriods(Arg.Is<IReadOnlyDictionary<string, TimeSpan>>(p => p[JobName] == _job.Period));
    }

    [Fact]
    public async Task A_stored_interval_shorter_than_Umbracos_poll_is_raised_to_it()
    {
        var store = new FakeSettingsStore();
        store.Stored[TaskId] = TestSupport.Setting(TaskId, period: TimeSpan.FromSeconds(2));
        var table = TableWithRow(_job.Period);
        var service = CreateDecorator(Substitute.For<IDistributedJobService>(), table, store);

        await service.EnsureJobsAsync();

        table.Received(1).SetPeriods(Arg.Is<IReadOnlyDictionary<string, TimeSpan>>(p => p[JobName] == TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task A_calendar_is_not_applied_to_a_job_aligned_to_the_clock()
    {
        var aligned = new AlignedDistributedJob(JobName, TimeSpan.FromMinutes(10));
        var store = new FakeSettingsStore();
        store.Stored[TaskId] = TestSupport.Setting(TaskId, calendar: Calendars.MondaysAtSeven());
        var table = TableWithRow(TimeSpan.FromMinutes(10));
        var service = new ManagedDistributedJobService(
            Substitute.For<IDistributedJobService>(), table, store, _runLog, [aligned], TestSupport.Calendars,
            NullLogger<ManagedDistributedJobService>.Instance);

        await service.FinishAsync(JobName);

        table.Received(1).SetPeriods(Arg.Is<IReadOnlyDictionary<string, TimeSpan>>(p => p[JobName] == TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public async Task An_ignored_calendar_on_an_aligned_job_is_warned_about_once_not_after_every_run()
    {
        var aligned = new AlignedDistributedJob(JobName, TimeSpan.FromMinutes(1));
        var store = new FakeSettingsStore();
        store.Stored[TaskId] = TestSupport.Setting(TaskId, calendar: Calendars.MondaysAtSeven());
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<ManagedDistributedJobService>>();
        logger.IsEnabled(default).ReturnsForAnyArgs(true);
        var service = new ManagedDistributedJobService(
            Substitute.For<IDistributedJobService>(), TableWithRow(TimeSpan.FromMinutes(1)), store, _runLog, [aligned], TestSupport.Calendars, logger);

        for (var run = 0; run < 3; run++) await service.FinishAsync(JobName);

        Assert.Single(logger.ReceivedCalls(), c => c.GetMethodInfo().Name == "Log"
            && (Microsoft.Extensions.Logging.LogLevel)c.GetArguments()[0]! == Microsoft.Extensions.Logging.LogLevel.Warning);
    }

    [Fact]
    public void Alignment_is_read_from_the_job()
    {
        Assert.True(DistributedJobClock.IsAlignedToClock(new AlignedDistributedJob("A", TimeSpan.FromMinutes(1))));
        Assert.False(DistributedJobClock.IsAlignedToClock(_job));
    }

    [Theory]
    [InlineData("2026-10-18T07:00:03", 10, "2026-10-18T07:10:00")]
    [InlineData("2026-10-18T07:10:00", 10, "2026-10-18T07:20:00")] // a run on a boundary waits for the next one
    [InlineData("2026-10-18T07:00:03", 1440, "2026-10-19T00:00:00")]
    public void An_aligned_jobs_next_run_is_the_next_clock_boundary_after_its_last_run(string lastRun, int periodMinutes, string expected)
    {
        var row = new DistributedBackgroundJobModel { Name = JobName, Period = TimeSpan.FromMinutes(periodMinutes), LastRun = DateTime.Parse(lastRun) };

        Assert.Equal(DateTime.Parse(expected), DistributedJobClock.NextRunUtc(row, alignedToClock: true));
        Assert.Equal(DateTime.Parse(lastRun).AddMinutes(periodMinutes), DistributedJobClock.NextRunUtc(row, alignedToClock: false));
    }

    [Fact]
    public async Task A_Run_now_still_running_at_an_occurrence_leaves_it_due_and_the_poller_runs_it_after()
    {
        // Daily 07:00; an editor's Run now started 06:58 and finished 07:03, while the poller left the row alone.
        var time = At(2026, 10, 19, 7, 3, 0);
        var store = new FakeSettingsStore();
        store.Stored[TaskId] = TestSupport.Setting(TaskId, calendar: Daily(7), lastScheduledUtc: Utc(2026, 10, 18, 7, 0, 3));
        var row = Row(lastRun: Utc(2026, 10, 19, 7, 3, 0));
        var table = TableWith(row);
        var inner = Substitute.For<IDistributedJobService>();
        inner.TryTakeRunnableAsync().Returns(_job);
        var service = CreateDecorator(inner, table, store, time);

        await service.FinishAsync(JobName);
        Assert.Equal(TimeSpan.Zero, LastPeriodWritten(table)); // today's 07:00 is still due

        var taken = await service.TryTakeRunnableAsync();
        await taken!.ExecuteAsync(CancellationToken.None);
        Assert.Equal(1, _job.Runs);
        Assert.Equal(Utc(2026, 10, 19, 7, 3, 0), store.Stored[TaskId].LastScheduledUtc);

        time.Advance(TimeSpan.FromSeconds(30));
        row.LastRun = Utc(2026, 10, 19, 7, 3, 30);
        await service.FinishAsync(JobName);
        Assert.Equal(Utc(2026, 10, 20, 7, 0, 0) - row.LastRun, LastPeriodWritten(table));
    }

    [Fact]
    public async Task A_run_ending_in_the_repeated_autumn_hour_does_not_run_again_at_its_second_instance()
    {
        // Daily 02:59 Stockholm: on 25 Oct 2026 that is 00:59Z (CEST) and again 01:59Z (CET).
        var time = At(2026, 10, 25, 0, 59, 4);
        var store = new FakeSettingsStore();
        store.Stored[TaskId] = TestSupport.Setting(
            TaskId, calendar: Daily(2, 59, Calendars.Stockholm), lastScheduledUtc: Utc(2026, 10, 24, 0, 59, 3));
        var row = Row(lastRun: Utc(2026, 10, 24, 1, 0, 0));
        var table = TableWith(row);
        var inner = Substitute.For<IDistributedJobService>();
        inner.TryTakeRunnableAsync().Returns(_job);
        var service = CreateDecorator(inner, table, store, time);

        await (await service.TryTakeRunnableAsync())!.ExecuteAsync(CancellationToken.None);
        Assert.Equal(1, _job.Runs);

        // It took two minutes: it finishes at 02:01 CET, inside the repeated hour.
        time.Advance(TimeSpan.FromMinutes(2));
        row.LastRun = Utc(2026, 10, 25, 1, 1, 4);
        await service.FinishAsync(JobName);

        Assert.Equal(Utc(2026, 10, 26, 1, 59, 0) - row.LastRun, LastPeriodWritten(table));
    }

    [Fact]
    public async Task A_calendar_job_handed_out_before_its_occurrence_is_skipped()
    {
        // E.g. a restarting server has just reset the row to the job's own hour, and another server's poller took it.
        var time = At(2026, 10, 21, 10, 0, 0); // a Wednesday
        var store = new FakeSettingsStore();
        store.Stored[TaskId] = TestSupport.Setting(TaskId, calendar: Calendars.MondaysAtSeven(), lastScheduledUtc: Utc(2026, 10, 19, 5, 0, 2));
        var table = TableWith(Row(lastRun: Utc(2026, 10, 21, 8, 0, 0)));
        var inner = Substitute.For<IDistributedJobService>();
        inner.TryTakeRunnableAsync().Returns(_job);
        var service = CreateDecorator(inner, table, store, time);

        await (await service.TryTakeRunnableAsync())!.ExecuteAsync(CancellationToken.None);

        Assert.Equal(0, _job.Runs);
        Assert.Empty(_runLog.GetRuns(TaskId));
        Assert.Empty(store.Recorded);
        table.DidNotReceiveWithAnyArgs().RaiseRunningPeriod(default!, default);

        // Its finish puts the calendar's period back: next Monday 07:00 CET.
        await service.FinishAsync(JobName);
        Assert.Equal(Utc(2026, 10, 26, 6, 0, 0) - Utc(2026, 10, 21, 8, 0, 0), LastPeriodWritten(table));
    }

    [Fact]
    public async Task A_paused_calendar_job_skips_the_occurrence_rather_than_postponing_it()
    {
        var time = At(2026, 10, 19, 5, 0, 3);
        var store = new FakeSettingsStore();
        store.Stored[TaskId] = TestSupport.Setting(TaskId, isPaused: true, calendar: Calendars.MondaysAtSeven());
        var table = TableWith(Row(lastRun: Utc(2026, 10, 18, 9, 0, 0)));
        var inner = Substitute.For<IDistributedJobService>();
        inner.TryTakeRunnableAsync().Returns(_job);
        var service = CreateDecorator(inner, table, store, time);

        await (await service.TryTakeRunnableAsync())!.ExecuteAsync(CancellationToken.None);

        Assert.Equal(0, _job.Runs);
        Assert.Equal(Utc(2026, 10, 19, 5, 0, 3), store.Stored[TaskId].LastScheduledUtc);
    }

    [Fact]
    public async Task A_taken_calendar_job_keeps_a_running_period_so_Umbraco_does_not_reclaim_it_early()
    {
        // Hourly; the job's own period is a minute. While due its period is short (10 min after a 50-minute run),
        // which would make Umbraco reclaim the run as stale after Period + MaximumExecutionTime.
        var job = new ScriptedDistributedJob(JobName, TimeSpan.FromMinutes(1));
        var time = At(2026, 10, 19, 9, 0, 3);
        var store = new FakeSettingsStore();
        store.Stored[TaskId] = TestSupport.Setting(TaskId, calendar: Hourly(), lastScheduledUtc: Utc(2026, 10, 19, 8, 0, 2));
        var table = TableWith(Row(lastRun: Utc(2026, 10, 19, 8, 50, 0), period: TimeSpan.FromMinutes(10)));
        var inner = Substitute.For<IDistributedJobService>();
        inner.TryTakeRunnableAsync().Returns(job);
        var service = new ManagedDistributedJobService(
            inner, table, store, _runLog, [job], TestSupport.Calendars, NullLogger<ManagedDistributedJobService>.Instance, time);

        await service.TryTakeRunnableAsync();

        table.Received(1).RaiseRunningPeriod(JobName, TimeSpan.FromMinutes(59) + TimeSpan.FromSeconds(57));
    }

    [Fact]
    public void A_calendar_jobs_running_period_is_at_least_its_own_period()
    {
        var time = At(2026, 10, 19, 8, 55, 0);
        var store = new FakeSettingsStore();
        store.Cached[TaskId] = TestSupport.Setting(TaskId, calendar: Hourly());
        var oneMinute = new ScriptedDistributedJob(JobName, TimeSpan.FromMinutes(1));
        var table = Substitute.For<IDistributedJobTable>();
        var inner = Substitute.For<IDistributedJobService>();

        Assert.Equal(TimeSpan.FromMinutes(5), new ManagedDistributedJobService(
            inner, table, store, _runLog, [oneMinute], TestSupport.Calendars, NullLogger<ManagedDistributedJobService>.Instance, time)
            .RunningPeriod(JobName));
        Assert.Equal(_job.Period, CreateDecorator(inner, table, store, time).RunningPeriod(JobName));
        Assert.Null(CreateDecorator(inner, table, new FakeSettingsStore(), time).RunningPeriod(JobName));
    }

    [Fact]
    public void A_schedule_applied_while_the_job_runs_does_not_shorten_its_running_period()
    {
        var time = At(2026, 10, 19, 6, 59, 0);
        var store = new FakeSettingsStore();
        store.Stored[TaskId] = TestSupport.Setting(TaskId, calendar: Daily(7) with { SavedUtc = Utc(2026, 10, 19, 6, 58, 0) });
        var row = Row(lastRun: Utc(2026, 10, 19, 6, 58, 30));
        row.IsRunning = true;
        var table = TableWith(row);
        table.IsRunning(row, Arg.Any<DateTime>()).Returns(true);
        var service = CreateDecorator(Substitute.For<IDistributedJobService>(), table, store, time);

        service.ApplySchedules([JobName]);

        Assert.Equal(_job.Period, LastPeriodWritten(table)); // not the 90 seconds until 07:00
    }

    private static ScheduledTaskCalendar Daily(int hour, int minute = 0, string zone = "UTC") => new()
    {
        Kind = ScheduledTaskCalendarKind.Daily,
        Time = new TimeOnly(hour, minute),
        TimeZone = zone,
        SavedUtc = Utc(2026, 10, 1, 0, 0, 0),
    };

    private static ScheduledTaskCalendar Hourly() => new()
    {
        Kind = ScheduledTaskCalendarKind.Cron,
        Cron = "0 * * * *",
        TimeZone = "UTC",
        SavedUtc = Utc(2026, 10, 1, 0, 0, 0),
    };

    private static DateTime Utc(int year, int month, int day, int hour, int minute, int second)
        => new(year, month, day, hour, minute, second, DateTimeKind.Utc);

    private static FakeTimeProvider At(int year, int month, int day, int hour, int minute, int second)
        => new(new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero));

    private static DistributedBackgroundJobModel Row(DateTime lastRun, TimeSpan? period = null)
        => new() { Name = JobName, Period = period ?? TimeSpan.FromHours(1), LastRun = lastRun, LastAttemptedRun = lastRun };

    private static IDistributedJobTable TableWith(DistributedBackgroundJobModel row)
    {
        var table = Substitute.For<IDistributedJobTable>();
        table.PollPeriod.Returns(TimeSpan.FromSeconds(5));
        table.GetAll().Returns(_ => new Dictionary<string, DistributedBackgroundJobModel> { [row.Name] = row });
        return table;
    }

    private static TimeSpan LastPeriodWritten(IDistributedJobTable table)
    {
        var call = table.ReceivedCalls().Last(c => c.GetMethodInfo().Name == nameof(IDistributedJobTable.SetPeriods));
        return ((IReadOnlyDictionary<string, TimeSpan>)call.GetArguments()[0]!)[JobName];
    }

    private IDistributedJobTable TableWithRow(TimeSpan period)
    {
        var table = Substitute.For<IDistributedJobTable>();
        var lastRun = DateTime.UtcNow.AddMinutes(-1);
        table.PollPeriod.Returns(TimeSpan.FromSeconds(5));
        table.GetAll().Returns(new Dictionary<string, DistributedBackgroundJobModel>
        {
            [JobName] = new() { Name = JobName, Period = period, LastRun = lastRun, LastAttemptedRun = lastRun },
        });
        return table;
    }

    private ManagedDistributedJobService CreateDecorator(
        IDistributedJobService inner, IDistributedJobTable table, IScheduledTaskSettingsStore? settings = null, TimeProvider? time = null)
        => new(inner, table, settings ?? _settings, _runLog, [_job], TestSupport.Calendars, NullLogger<ManagedDistributedJobService>.Instance, time);
}

/// <summary>A job that asks Umbraco (18.1+) to align its runs to the clock.</summary>
public sealed class AlignedDistributedJob(string name, TimeSpan period) : IDistributedBackgroundJob
{
    public string Name { get; } = name;

    public TimeSpan Period { get; } = period;

    public bool AlignToClock => true;

    public Task ExecuteAsync() => Task.CompletedTask;

    public Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// A settings store with a per-server cache (<see cref="Cached"/>) in front of the database
/// (<see cref="Stored"/>); <see cref="Refresh"/> copies the database into the cache.
/// </summary>
internal sealed class FakeSettingsStore : IScheduledTaskSettingsStore
{
    public Dictionary<string, ScheduledTaskSetting> Cached { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, ScheduledTaskSetting> Stored { get; } = new(StringComparer.Ordinal);

    public bool IsWaitingForDatabase { get; set; }

    public ScheduledTaskSetting? Get(string taskId) => Cached.GetValueOrDefault(taskId);

    public IReadOnlyDictionary<string, ScheduledTaskSetting> GetAll() => Cached;

    public void Refresh()
    {
        if (IsWaitingForDatabase) return;
        Cached.Clear();
        foreach (var (id, setting) in Stored) Cached[id] = setting;
    }

    public ScheduledTaskSetting? Update(string taskId, Func<ScheduledTaskSettingState, ScheduledTaskSettingState> change, string? updatedBy)
        => throw new NotSupportedException();

    public List<(string TaskId, DateTime Utc)> Recorded { get; } = [];

    public void RecordScheduledOccurrence(string taskId, DateTime utc)
    {
        Recorded.Add((taskId, utc));
        if (Stored.GetValueOrDefault(taskId) is not { Calendar: not null } stored || stored.LastScheduledUtc >= utc) return;
        Stored[taskId] = Cached[taskId] = stored with { LastScheduledUtc = utc };
    }
}

public class DistributedJobTableTests
{
    private const string JobName = "NightlyExport";

    private readonly ICoreScopeProvider _scopeProvider = Substitute.For<ICoreScopeProvider>();
    private readonly IDistributedJobRepository _repository = Substitute.For<IDistributedJobRepository>();
    private readonly DistributedJobTable _table;

    public DistributedJobTableTests()
    {
        _scopeProvider.CreateCoreScope().ReturnsForAnyArgs(_ => Substitute.For<ICoreScope>());
        var settings = Substitute.For<IOptionsMonitor<DistributedJobSettings>>();
        settings.CurrentValue.Returns(new DistributedJobSettings());
        _table = new DistributedJobTable(_scopeProvider, _repository, settings);
    }

    [Fact]
    public void Run_now_takes_a_calendar_job_with_its_running_period()
    {
        var row = new DistributedBackgroundJobModel { Name = JobName, Period = TimeSpan.Zero, LastRun = DateTime.UtcNow.AddHours(-1) };
        _repository.GetByName(JobName).Returns(row);

        Assert.Equal(DistributedTakeResult.Taken, _table.TryTake(JobName, TimeSpan.FromHours(2)));

        Assert.True(row.IsRunning);
        Assert.Equal(TimeSpan.FromHours(2), row.Period);
        _repository.Received(1).Update(row);
    }

    [Fact]
    public void A_running_period_only_ever_raises_a_running_rows_period()
    {
        var running = new DistributedBackgroundJobModel { Name = JobName, Period = TimeSpan.FromMinutes(10), IsRunning = true };
        _repository.GetByName(JobName).Returns(running);

        _table.RaiseRunningPeriod(JobName, TimeSpan.FromMinutes(5));
        Assert.Equal(TimeSpan.FromMinutes(10), running.Period);

        _table.RaiseRunningPeriod(JobName, TimeSpan.FromMinutes(59));
        Assert.Equal(TimeSpan.FromMinutes(59), running.Period);

        running.IsRunning = false; // finished meanwhile: its finish wrote the period it needs
        _table.RaiseRunningPeriod(JobName, TimeSpan.FromHours(2));
        Assert.Equal(TimeSpan.FromMinutes(59), running.Period);
        _repository.Received(1).Update(running);
    }
}
