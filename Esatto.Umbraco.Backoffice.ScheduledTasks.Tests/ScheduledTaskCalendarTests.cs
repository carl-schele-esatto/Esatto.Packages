using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Infrastructure.Models;
using Umbraco.Cms.Infrastructure.Services;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

internal static class Calendars
{
    public const string Stockholm = "Europe/Stockholm";

    // 2026-10-18 is a Sunday; Stockholm is on summer time (UTC+2) until 2026-10-25 03:00.
    public static readonly DateTime SundayUtc = new(2026, 10, 18, 8, 0, 0, DateTimeKind.Utc);

    public static ScheduledTaskCalendar MondaysAtSeven(DateTime? savedUtc = null) => new()
    {
        Kind = ScheduledTaskCalendarKind.Weekly,
        Time = new TimeOnly(7, 0),
        Weekdays = [DayOfWeek.Monday],
        TimeZone = Stockholm,
        SavedUtc = savedUtc ?? SundayUtc,
    };

    public static DateTime Utc(int year, int month, int day, int hour, int minute = 0)
        => new(year, month, day, hour, minute, 0, DateTimeKind.Utc);
}

public class ScheduledTaskCalendarEvaluatorTests
{
    private readonly ScheduledTaskCalendarEvaluator _evaluator = TestSupport.Calendars;

    private static ScheduledTaskCalendar Make(ScheduledTaskCalendarKind kind, int hour, int minute = 0, int day = 1, int month = 1, string? cron = null)
        => new()
        {
            Kind = kind,
            Time = new TimeOnly(hour, minute),
            DayOfMonth = day,
            Month = month,
            Cron = cron,
            TimeZone = Calendars.Stockholm,
        };

    [Fact]
    public void Every_Monday_at_seven_is_seven_oclock_Stockholm_time()
    {
        var next = _evaluator.NextOccurrenceUtc(Calendars.MondaysAtSeven(), Calendars.SundayUtc);

        Assert.Equal(Calendars.Utc(2026, 10, 19, 5), next); // 07:00 CEST
    }

    [Fact]
    public void Summer_time_ending_keeps_the_local_time()
    {
        var next = _evaluator.NextOccurrenceUtc(Calendars.MondaysAtSeven(), Calendars.Utc(2026, 10, 19, 5));

        Assert.Equal(Calendars.Utc(2026, 10, 26, 6), next); // 07:00 CET
    }

    [Fact]
    public void Several_weekdays()
    {
        var calendar = Calendars.MondaysAtSeven() with { Weekdays = [DayOfWeek.Thursday, DayOfWeek.Monday] };

        Assert.Equal("0 7 * * 1,4", calendar.ToCronExpression());
        Assert.Equal(
            [Calendars.Utc(2026, 10, 19, 5), Calendars.Utc(2026, 10, 22, 5), Calendars.Utc(2026, 10, 26, 6)],
            _evaluator.NextOccurrencesUtc(calendar, Calendars.SundayUtc, 3));
    }

    [Fact]
    public void Daily()
        => Assert.Equal(
            Calendars.Utc(2026, 10, 18, 16, 30),
            _evaluator.NextOccurrenceUtc(Make(ScheduledTaskCalendarKind.Daily, 18, 30), Calendars.SundayUtc));

    [Fact]
    public void Monthly_on_a_day()
        => Assert.Equal(
            Calendars.Utc(2026, 11, 1, 5),
            _evaluator.NextOccurrenceUtc(Make(ScheduledTaskCalendarKind.Monthly, 6, day: 1), Calendars.SundayUtc));

    [Fact]
    public void Months_without_the_day_are_skipped()
        => Assert.Equal(
            Calendars.Utc(2026, 12, 31, 5),
            _evaluator.NextOccurrenceUtc(Make(ScheduledTaskCalendarKind.Monthly, 6, day: 31), Calendars.Utc(2026, 11, 1, 0)));

    [Fact]
    public void Yearly_on_a_date()
        => Assert.Equal(
            Calendars.Utc(2027, 3, 15, 8),
            _evaluator.NextOccurrenceUtc(Make(ScheduledTaskCalendarKind.Yearly, 9, day: 15, month: 3), Calendars.SundayUtc));

    [Fact]
    public void The_29th_of_February_comes_round_in_leap_years()
        => Assert.Equal(
            Calendars.Utc(2028, 2, 29, 11),
            _evaluator.NextOccurrenceUtc(Make(ScheduledTaskCalendarKind.Yearly, 12, day: 29, month: 2), Calendars.SundayUtc));

    [Fact]
    public void A_time_skipped_when_summer_time_starts_runs_when_the_clock_resumes()
    {
        // 2027-03-28: Stockholm clocks jump from 02:00 to 03:00, so 02:30 doesn't exist.
        var next = _evaluator.NextOccurrenceUtc(Make(ScheduledTaskCalendarKind.Daily, 2, 30), Calendars.Utc(2027, 3, 27, 12));

        Assert.Equal(Calendars.Utc(2027, 3, 28, 1), next); // 03:00 CEST
    }

    [Fact]
    public void A_time_repeated_when_summer_time_ends_runs_once()
    {
        // 2026-10-25: Stockholm clocks go back from 03:00 to 02:00, so 02:30 happens twice.
        var calendar = Make(ScheduledTaskCalendarKind.Daily, 2, 30);
        var first = _evaluator.NextOccurrenceUtc(calendar, Calendars.Utc(2026, 10, 24, 12));
        var second = _evaluator.NextOccurrenceUtc(calendar, first!.Value);

        Assert.Equal(Calendars.Utc(2026, 10, 25, 0, 30), first);   // 02:30 CEST
        Assert.Equal(Calendars.Utc(2026, 10, 26, 1, 30), second);  // next day, 02:30 CET
    }

