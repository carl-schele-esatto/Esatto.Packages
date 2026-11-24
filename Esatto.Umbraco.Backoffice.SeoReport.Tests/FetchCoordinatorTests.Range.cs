using Esatto.Umbraco.Backoffice.SeoReport.Jobs;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The one <see cref="ArgumentOutOfRangeException"/> a run forgives. The service refuses a month outside what can be archived NOW
/// (later than its expected latest month, or before the first month Search Console still has) with <c>ParamName == "period"</c>, and
/// the run's bounds were worked out once at its start, so a month can fall out of them while the run goes (the clock rolls over a
/// month). Such a month is skipped, not failed. ANY other <see cref="ArgumentOutOfRangeException"/> (another parameter, or a month
/// that is inside the bounds, so a bug deep in a fetch) is a failure like any other with no code: Warning, month not done, and the
/// record that holds the scheduled job back, because the month stays missing and would otherwise be fetched again at every wake-up.
/// </summary>
public partial class FetchCoordinatorTests
{
    private static ArgumentOutOfRangeException OutOfRange(string? paramName = "period") => new(paramName, "fixed text");

    // ---------------------------------------------------------------------------------------------------------------
    // Skipped: the month really is outside the bounds as they are NOW
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_month_that_fell_out_of_search_consoles_window_while_the_run_went_is_skipped_not_failed_and_the_run_goes_on()
    {
        var rig = await Rig.ReadyAsync();
        rig.Reports.OnRefresh = (period, _) =>
        {
            // The first month takes four months (on 2026-12-18 the first month inside the window is 2025-09)...
            if (period == Period.Parse("2026-07"))
            {
                rig.Time.Advance(TimeSpan.FromDays(120));
                return Task.CompletedTask;
            }

            // ...so the service now refuses the two oldest.
            return period < Period.Parse("2025-09") ? Task.FromException(OutOfRange()) : Task.CompletedTask;
        };

        await rig.RunAsync();

        Assert.Equal(13, rig.Reports.Refreshed.Count);
        Assert.Equal(new FetchProgress(false, 13, 13, null, null), rig.Coordinator.Progress);
        var state = await rig.StateAsync();
        Assert.NotNull(state.LastSuccessUtc);
        Assert.Null(state.LastFailureUtc);
        Assert.Contains("2025-08", Assert.Single(rig.Log.Entries, e => e.Level == LogLevel.Information).Text);   // once, for the first
        Assert.DoesNotContain(rig.Log.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task A_month_that_is_later_than_the_latest_the_service_now_expects_is_skipped_not_failed()
    {
        var rig = await Rig.ReadyAsync();
        rig.Reports.OnRefresh = (period, _) =>
        {
            if (period == Period.Parse("2026-07"))
            {
                rig.Reports.ExpectedLatestPeriod = Period.Parse("2026-04");   // the clock of the service moved back (another instance, a setting)
                return Task.CompletedTask;
            }

            return period > Period.Parse("2026-04") ? Task.FromException(OutOfRange()) : Task.CompletedTask;
        };

        await rig.RunAsync();

        Assert.Equal(13, rig.Reports.Refreshed.Count);
        Assert.Equal(new FetchProgress(false, 13, 13, null, null), rig.Coordinator.Progress);
        Assert.Null((await rig.StateAsync()).LastFailureUtc);
        Assert.Contains("2026-06", Assert.Single(rig.Log.Entries, e => e.Level == LogLevel.Information).Text);
        Assert.DoesNotContain(rig.Log.Entries, e => e.Level >= LogLevel.Warning);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Failed: nothing says the month is out of range, so the error is a bug and the month still has to be fetched
    // ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("period")]   // the name the service uses, but this month is inside the bounds
    [InlineData("index")]
    [InlineData(null)]
    public async Task An_argument_error_for_a_month_inside_the_range_is_a_failed_month_and_not_a_skipped_one(string? paramName)
    {
        var rig = await Rig.ReadyAsync();
        FailOn(rig, ThirdMonth, OutOfRange(paramName));

        await rig.RunAsync();

        Assert.Equal(13, rig.Reports.Refreshed.Count);
        Assert.Equal(new FetchProgress(false, 12, 13, null, null), rig.Coordinator.Progress);   // the month is not done
        var state = await rig.StateAsync();
        Assert.Equal("unknown", state.LastErrorCode);
        Assert.NotNull(state.LastFailureUtc);
        Assert.True(state.IsBackingOff(rig.Time.GetUtcNow()));
        Assert.Contains(rig.Log.Entries, e => e.Level == LogLevel.Warning && e.Text.Contains("ArgumentOutOfRangeException") && e.Text.Contains("2026-05"));
        Assert.DoesNotContain(rig.Log.Entries, e => e.Level == LogLevel.Information);   // not "can no longer be fetched"
    }

    [Fact]
    public async Task An_argument_error_for_another_parameter_is_a_failure_even_when_the_month_is_outside_the_range()
    {
        var rig = await Rig.ReadyAsync();
        rig.Reports.OnRefresh = (period, _) =>
        {
            if (period == Period.Parse("2026-07"))
            {
                rig.Time.Advance(TimeSpan.FromDays(120));   // 2025-08 and 2025-07 are now outside the window...
                return Task.CompletedTask;
            }

            return period < Period.Parse("2025-09") ? Task.FromException(OutOfRange("index")) : Task.CompletedTask;   // ...but this is not the service's check
        };

        await rig.RunAsync();

        Assert.Equal(new FetchProgress(false, 11, 13, null, null), rig.Coordinator.Progress);
        Assert.Equal("unknown", (await rig.StateAsync()).LastErrorCode);
        Assert.Equal(2, rig.Log.Entries.Count(e => e.Level == LogLevel.Warning));
    }

    [Fact]
    public async Task The_latest_expected_month_itself_is_inside_the_range_so_an_argument_error_for_it_is_a_failure()
    {
        var rig = await Rig.ReadyAsync();   // the latest expected month is 2026-07
        FailOn(rig, Period.Parse("2026-07"), OutOfRange());

        await rig.RunAsync();

        Assert.Equal(new FetchProgress(false, 12, 13, null, null), rig.Coordinator.Progress);
        Assert.Equal("unknown", (await rig.StateAsync()).LastErrorCode);
        Assert.DoesNotContain(rig.Log.Entries, e => e.Level == LogLevel.Information);
    }

    [Fact]
    public async Task The_first_month_search_console_still_has_is_inside_the_range_so_an_argument_error_for_it_is_a_failure()
    {
        var rig = await Rig.ReadyAsync();
        rig.Time.SetUtcNow(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero));   // the window now starts at 2025-07, the oldest wanted month
        FailOn(rig, Period.Parse("2025-07"), OutOfRange());

        await rig.RunAsync();

        Assert.Equal(13, rig.Reports.Refreshed.Count);
        Assert.Equal(new FetchProgress(false, 12, 13, null, null), rig.Coordinator.Progress);
        Assert.Equal("unknown", (await rig.StateAsync()).LastErrorCode);
        Assert.DoesNotContain(rig.Log.Entries, e => e.Level == LogLevel.Information);
    }
}
