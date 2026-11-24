using Esatto.Umbraco.Backoffice.SeoReport.Api;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using static Esatto.Umbraco.Backoffice.SeoReport.Tests.Support.ApiResults;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// <c>GET daily</c>: the day-fresh curve for the rolling window or a calendar month, with what the client needs to draw it and word
/// it. It is the one analysis endpoint that asks Google (through the service, which keeps the answer for half an hour), and it never
/// starts the backfill. The service is a substitute that answers with the real curve arithmetic over made-up sessions; the clock is the
/// rig's (<see cref="ReadySettingsHarness.Now"/>, 20 August 2026, unless a test moves it).
/// </summary>
public class DailyEndpointTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static long Sessions(DateOnly day) => DailyCurves.Sessions(day);

    private static async Task<ApiRig> RigAsync(Func<DateOnly, long>? sessions = null, DateTimeOffset? now = null)
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Now: now));
        DailyCurves.Serve(rig, sessions);
        return rig;
    }

    private static DateTimeOffset At(string day) => new(DateOnly.ParseExact(day, "yyyy-MM-dd").ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero);

    private static async Task<DailyResponse> DailyAsync(ApiRig rig, string? month = null)
        => Ok<DailyResponse>(await rig.Controller.GetDaily(month, Ct));

    // ---- the curve ----------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_rolling_window_comes_with_its_chart_summary_days_and_the_months_to_pick()
    {
        var rig = await RigAsync();
        var (series, _) = DailyCurves.Curve(DailyCurves.TodayOf(rig), null);

        var daily = await DailyAsync(rig);

        Assert.Equal(new DailyWindowResponse("2026-07-21", "2026-08-19", null), daily.Window);
        Assert.Equal(DailyChart.ChartId, daily.Chart.Id);
        Assert.Equal(30, daily.Chart.Labels.Count);
        Assert.Equal(daily.Days.Select(d => d.Date), daily.Chart.Labels); // the dates, raw, for the client to format
        Assert.Equal(series.Total, daily.Total);
        Assert.Equal(Math.Round(series.ChangePercent!.Value, 1), daily.ChangePercent);
        Assert.Equal(new DailyPeakResponse("2026-08-09", Sessions(new DateOnly(2026, 8, 9))), daily.Peak);
        Assert.Equal(new DailyDayResponse("2026-07-21", Sessions(new DateOnly(2026, 7, 21))), daily.Days[0]);
        Assert.Equal(30, daily.Days.Length);
        Assert.Equal(30, daily.WindowDays);

        Assert.Equal(13, daily.Months.Length);
        Assert.Equal("", daily.Months[0]); // the rolling window, which is the default
        Assert.Equal("2026-08", daily.Months[1]);
        Assert.Equal("2025-09", daily.Months[^1]);
    }

    [Fact]
    public async Task A_calendar_month_is_that_month()
    {
        var rig = await RigAsync();

        var daily = await DailyAsync(rig, "2026-06");

        Assert.Equal(new DailyWindowResponse("2026-06-01", "2026-06-30", "2026-06"), daily.Window);
        Assert.Equal(30, daily.Days.Length);
        Assert.Equal("2026-06", daily.TextArgs["period"]);
        await rig.Daily.Received(1).GetSeriesAsync("2026-06", Ct);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task No_month_and_an_empty_month_are_both_the_rolling_window(string? month)
    {
        var rig = await RigAsync();

        var daily = await DailyAsync(rig, month);

        Assert.Null(daily.Window.Month);
        Assert.Equal("2026-07-21", daily.Window.Start);
        await rig.Daily.Received(1).GetSeriesAsync(null, Ct); // asked for the same way, so the service has one rolling entry
    }

    [Fact]
    public async Task A_window_with_no_sessions_has_no_peak_and_no_change_and_is_not_an_error()
    {
        var rig = await RigAsync(sessions: _ => 0);

        var daily = await DailyAsync(rig);

        Assert.Null(daily.Peak);
        Assert.Null(daily.ChangePercent); // nothing to compare with: not "infinitely many percent"
        Assert.Equal(0, daily.Total);
        Assert.Equal(30, daily.Days.Length);
        Assert.All(daily.Days, d => Assert.Equal(0, d.Sessions)); // a day without visits is zero, not a gap
    }

    [Fact]
    public async Task The_running_month_on_its_first_day_has_no_days_to_draw_and_says_so()
    {
        var rig = await RigAsync(now: At("2026-09-01"));

        var daily = await DailyAsync(rig, "2026-09");

        Assert.Empty(daily.Days);
        Assert.Empty(daily.Chart.Series); // "no data" in the report contract's own words
        Assert.Null(daily.Peak);
        Assert.Equal(0, daily.WindowDays);
        Assert.Equal(0, daily.TextArgs["windowDays"]);
    }

    // ---- the arguments of the chart's texts -------------------------------------------------------------------------------

    [Theory]
    [InlineData("", "2026-08-20", 30, 2)]           // rolling: its own length and the margin
    [InlineData("2026-06", "2026-08-20", 30, null)] // a finished month: its own length, nothing provisional
    [InlineData("2026-07", "2026-08-20", 31, null)]
    [InlineData("2026-02", "2026-08-20", 28, null)]
    [InlineData("2026-08", "2026-08-20", 19, 2)]    // the running month: the days so far (it ends yesterday) and the margin
    [InlineData("2026-07", "2026-08-01", 31, 2)]    // a month that ended yesterday: its last two days are inside the margin
    [InlineData("2026-07", "2026-08-02", 31, 1)]    // two days ago: only its last day is, so one day and not two
    [InlineData("2026-07", "2026-08-03", 31, null)] // three days ago: older than the margin
    public async Task The_texts_are_told_the_days_the_window_really_has_and_the_provisional_days_only_when_there_are_any(
        string month, string today, int windowDays, int? provisionalDays)
    {
        var rig = await RigAsync(now: At(today));

        var daily = await DailyAsync(rig, month);

        Assert.Equal(windowDays, daily.TextArgs["windowDays"]);
        Assert.Equal(windowDays, daily.WindowDays);
        Assert.Equal(windowDays, daily.Days.Length);
        Assert.Equal(provisionalDays, daily.TextArgs["provisionalDays"]);
        Assert.Equal(month.Length == 0 ? null : month, daily.TextArgs["period"]);
        Assert.Equal(["period", "provisionalDays", "windowDays"], daily.TextArgs.Keys.Order(StringComparer.Ordinal));
    }

    // ---- what it refuses -------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(" ")]
    [InlineData("2026-13")]
    [InlineData("2026-00")]
    [InlineData("2026-7")]
    [InlineData("202607")]
    [InlineData("2026-07 ")]
    [InlineData(" 2026-07")]
    [InlineData("../x")]
    [InlineData("2026-07-01")]
    [InlineData("juli")]
    public async Task A_month_that_is_not_exactly_YYYY_MM_is_refused_before_Google_is_asked(string month)
    {
        var rig = await RigAsync();

        var problem = Problem(await rig.Controller.GetDaily(month, Ct), 400);

        Assert.Equal(ApiCodes.PeriodInvalid, Code(problem));
        Assert.Empty(rig.Daily.ReceivedCalls());
    }

    [Theory]
    [InlineData("2025-09", true)]  // the oldest month the picker offers
    [InlineData("2025-08", false)] // one before it
    [InlineData("2026-08", true)]  // the running month
    [InlineData("2026-09", false)] // one after it
    [InlineData("0001-01", false)]
    [InlineData("9999-12", false)]
    public async Task Only_a_month_the_picker_offers_is_asked_for(string month, bool allowed)
    {
        var rig = await RigAsync();

        var result = await rig.Controller.GetDaily(month, Ct);

        if (allowed)
        {
            Assert.Equal(month, Ok<DailyResponse>(result).Window.Month);
        }
        else
        {
            Assert.Equal(ApiCodes.DateOutOfRange, Code(Problem(result, 400)));
            Assert.Empty(rig.Daily.ReceivedCalls()); // a month outside what the picker offers is no reason to spend a Google call
        }
    }

    [Fact]
    public async Task The_oldest_month_moves_with_the_clock()
    {
        var rig = await RigAsync(now: At("2026-09-01"));

        Assert.Equal(ApiCodes.DateOutOfRange, Code(Problem(await rig.Controller.GetDaily("2025-09", Ct), 400)));
        Assert.Equal("2025-10", (await DailyAsync(rig, "2025-10")).Window.Month);
    }

    [Fact]
    public async Task A_month_the_service_refuses_by_name_is_a_400_periodInvalid()
    {
        // The service checks the month too. Whatever it refuses by the parameter's name is the caller's month.
        var rig = await RigAsync();
        rig.Daily.GetSeriesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ArgumentException($"rejected {FakeServiceAccount.LeakMarker}", "month"));

        var problem = Problem(await rig.Controller.GetDaily("2026-06", Ct), 400);

        Assert.Equal(ApiCodes.PeriodInvalid, Code(problem));
        Assert.DoesNotContain(FakeServiceAccount.LeakMarker, ManagementJson.Serialize(problem));
    }

    [Fact]
    public async Task An_argument_error_about_anything_else_is_not_the_callers_month_and_is_a_failure()
    {
        var rig = await RigAsync();
        rig.Daily.GetSeriesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ArgumentOutOfRangeException("months", $"deep inside {FakeServiceAccount.LeakMarker}"));

        var problem = Problem(await rig.Controller.GetDaily("2026-06", Ct), 502);

        Assert.Equal(ErrorCodes.Unknown, Code(problem));
        Assert.Contains("ArgumentOutOfRangeException", Assert.Single(rig.ControllerLog.Entries, e => e.Level == LogLevel.Warning).Text);
        Assert.DoesNotContain(FakeServiceAccount.LeakMarker, rig.AllLogText);
    }

    [Theory]
    [InlineData("notConfigured", 409, ErrorCodes.NotConfigured)]
    [InlineData("keyUnreadable", 409, ErrorCodes.KeyUnreadable)]
    [InlineData("keyInvalid", 409, ErrorCodes.KeyInvalid)]
    [InlineData("apiDisabled", 502, ErrorCodes.ApiDisabled)]
    [InlineData("noAccess", 502, ErrorCodes.NoAccess)]
    [InlineData("propertyNotFound", 502, ErrorCodes.PropertyNotFound)]
    [InlineData("quota", 502, ErrorCodes.Quota)]
    [InlineData("serverError", 502, ErrorCodes.Unknown)]
    [InlineData("surprise", 502, ErrorCodes.Unknown)]
    [InlineData("timeout", 502, ErrorCodes.Unknown)]
    [InlineData("unlistedCode", 502, ErrorCodes.Unknown)]
    public async Task A_failure_to_reach_the_data_is_a_problem_with_a_code_and_none_of_the_exception_text(string kind, int status, string code)
    {
        var rig = await RigAsync();
        var failure = await GoogleFailures.FailureOf(kind);
        rig.Daily.GetSeriesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).ThrowsAsync(failure);

        var problem = Problem(await rig.Controller.GetDaily(null, Ct), status);

        Assert.Equal(code, Code(problem));
        Assert.Equal(rig.Http.TraceIdentifier, problem.Extensions["traceId"]);
        Assert.DoesNotContain(FakeServiceAccount.LeakMarker, ManagementJson.Serialize(problem));
        Assert.DoesNotContain(FakeServiceAccount.LeakMarker, rig.AllLogText);
    }

    [Fact]
    public async Task A_caller_that_went_away_is_not_a_failure_to_report()
    {
        var rig = await RigAsync();
        using var left = new CancellationTokenSource();
        left.Cancel();
        rig.Daily.GetSeriesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).ThrowsAsync(new OperationCanceledException(left.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rig.Controller.GetDaily(null, left.Token));

        Assert.Empty(rig.ControllerLog.Entries);
    }

    [Fact]
    public async Task The_daily_endpoints_never_start_the_backfill_or_read_the_archive()
    {
        var rig = await RigAsync();

        await rig.Controller.GetDaily(null, Ct);
        await rig.Controller.GetDaily("2026-06", Ct);
        await rig.Controller.GetDaily("2026-13", Ct);
        await rig.Controller.GetDailySources("2026-08-05", Ct);
        await rig.Controller.GetDailySources("nonsense", Ct);

        Assert.Empty(rig.Spy.Starts);
        Assert.Empty(rig.Reports.Refreshed);
        Assert.Equal(0, rig.Reports.ReportReads);
    }

    // ---- with the real service behind it --------------------------------------------------------------------------------------

    [Fact]
    public async Task Through_the_real_service_the_curve_is_one_Google_call_and_the_second_request_is_served_from_its_cache()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(RealDaily: true));

        var first = Ok<DailyResponse>(await rig.Controller.GetDaily(null, Ct));
        var second = Ok<DailyResponse>(await rig.Controller.GetDaily("", Ct)); // the same question as none

        Assert.Single(rig.DailyGoogle!.Ga4.Calls);
        Assert.Equal(new DailyWindowResponse("2026-07-21", "2026-08-19", null), first.Window);
        Assert.Equal(SyntheticGoogle.DailySessions(new DateOnly(2026, 7, 21)), first.Days[0].Sessions);
        Assert.Equal(first.Total, second.Total);
        Assert.Empty(rig.Spy.Starts);
    }

    [Fact]
    public async Task Through_the_real_service_the_sources_of_a_day_are_what_Google_answered()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(RealDaily: true));

        var sources = Ok<DailySourcesResponse>(await rig.Controller.GetDailySources("2026-08-05", Ct));

        Assert.Equal("2026-08-05", sources.Date);
        Assert.NotEmpty(sources.Sources);
        Assert.All(sources.Sources, row => Assert.True(row.Sessions > 0));
        Assert.Single(rig.DailyGoogle!.Ga4.Calls);
    }

    [Fact]
    public async Task Through_the_real_service_a_package_that_is_not_set_up_is_a_409_and_Google_is_not_asked()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: false, RealDaily: true));

        Assert.Equal(ErrorCodes.NotConfigured, Code(Problem(await rig.Controller.GetDaily(null, Ct), 409)));
        Assert.Equal(ErrorCodes.NotConfigured, Code(Problem(await rig.Controller.GetDailySources("2026-08-05", Ct), 409)));
        Assert.Empty(rig.DailyGoogle!.Ga4.Calls);
    }

    [Fact]
    public async Task Through_the_real_service_an_error_from_Google_is_a_502_with_its_code_and_is_not_kept()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(RealDaily: true));
        rig.DailyGoogle!.Ga4Failure = System.Net.HttpStatusCode.Forbidden;

        var problem = Problem(await rig.Controller.GetDaily(null, Ct), 502);
        rig.DailyGoogle.Ga4Failure = null;
        var recovered = Ok<DailyResponse>(await rig.Controller.GetDaily(null, Ct)); // an error is not cached: the next request asks again

        Assert.Equal(ErrorCodes.NoAccess, Code(problem)); // a 403 PERMISSION_DENIED from GA4: the account has no access to the property
        Assert.Equal(502, problem.Status);
        Assert.Equal(30, recovered.Days.Length);
    }

    // ---- the sources of one day ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_sources_of_a_day_are_the_rows_the_service_gave_with_the_date_in_its_one_form()
    {
        var rig = await RigAsync();

        var sources = Ok<DailySourcesResponse>(await rig.Controller.GetDailySources("2026-08-05", Ct));

        Assert.Equal("2026-08-05", sources.Date);
        Assert.Equal([new DailySourceRow("Organic Search", "google", 120), new DailySourceRow("Email", "newsletter", 40)], sources.Sources);
        await rig.Daily.Received(1).GetSourcesAsync(new DateOnly(2026, 8, 5), Ct);
    }

    [Fact]
    public async Task A_day_with_no_sources_is_an_empty_list()
    {
        var rig = await RigAsync();
        rig.Daily.GetSourcesAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Ga4DailySource>>([]));

        Assert.Empty(Ok<DailySourcesResponse>(await rig.Controller.GetDailySources("2026-08-05", Ct)).Sources);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("2026-8-5")]
    [InlineData("2026-08-5")]
    [InlineData("20260805")]
    [InlineData("2026-08-05 ")]
    [InlineData(" 2026-08-05")]
    [InlineData("2026-02-30")]
    [InlineData("2026-13-01")]
    [InlineData("2026-08")]
    [InlineData("2026-08-05T00:00:00")]
    [InlineData("05/08/2026")]
    [InlineData("yesterday")]
    [InlineData("../2026-08-05")]
    public async Task A_date_that_is_not_exactly_YYYY_MM_DD_is_refused_before_Google_is_asked(string? date)
    {
        var rig = await RigAsync();

        var problem = Problem(await rig.Controller.GetDailySources(date, Ct), 400);

        Assert.Equal(ApiCodes.DateInvalid, Code(problem));
        Assert.Empty(rig.Daily.ReceivedCalls());
    }

    [Theory]
    [InlineData("2026-08-20", "2025-09-01", true)]  // the first day of the oldest month the picker offers
    [InlineData("2026-08-20", "2025-08-31", false)] // the day before it
    [InlineData("2026-08-20", "2026-08-20", true)]  // today
    [InlineData("2026-08-20", "2026-08-21", false)] // tomorrow
    [InlineData("2026-08-20", "0001-01-01", false)]
    [InlineData("2026-08-20", "9999-12-31", false)]
    [InlineData("2026-09-01", "2025-09-30", false)] // the oldest month moves on with the calendar
    [InlineData("2026-09-01", "2025-10-01", true)]
    [InlineData("2026-03-01", "2025-04-01", true)]
    [InlineData("2026-03-01", "2025-03-31", false)]
    public async Task Only_a_day_the_picker_can_show_is_asked_for(string today, string date, bool allowed)
    {
        var rig = await RigAsync(now: At(today));

        var result = await rig.Controller.GetDailySources(date, Ct);

        if (allowed)
        {
            Assert.Equal(date, Ok<DailySourcesResponse>(result).Date);
            await rig.Daily.Received(1).GetSourcesAsync(DateOnly.ParseExact(date, "yyyy-MM-dd"), Ct);
        }
        else
        {
            Assert.Equal(ApiCodes.DateOutOfRange, Code(Problem(result, 400)));
            Assert.Empty(rig.Daily.ReceivedCalls());
        }
    }

    [Theory]
    [InlineData("notConfigured", 409, ErrorCodes.NotConfigured)]
    [InlineData("keyInvalid", 409, ErrorCodes.KeyInvalid)]
    [InlineData("noAccess", 502, ErrorCodes.NoAccess)]
    [InlineData("quota", 502, ErrorCodes.Quota)]
    [InlineData("surprise", 502, ErrorCodes.Unknown)]
    public async Task A_failure_to_reach_the_sources_is_a_problem_with_a_code_and_none_of_the_exception_text(string kind, int status, string code)
    {
        var rig = await RigAsync();
        rig.Daily.GetSourcesAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).ThrowsAsync(await GoogleFailures.FailureOf(kind));

        var problem = Problem(await rig.Controller.GetDailySources("2026-08-05", Ct), status);

        Assert.Equal(code, Code(problem));
        Assert.DoesNotContain(FakeServiceAccount.LeakMarker, ManagementJson.Serialize(problem));
        Assert.DoesNotContain(FakeServiceAccount.LeakMarker, rig.AllLogText);
    }
}