    [Theory]
    // A run of 02:30 CEST that ended at 02:05 CET (second pass), or a tick there: not 02:30 CET again.
    [InlineData(Calendars.Stockholm, 2, 30, "2026-10-25T01:05:00Z", "2026-10-26T01:30:00Z")]
    [InlineData(Calendars.Stockholm, 2, 55, "2026-10-25T01:02:00Z", "2026-10-26T01:55:00Z")]
    [InlineData(Calendars.Stockholm, 2, 30, "2026-10-25T01:29:59Z", "2026-10-26T01:30:00Z")]
    // New York: 01:30 EDT = 05:30Z, 01:30 EST = 06:30Z on 2026-11-01.
    [InlineData("America/New_York", 1, 30, "2026-11-01T06:05:00Z", "2026-11-02T06:30:00Z")]
    // Before the first instance it is still the first instance.
    [InlineData(Calendars.Stockholm, 2, 30, "2026-10-25T00:10:00Z", "2026-10-25T00:30:00Z")]
    public void The_second_instance_of_a_repeated_time_is_never_returned(string zone, int hour, int minute, string after, string expected)
    {
        var calendar = Make(ScheduledTaskCalendarKind.Daily, hour, minute) with { TimeZone = zone };

        Assert.Equal(Parse(expected), _evaluator.NextOccurrenceUtc(calendar, Parse(after)));
    }

    [Fact]
    public void A_frequent_schedule_skips_the_repeated_hour()
    {
        // Every 15 minutes: 02:00–02:45 CEST run; their repeats in CET don't; the next run is 03:00 CET.
        var calendar = Make(ScheduledTaskCalendarKind.Cron, 0, cron: "*/15 * * * *");

        Assert.Equal(Calendars.Utc(2026, 10, 25, 2, 0), _evaluator.NextOccurrenceUtc(calendar, Calendars.Utc(2026, 10, 25, 0, 45)));
        Assert.Equal(Calendars.Utc(2026, 10, 25, 2, 0), _evaluator.NextOccurrenceUtc(calendar, Calendars.Utc(2026, 10, 25, 1, 20)));
    }

    [Fact]
    public void The_latest_missed_occurrence_is_the_one_a_catch_up_run_serves()
    {
        var mondays = Calendars.MondaysAtSeven();

        Assert.Equal(Calendars.Utc(2026, 11, 2, 6), _evaluator.LatestOccurrenceUtc(mondays, Calendars.SundayUtc, Calendars.Utc(2026, 11, 3, 9)));
        Assert.Equal(Calendars.Utc(2026, 10, 19, 5), _evaluator.LatestOccurrenceUtc(mondays, Calendars.SundayUtc, Calendars.Utc(2026, 10, 19, 5)));
        Assert.Null(_evaluator.LatestOccurrenceUtc(mondays, Calendars.SundayUtc, Calendars.Utc(2026, 10, 19, 4)));

        var everyMinute = Make(ScheduledTaskCalendarKind.Cron, 0, cron: "* * * * *");
        Assert.Equal(
            Calendars.Utc(2026, 12, 1, 0, 0),
            _evaluator.LatestOccurrenceUtc(everyMinute, Calendars.SundayUtc, Calendars.Utc(2026, 12, 1, 0, 0).AddSeconds(30)));
    }

    [Fact]
    public void The_latest_missed_occurrence_is_found_however_many_come_before_it_in_the_window()
    {
        // Every minute on the 1st (UTC), back on the 3rd: the look-back window holds part of 1 September
        // and all of 1 October, over 2000 occurrences; the latest is 23:59 on 1 October.
        var calendar = Make(ScheduledTaskCalendarKind.Cron, 0, cron: "* * 1 * *") with { TimeZone = "UTC" };

        Assert.Equal(
            Calendars.Utc(2026, 10, 1, 23, 59),
            _evaluator.LatestOccurrenceUtc(calendar, Calendars.Utc(2026, 8, 20, 0), Calendars.Utc(2026, 10, 3, 0)));
    }

    [Fact]
    public void Cron_with_both_day_fields_runs_on_either_as_in_standard_cron()
    {
        // The 1st of the month and every Monday, at 09:00.
        var calendar = Make(ScheduledTaskCalendarKind.Cron, 0, cron: "0 9 1 * 1");

        Assert.Null(_evaluator.Validate(calendar, Calendars.SundayUtc));
        Assert.Equal(
            [Calendars.Utc(2026, 10, 19, 7), Calendars.Utc(2026, 10, 26, 8), Calendars.Utc(2026, 11, 1, 8), Calendars.Utc(2026, 11, 2, 8)],
            _evaluator.NextOccurrencesUtc(calendar, Calendars.SundayUtc, 4));
    }

    [Fact]
    public void Friday_the_13th_of_February_is_every_13_February_and_every_Friday_in_February()
    {
        var calendar = Make(ScheduledTaskCalendarKind.Cron, 0, cron: "0 0 13 2 5");

        Assert.Null(_evaluator.Validate(calendar, Calendars.SundayUtc));
        Assert.Equal(Calendars.Utc(2027, 2, 4, 23), _evaluator.NextOccurrenceUtc(calendar, Calendars.SundayUtc)); // Fri 5 Feb 00:00 CET
    }

    [Fact]
    public void A_star_in_either_day_field_keeps_the_other_as_is()
    {
        Assert.Equal(["0 9 */2 * 1"], ScheduledTaskCalendarEvaluator.CronExpressions("0 9 */2 * 1"));
        Assert.Equal(["0 9 1 * *", "0 9 * * 1-5"], ScheduledTaskCalendarEvaluator.CronExpressions(" 0  9 1 * 1-5 "));
    }

    [Fact]
    public void Cron_expressions_longer_than_the_column_allows_are_rejected()
    {
        var cron = $"{string.Join(',', Enumerable.Range(0, 60))} {string.Join(',', Enumerable.Range(0, 24))} * * *";
        Assert.True(cron.Length > ScheduledTaskCalendarEvaluator.MaxCronLength);

        Assert.Equal(
            "The cron expression can be at most 200 characters long.",
            _evaluator.Validate(Make(ScheduledTaskCalendarKind.Cron, 0, cron: cron), Calendars.SundayUtc));
    }

    [Theory]
    [InlineData("UTC")]
    [InlineData("Etc/UTC")]
    [InlineData("utc")]
    [InlineData("Europe/Stockholm")]
    [InlineData("W. Europe Standard Time")]
    public void Common_time_zone_ids_are_accepted(string zone)
        => Assert.Null(_evaluator.Validate(Calendars.MondaysAtSeven() with { TimeZone = zone }, Calendars.SundayUtc));

    [Theory]
    // Newer than some servers' time zone data; mapped to an older zone with the same clock times.
    [InlineData("America/Ciudad_Juarez", "America/Denver")]
    [InlineData("Asia/Urumqi", "Asia/Bishkek")]
    public void Zones_the_server_doesnt_know_yet_use_an_equivalent_one(string zone, string equivalent)
    {
        var calendar = Calendars.MondaysAtSeven() with { TimeZone = zone };

        Assert.Null(_evaluator.Validate(calendar, Calendars.SundayUtc));
        Assert.Equal(
            _evaluator.NextOccurrenceUtc(calendar with { TimeZone = equivalent }, Calendars.SundayUtc),
            _evaluator.NextOccurrenceUtc(calendar, Calendars.SundayUtc));
    }

    internal static string UnknownZone(string zone)
        => $"Unknown time zone “{zone}”: this server can't convert times in it. Choose a zone with the same clock times, or UTC.";

    private static DateTime Parse(string utc)
        => DateTime.Parse(utc, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal);

    [Fact]
    public void Cron_expressions_are_supported()
    {
        var weekdays = Make(ScheduledTaskCalendarKind.Cron, 0, cron: "0 7 * * 1-5");

        // Friday 2026-10-23 07:00 -> Monday 2026-10-26 07:00 CET
        Assert.Equal(Calendars.Utc(2026, 10, 26, 6), _evaluator.NextOccurrenceUtc(weekdays, Calendars.Utc(2026, 10, 23, 5)));
    }

    [Theory]
    [InlineData(ScheduledTaskCalendarKind.Weekly, 1, 1, null, "Choose at least one weekday.")]
    [InlineData(ScheduledTaskCalendarKind.Monthly, 32, 1, null, "The day of the month must be between 1 and 31.")]
    [InlineData(ScheduledTaskCalendarKind.Yearly, 1, 13, null, "The month must be between 1 and 12.")]
    [InlineData(ScheduledTaskCalendarKind.Yearly, 30, 2, null, "February has no day 30.")]
    [InlineData(ScheduledTaskCalendarKind.Cron, 1, 1, "every monday", InvalidCron)]
    [InlineData(ScheduledTaskCalendarKind.Cron, 1, 1, "0 0 7 * * 1", InvalidCron)]
    [InlineData(ScheduledTaskCalendarKind.Cron, 1, 1, "0 0 99999999999 * *", InvalidCron)]
    [InlineData(ScheduledTaskCalendarKind.Cron, 1, 1, "*/99999999999 * * * *", InvalidCron)]
    [InlineData(ScheduledTaskCalendarKind.Cron, 1, 1, "0 0 99999999999 * 1", InvalidCron)]
    [InlineData(ScheduledTaskCalendarKind.Cron, 1, 1, "0 0 31 2 *", "This schedule doesn't run in the next five years.")]
    public void Unusable_schedules_say_why(ScheduledTaskCalendarKind kind, int day, int month, string? cron, string expected)
        => Assert.Equal(expected, _evaluator.Validate(Make(kind, 7, day: day, month: month, cron: cron), Calendars.SundayUtc));

    private const string InvalidCron = "Enter a valid five-field cron expression: minute hour day-of-month month day-of-week (Sunday = 0).";

    [Fact]
    public void Unknown_time_zones_are_rejected()
        => Assert.Equal(
            UnknownZone("Mars/Olympus"),
            _evaluator.Validate(Calendars.MondaysAtSeven() with { TimeZone = "Mars/Olympus" }, Calendars.SundayUtc));

    [Fact]
    public void A_distributed_job_gets_the_period_up_to_its_next_occurrence()
    {
        var lastRun = Calendars.Utc(2026, 10, 19, 5);

        Assert.Equal(TimeSpan.FromDays(7) + TimeSpan.FromHours(1), _evaluator.PeriodUntilNextOccurrence(Calendars.MondaysAtSeven(), lastRun));
    }

    [Fact]
    public void A_schedule_saved_after_the_last_run_counts_from_when_it_was_saved()
    {
        // Last ran Sunday 08:00 (UTC); the schedule was saved Monday 06:00 UTC, after that Monday's 07:00 CEST.
        var calendar = Calendars.MondaysAtSeven(savedUtc: Calendars.Utc(2026, 10, 19, 6));

        Assert.Equal(
            Calendars.Utc(2026, 10, 26, 6) - Calendars.SundayUtc,
            _evaluator.PeriodUntilNextOccurrence(calendar, Calendars.SundayUtc));
    }
}

public class CalendarScheduledRecurringJobTests
{
    private static readonly string TaskId = ScheduledTaskIdentity.ForRecurring(typeof(ScriptedJob));

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Calendars.SundayUtc));
    private readonly IRuntimeState _runtimeState = Substitute.For<IRuntimeState>();
    private readonly IMainDom _mainDom = Substitute.For<IMainDom>();
    private readonly IServerRoleAccessor _serverRole = Substitute.For<IServerRoleAccessor>();
    private readonly IEventAggregator _events = Substitute.For<IEventAggregator>();
    private readonly IEventMessagesFactory _eventMessages = Substitute.For<IEventMessagesFactory>();
    private readonly IScheduledTaskSettingsStore _settings = Substitute.For<IScheduledTaskSettingsStore>();
    private readonly ScheduledTaskRunLog _runLog;
    private readonly ScriptedJob _job = new(TimeSpan.FromMinutes(5));

    public CalendarScheduledRecurringJobTests()
    {
        _runtimeState.Level.Returns(RuntimeLevel.Run);
        _mainDom.IsMainDom.Returns(true);
        _serverRole.CurrentServerRole.Returns(ServerRole.Single);
        _eventMessages.Get().Returns(_ => new EventMessages());
        _runLog = new ScheduledTaskRunLog(_time, TestSupport.Options());
        OnCalendar(Calendars.MondaysAtSeven());
    }

    private void OnCalendar(ScheduledTaskCalendar calendar, bool isPaused = false, DateTime? lastScheduledUtc = null)
        => _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, isPaused: isPaused, calendar: calendar, lastScheduledUtc: lastScheduledUtc));

    private ManagedRecurringBackgroundJobHostedService<ScriptedJob> CreateHost() => new(
        _runtimeState, NullLogger<RecurringBackgroundJobHostedService<ScriptedJob>>.Instance, _mainDom, _serverRole,
        _events, _eventMessages, _job, _time, _settings, _runLog, TestSupport.Options(), TestSupport.Calendars,
        new RecurringJobHostRegistry());

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

    private void AdvanceTo(DateTime utc) => _time.Advance(utc - _time.GetUtcNow().UtcDateTime);

    [Fact]
    public void A_calendar_ticks_every_thirty_seconds_and_shows_the_next_occurrence()
    {
        using var host = CreateHost();
        host.SyncWithSettings();

        Assert.Equal(ManagedRecurringBackgroundJobHostedService<ScriptedJob>.CalendarTick, host.TimerPeriod);
        Assert.Equal(Calendars.Utc(2026, 10, 19, 5), host.NextRunUtc);
    }

    [Fact]
    public async Task It_runs_at_the_occurrence_and_not_before_or_again()
    {
        using var host = CreateHost();

        await TickAsync(host); // the startup tick: nothing due, so no extra run after start
        AdvanceTo(Calendars.Utc(2026, 10, 19, 4, 59));
        await TickAsync(host);
        Assert.Equal(0, _job.Runs);

        AdvanceTo(Calendars.Utc(2026, 10, 19, 5, 0).AddSeconds(20));
        await TickAsync(host);
        _time.Advance(TimeSpan.FromSeconds(30));
        await TickAsync(host);

        Assert.Equal(1, _job.Runs);
        // The occurrence served, for this server and this schedule (not the tick time, nor shared).
        _settings.Received(1).RecordServerOccurrence(TaskId, Calendars.Utc(2026, 10, 19, 5), Calendars.SundayUtc);
        _settings.DidNotReceiveWithAnyArgs().RecordScheduledOccurrence(default!, default);
        Assert.Equal(Calendars.Utc(2026, 10, 26, 6), host.NextRunUtc);
    }

    [Fact]
    public async Task An_occurrence_already_handled_before_a_restart_isnt_repeated()
    {
        AdvanceTo(Calendars.Utc(2026, 10, 19, 5, 10));
        OnCalendar(Calendars.MondaysAtSeven());
        _settings.GetServerOccurrence(TaskId, Calendars.SundayUtc).Returns(Calendars.Utc(2026, 10, 19, 5, 0));
        using var host = CreateHost();

        await TickAsync(host);

        Assert.Equal(0, _job.Runs);
    }

    [Fact]
    public async Task An_occurrence_missed_while_the_site_was_down_runs_once_on_start()
    {
        // This server followed the schedule before it went down (it recorded so when it first saw it).
        _settings.GetServerOccurrence(TaskId, Calendars.SundayUtc).Returns(Calendars.SundayUtc);
        AdvanceTo(Calendars.Utc(2026, 10, 19, 9));
        using var host = CreateHost();

        await TickAsync(host);
        _time.Advance(TimeSpan.FromSeconds(30));
        await TickAsync(host);

        Assert.Equal(1, _job.Runs);
    }

    [Fact]
    public async Task A_new_server_doesnt_catch_up_occurrences_from_before_it_started()
    {
        // No record on this server (e.g. a new container with a new machine name), started Wednesday.
        AdvanceTo(Calendars.Utc(2026, 10, 21, 9));
        using var host = CreateHost();

        await TickAsync(host);
        _time.Advance(TimeSpan.FromSeconds(30));
        await TickAsync(host);

        Assert.Equal(0, _job.Runs);
        Assert.Equal(Calendars.Utc(2026, 10, 26, 6), host.NextRunUtc);
        // Recorded, so a restart of this server catches up what it misses from now on.
        _settings.Received(1).RecordServerOccurrence(TaskId, Calendars.Utc(2026, 10, 21, 9), Calendars.SundayUtc);

        AdvanceTo(Calendars.Utc(2026, 10, 26, 6).AddSeconds(10));
        await TickAsync(host);
        Assert.Equal(1, _job.Runs);
    }

    [Fact]
    public void A_server_running_when_a_schedule_is_saved_records_that_it_follows_it()
    {
        using var host = CreateHost();
        var saved = Calendars.Utc(2026, 10, 18, 9);
        AdvanceTo(saved.AddSeconds(20));
        OnCalendar(Calendars.MondaysAtSeven(savedUtc: saved));

        host.SyncWithSettings();
        host.SyncWithSettings();

        // Once, counting from the save: a restart before Monday still catches up a missed 07:00.
        _settings.Received(1).RecordServerOccurrence(TaskId, saved, saved);
        Assert.Equal(Calendars.Utc(2026, 10, 19, 5), host.NextRunUtc);
    }

    [Fact]
    public async Task An_occurrence_while_paused_is_skipped_not_postponed()
    {
        OnCalendar(Calendars.MondaysAtSeven(), isPaused: true);
        using var host = CreateHost();
        AdvanceTo(Calendars.Utc(2026, 10, 19, 5, 0).AddSeconds(10));
        await TickAsync(host);

        OnCalendar(Calendars.MondaysAtSeven()); // resumed later that morning
        _time.Advance(TimeSpan.FromHours(2));
        await TickAsync(host);

        Assert.Equal(0, _job.Runs);
        Assert.Equal(Calendars.Utc(2026, 10, 26, 6), host.NextRunUtc);
    }

    [Fact]
    public async Task An_occurrence_this_server_cant_run_yet_is_retried_on_the_next_tick()
    {
        _serverRole.CurrentServerRole.Returns(ServerRole.Unknown);
        using var host = CreateHost();
        AdvanceTo(Calendars.Utc(2026, 10, 19, 5, 0).AddSeconds(10));
        await TickAsync(host);
        Assert.Equal(0, _job.Runs);

        _serverRole.CurrentServerRole.Returns(ServerRole.Single);
        _time.Advance(TimeSpan.FromSeconds(30));
        await TickAsync(host);

        Assert.Equal(1, _job.Runs);
    }

    [Fact]
    public async Task Run_now_doesnt_move_the_calendar()
    {
        using var host = CreateHost();

        host.RequestRun("Carl");
        await TickAsync(host);

        Assert.Equal(1, _job.Runs);
        Assert.Equal(Calendars.Utc(2026, 10, 19, 5), host.NextRunUtc);
        // Only the record that this server follows the schedule (from its save), no occurrence.
        _settings.DidNotReceive().RecordServerOccurrence(TaskId, Arg.Is<DateTime>(d => d != Calendars.SundayUtc), Arg.Any<DateTime>());
    }

    [Fact]
    public async Task Saving_a_schedule_never_triggers_a_run_for_a_time_that_has_passed()
    {
        // Saved Monday 08:00 CEST, an hour after that Monday's 07:00.
        AdvanceTo(Calendars.Utc(2026, 10, 19, 6));
        OnCalendar(Calendars.MondaysAtSeven(savedUtc: Calendars.Utc(2026, 10, 19, 6)));
        using var host = CreateHost();

        await TickAsync(host);

        Assert.Equal(0, _job.Runs);
        Assert.Equal(Calendars.Utc(2026, 10, 26, 6), host.NextRunUtc);
    }

    [Fact]
    public async Task A_catch_up_run_records_the_latest_occurrence_it_served()
    {
        // Down for two Mondays; back on Tuesday.
        _settings.GetServerOccurrence(TaskId, Calendars.SundayUtc).Returns(Calendars.SundayUtc);
        AdvanceTo(Calendars.Utc(2026, 10, 27, 9));
        using var host = CreateHost();

        await TickAsync(host);
        _time.Advance(TimeSpan.FromSeconds(30));
        await TickAsync(host);

        Assert.Equal(1, _job.Runs);
        _settings.Received(1).RecordServerOccurrence(TaskId, Calendars.Utc(2026, 10, 26, 6), Calendars.SundayUtc);
        Assert.Equal(Calendars.Utc(2026, 11, 2, 6), host.NextRunUtc);
    }

    [Fact]
    public async Task A_catch_up_run_inside_the_repeated_autumn_hour_doesnt_run_again_at_its_second_instance()
    {
        // Daily 02:30 Stockholm; the site was down over 02:30 CEST (00:30Z) and is back at 02:10 CET (01:10Z).
        var daily = new ScheduledTaskCalendar
        {
            Kind = ScheduledTaskCalendarKind.Daily, Time = new TimeOnly(2, 30), TimeZone = Calendars.Stockholm,
            SavedUtc = Calendars.Utc(2026, 10, 24, 12),
        };
        OnCalendar(daily);
        _settings.GetServerOccurrence(TaskId, daily.SavedUtc).Returns(daily.SavedUtc);
        AdvanceTo(Calendars.Utc(2026, 10, 25, 1, 10));
        using var host = CreateHost();

        await TickAsync(host);
        Assert.Equal(1, _job.Runs);
        _settings.Received(1).RecordServerOccurrence(TaskId, Calendars.Utc(2026, 10, 25, 0, 30), daily.SavedUtc);

        AdvanceTo(Calendars.Utc(2026, 10, 25, 1, 31)); // 02:31 CET
        await TickAsync(host);

        Assert.Equal(1, _job.Runs);
        Assert.Equal(Calendars.Utc(2026, 10, 26, 1, 30), host.NextRunUtc);
    }

    [Fact]
    public async Task Another_servers_record_doesnt_stop_this_server_running_a_recurring_job()
    {
        // A recurring job runs on every server whose role matches (e.g. a per-server temp folder
        // clean-up); the shared record says another server (or a distributed run) handled 07:00.
        OnCalendar(Calendars.MondaysAtSeven(), lastScheduledUtc: Calendars.Utc(2026, 10, 19, 5, 0));
        using var host = CreateHost();
        AdvanceTo(Calendars.Utc(2026, 10, 19, 5, 0).AddSeconds(25));

        await TickAsync(host);

        Assert.Equal(1, _job.Runs);
    }

    [Fact]
    public async Task This_servers_record_of_an_earlier_schedule_doesnt_count()
    {
        // Recorded under the previous schedule; the new one was saved at 06:59 Monday.
        var saved = Calendars.Utc(2026, 10, 19, 4, 59);
        OnCalendar(Calendars.MondaysAtSeven(savedUtc: saved));
        _settings.GetServerOccurrence(TaskId, Calendars.SundayUtc).Returns(Calendars.Utc(2026, 10, 19, 5, 10));
        using var host = CreateHost();
        AdvanceTo(Calendars.Utc(2026, 10, 19, 5, 0).AddSeconds(10));

        await TickAsync(host);

        Assert.Equal(1, _job.Runs);
        _settings.Received(1).RecordServerOccurrence(TaskId, Calendars.Utc(2026, 10, 19, 5), saved);
    }

    [Fact]
    public async Task Switching_from_a_calendar_to_a_long_interval_counts_the_interval_from_the_switch()
    {
        using var host = CreateHost();
        await TickAsync(host); // a calendar tick, nothing due

        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromDays(30)));
        host.SyncWithSettings();
        var switchedAt = _time.GetUtcNow().UtcDateTime;
        Assert.Equal(ManagedRecurringBackgroundJobHostedService<ScriptedJob>.Heartbeat, host.TimerPeriod);

        _time.Advance(ManagedRecurringBackgroundJobHostedService<ScriptedJob>.Heartbeat);
        await TickAsync(host);

        Assert.Equal(0, _job.Runs);
        Assert.Equal(switchedAt.AddDays(30), host.NextRunUtc);
    }

    [Fact]
    public async Task The_calendar_tick_that_finds_an_interval_instead_isnt_a_run()
    {
        using var host = CreateHost();
        await TickAsync(host);

        _settings.Get(TaskId).Returns(TestSupport.Setting(TaskId, period: TimeSpan.FromMinutes(10)));
        _time.Advance(ManagedRecurringBackgroundJobHostedService<ScriptedJob>.CalendarTick);
        await TickAsync(host); // the switch is noticed by this tick

        Assert.Equal(0, _job.Runs);
        Assert.Equal(TimeSpan.FromMinutes(10), host.TimerPeriod);
    }

    [Fact]
    public async Task Nothing_runs_on_its_own_before_the_settings_could_be_read()
    {
        // E.g. before the package's migration has run: the task may well be paused.
        _settings.IsWaitingForDatabase.Returns(true);
        _settings.Get(TaskId).Returns((ScheduledTaskSetting?)null);
        using var host = CreateHost();

        await TickAsync(host);
        Assert.True(host.Trigger(global::Umbraco.Cms.Infrastructure.HostedServices.NextExecutionStrategy.None));
        await TickAsync(host);

        Assert.Equal(0, _job.Runs);
        Assert.NotEmpty(_runLog.GetRuns(TaskId));
        Assert.All(_runLog.GetRuns(TaskId), run => Assert.Equal(ScheduledTaskRunOutcome.Skipped, run.Outcome));

        // Run now in the dashboard is an editor's explicit request, as it is while paused.
        host.RequestRun("Carl");
        await TickAsync(host);
        Assert.Equal(1, _job.Runs);
    }

    [Fact]
    public void Settings_that_couldnt_be_read_yet_arent_applied_as_defaults()
    {
        using var host = CreateHost();
        host.SyncWithSettings();
        Assert.Equal(ManagedRecurringBackgroundJobHostedService<ScriptedJob>.CalendarTick, host.TimerPeriod);

        _settings.IsWaitingForDatabase.Returns(true);
        _settings.Get(TaskId).Returns((ScheduledTaskSetting?)null);
        host.SyncWithSettings();

        Assert.Equal(ManagedRecurringBackgroundJobHostedService<ScriptedJob>.CalendarTick, host.TimerPeriod);
    }

    [Fact]
    public async Task A_job_that_otherwise_only_runs_when_triggered_runs_on_its_calendar()
    {
        // Delay = infinite: Umbraco's loop would wait for a trigger before its first tick, and a
        // period change doesn't end that wait.
        _job.DelayOverride = Timeout.InfiniteTimeSpan;
        _settings.Get(TaskId).Returns((ScheduledTaskSetting?)null);
        using var host = CreateHost();
        host.SyncWithSettings();
        await host.StartAsync(CancellationToken.None);
        try
        {
            // On its own schedule it still only runs when triggered.
            Assert.False(await AdvanceUntilRunAsync(TimeSpan.FromMinutes(10), steps: 6));

            // A calendar saved later (picked up by the settings synchronizer) wakes the loop; that
            // first tick is just a calendar check.
            AdvanceTo(Calendars.Utc(2026, 10, 19, 4, 58));
            OnCalendar(Calendars.MondaysAtSeven());
            host.SyncWithSettings();
            Assert.False(await AdvanceUntilRunAsync(TimeSpan.FromSeconds(30), steps: 3));
            Assert.Equal(0, _job.Runs);

            Assert.True(await AdvanceUntilRunAsync(TimeSpan.FromSeconds(30), steps: 10), "the 07:00 occurrence never ran");
            Assert.True(_time.GetUtcNow().UtcDateTime <= Calendars.Utc(2026, 10, 19, 5, 1));
            Assert.False(await AdvanceUntilRunAsync(TimeSpan.FromSeconds(30), steps: 4));
            Assert.Equal(1, _job.Runs);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task A_long_job_delay_doesnt_hold_up_a_calendar_after_startup()
    {
        // E.g. a Delay computed from a first-run time hours away.
        _job.DelayOverride = TimeSpan.FromHours(6);
        AdvanceTo(Calendars.Utc(2026, 10, 19, 4, 59));
        using var host = CreateHost();
        host.SyncWithSettings(); // as on UmbracoApplicationStarted
        await host.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(await AdvanceUntilRunAsync(TimeSpan.FromSeconds(30), steps: 10), "the 07:00 occurrence never ran");
            Assert.True(_time.GetUtcNow().UtcDateTime <= Calendars.Utc(2026, 10, 19, 5, 1));
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>Advances the clock in steps, letting Umbraco's real loop run, until the job has run once.</summary>
    private async Task<bool> AdvanceUntilRunAsync(TimeSpan step, int steps)
    {
        for (var attempt = 0; attempt < steps; attempt++)
        {
            if (await _job.RunCompleted.WaitAsync(TimeSpan.FromMilliseconds(100))) return true;
            _time.Advance(step);
        }

        return await _job.RunCompleted.WaitAsync(TimeSpan.FromMilliseconds(100));
    }
}

public class CalendarScheduleServiceTests
{
    private const string Name = "NightlyExport";
    private static readonly string RecurringId = ScheduledTaskIdentity.ForRecurring(typeof(ScriptedJob));
    private static readonly string DistributedId = ScheduledTaskIdentity.ForDistributed(Name);

    private readonly IScheduledTaskSettingsStore _settings = Substitute.For<IScheduledTaskSettingsStore>();
    private readonly IDistributedJobTable _table = Substitute.For<IDistributedJobTable>();

    private ScheduledTaskService CreateService(IDistributedJobService? distributedJobService = null)
    {
        _settings.GetAll().Returns(new Dictionary<string, ScheduledTaskSetting>());
        _table.GetAll().Returns(new Dictionary<string, DistributedBackgroundJobModel>());
        var serverRole = Substitute.For<IServerRoleAccessor>();
        serverRole.CurrentServerRole.Returns(ServerRole.Single);

        return new ScheduledTaskService(
            [new ScriptedJob(TimeSpan.FromMinutes(5))],
            [new ScriptedDistributedJob(Name, TimeSpan.FromHours(1))],
            new RecurringJobHostRegistry(),
            distributedJobService ?? Substitute.For<IDistributedJobService>(),
            _table,
            _settings,
            new ScheduledTaskRunLog(TimeProvider.System, TestSupport.Options()),
            serverRole,
            Substitute.For<IHostApplicationLifetime>(),
            new ManualDistributedRunTracker(NullLogger<ManualDistributedRunTracker>.Instance),
            new ScheduledTaskPropertyValues(NullLogger<ScheduledTaskPropertyValues>.Instance),
            TestSupport.Calendars,
            TestSupport.Options(),
            NullLogger<ScheduledTaskService>.Instance);
    }

    [Fact]
    public void Saving_a_calendar_stamps_it_and_replaces_the_interval()
    {
        var before = DateTime.UtcNow;
        var result = CreateService().SetSchedule(RecurringId, Calendars.MondaysAtSeven(savedUtc: DateTime.MinValue), "Carl");

        Assert.Equal(ScheduledTaskCommandStatus.Ok, result.Status);
        var after = _settings.ChangeSavedFor(RecurringId)(new ScheduledTaskSettingState(true, TimeSpan.FromHours(2), null));
        Assert.Null(after.PeriodOverride);
        Assert.True(after.IsPaused);
        Assert.Equal(ScheduledTaskCalendarKind.Weekly, after.Calendar!.Kind);
        Assert.True(after.Calendar.SavedUtc >= before);
    }

    [Fact]
    public void An_unusable_calendar_is_rejected_and_not_saved()
    {
        var result = CreateService().SetSchedule(RecurringId, Calendars.MondaysAtSeven() with { Weekdays = [] }, "Carl");

        Assert.Equal(ScheduledTaskCommandStatus.Invalid, result.Status);
        Assert.Equal("Choose at least one weekday.", result.Message);
        _settings.DidNotReceiveWithAnyArgs().Update(default!, default!, default);
    }

    [Fact]
    public void Setting_an_interval_drops_the_calendar()
    {
        CreateService().SetPeriod(RecurringId, TimeSpan.FromMinutes(10), "Carl");

        var after = _settings.ChangeSavedFor(RecurringId)(new ScheduledTaskSettingState(false, null, null, Calendars.MondaysAtSeven()));
        Assert.Null(after.Calendar);
        Assert.Equal(TimeSpan.FromMinutes(10), after.PeriodOverride);
    }

    [Fact]
    public void A_distributed_calendar_writes_the_period_up_to_the_next_occurrence()
    {
        var decorated = new ManagedDistributedJobService(
            Substitute.For<IDistributedJobService>(), _table, _settings, new ScheduledTaskRunLog(TimeProvider.System, TestSupport.Options()),
            [new ScriptedDistributedJob(Name, TimeSpan.FromHours(1))], TestSupport.Calendars, NullLogger<ManagedDistributedJobService>.Instance);
        var service = CreateService(decorated);
        var lastRun = DateTime.UtcNow.AddMinutes(-1);
        _table.GetAll().Returns(new Dictionary<string, DistributedBackgroundJobModel>
        {
            [Name] = new() { Name = Name, Period = TimeSpan.FromHours(1), LastRun = lastRun, LastAttemptedRun = lastRun },
        });
        ScheduledTaskCalendar? saved = null;
        _settings.Update(DistributedId, Arg.Any<Func<ScheduledTaskSettingState, ScheduledTaskSettingState>>(), "Carl")
            .Returns(call =>
            {
                saved = call.Arg<Func<ScheduledTaskSettingState, ScheduledTaskSettingState>>()(ScheduledTaskSettingState.Default).Calendar;
                return TestSupport.Setting(DistributedId, calendar: saved);
            });
        _settings.Get(DistributedId).Returns(_ => saved is null ? null : TestSupport.Setting(DistributedId, calendar: saved));
        _settings.GetAll().Returns(_ => saved is null
            ? new Dictionary<string, ScheduledTaskSetting>()
            : new Dictionary<string, ScheduledTaskSetting> { [DistributedId] = TestSupport.Setting(DistributedId, calendar: saved) });

        var daily = new ScheduledTaskCalendar { Kind = ScheduledTaskCalendarKind.Daily, Time = new TimeOnly(3, 0), TimeZone = "UTC" };
        Assert.Equal(ScheduledTaskCommandStatus.Ok, service.SetSchedule(DistributedId, daily, "Carl").Status);

        var nextThreeAm = DateTime.UtcNow.Date.AddHours(3) > DateTime.UtcNow ? DateTime.UtcNow.Date.AddHours(3) : DateTime.UtcNow.Date.AddDays(1).AddHours(3);
        _table.Received().SetPeriods(Arg.Is<IReadOnlyDictionary<string, TimeSpan>>(p =>
            Math.Abs((p[Name] - (nextThreeAm - lastRun)).TotalSeconds) < 1));
    }

    [Fact]
    public void The_preview_lists_the_next_runs()
    {
        var (next, error) = CreateService().PreviewSchedule(Calendars.MondaysAtSeven(), 3);

        Assert.Null(error);
        Assert.Equal(3, next.Count);
        Assert.All(next, utc => Assert.True(utc > DateTime.UtcNow));
    }

    [Fact]
    public void The_preview_explains_an_unusable_schedule()
    {
        var (next, error) = CreateService().PreviewSchedule(Calendars.MondaysAtSeven() with { TimeZone = "Nowhere/Land" }, 3);

        Assert.Empty(next);
        Assert.Equal(ScheduledTaskCalendarEvaluatorTests.UnknownZone("Nowhere/Land"), error);
    }

    [Theory]
    [InlineData("hourly", "07:00", "Unknown schedule kind “hourly”; use daily, weekly, monthly, yearly or cron.")]
    [InlineData("daily", "7 o'clock", "Enter a time of day as HH:mm, e.g. 07:00.")]
    public void Unreadable_schedule_requests_say_why(string kind, string time, string expected)
    {
        Assert.Null(new ScheduledTaskScheduleDto(kind, time, null, null, null, null, "UTC").ToCalendar(out var error));
        Assert.Equal(expected, error);
    }

    [Theory]
    [InlineData("Monday,Tuesday")] // Enum.TryParse would OR these into Wednesday
    [InlineData("Monday, Tuesday")]
    [InlineData("3")]
    [InlineData("Someday")]
    public void Weekdays_must_be_single_day_names(string weekday)
    {
        Assert.Null(new ScheduledTaskScheduleDto("weekly", "07:00", [weekday], null, null, null, "UTC").ToCalendar(out var error));
        Assert.StartsWith($"Unknown weekday “{weekday}”", error);
    }

    [Theory]
    [InlineData("Daily, Weekly")]
    [InlineData("1")]
    public void The_kind_must_be_a_single_name(string kind)
    {
        Assert.Null(new ScheduledTaskScheduleDto(kind, "07:00", ["Monday"], null, null, null, "UTC").ToCalendar(out var error));
        Assert.StartsWith("Unknown schedule kind", error);
    }

    [Fact]
    public void A_cron_expression_is_stored_without_surrounding_whitespace()
    {
        // Validation measures the trimmed expression; what is stored must be no longer.
        var padded = new string(' ', 3000) + "0 7 * * 1" + new string(' ', 3000);

        var calendar = new ScheduledTaskScheduleDto("cron", null, null, null, null, padded, "UTC").ToCalendar(out var error);

        Assert.Null(error);
        Assert.Equal("0 7 * * 1", calendar!.Cron);
    }

    [Fact]
    public void Schedule_requests_round_trip()
    {
        var dto = new ScheduledTaskScheduleDto("weekly", "07:00", ["Monday", "thursday"], null, null, null, Calendars.Stockholm);

        var calendar = dto.ToCalendar(out var error);

        Assert.Null(error);
        Assert.Equal("0 7 * * 1,4", calendar!.ToCronExpression());
        Assert.Equal(dto with { Weekdays = ["Monday", "Thursday"] }, calendar.ToDto() with { Weekdays = calendar.ToDto().Weekdays!.ToList() }, new ScheduleDtoComparer());
    }

    private sealed class ScheduleDtoComparer : IEqualityComparer<ScheduledTaskScheduleDto>
    {
        public bool Equals(ScheduledTaskScheduleDto? x, ScheduledTaskScheduleDto? y)
            => x!.Kind == y!.Kind && x.Time == y.Time && x.Weekdays!.SequenceEqual(y.Weekdays!) && x.TimeZone == y.TimeZone;

        public int GetHashCode(ScheduledTaskScheduleDto obj) => obj.Kind.GetHashCode();
    }
}
