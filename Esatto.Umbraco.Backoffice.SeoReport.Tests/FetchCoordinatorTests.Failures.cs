using System.Net;
using Esatto.Umbraco.Backoffice.SeoReport.Jobs;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Google;
using Google.Apis.AnalyticsData.v1beta.Data;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// What a failing month does to a run: some codes end it (another month would fail the same way), the rest skip the month; the key
/// disappearing mid-run; an unexpected error; and the application shutting down. No exception ever escapes the background task, and
/// the lock is released on every path. Failures are made the way production makes them (a real Google client over a fake transport).
/// </summary>
public partial class FetchCoordinatorTests
{
    private const string Marker = FakeServiceAccount.LeakMarker;

    private static readonly Period ThirdMonth = Period.Parse("2026-05");

    private static async Task<GoogleApiException> GoogleFailureAsync(HttpStatusCode status, string body)
    {
        var fake = new FakeAnalyticsData(new FakeGoogleTransport(_ => new FakeGoogleTransport.Answer(status, body)));
        return await Assert.ThrowsAsync<GoogleApiException>(
            () => fake.Service.Properties.RunReport(new RunReportRequest(), "properties/123456789").ExecuteAsync());
    }

    private static async Task<Exception> FailureForAsync(string code) => code switch
    {
        "quota" => await GoogleFailureAsync(HttpStatusCode.TooManyRequests, GoogleErrorBodies.Error(429, "RESOURCE_EXHAUSTED")),
        "noAccess" => await GoogleFailureAsync(HttpStatusCode.Forbidden, GoogleErrorBodies.Denied()),
        "apiDisabled" => await GoogleFailureAsync(HttpStatusCode.Forbidden, GoogleErrorBodies.Disabled()),
        "propertyNotFound" => await GoogleFailureAsync(HttpStatusCode.NotFound, GoogleErrorBodies.Error(404, "NOT_FOUND")),
        _ => new ReportFetchException(code),   // keyUnreadable, keyInvalid, notConfigured
    };

    /// <summary>Makes the refresh of <paramref name="failing"/> fail with <paramref name="failure"/>; every other month succeeds.</summary>
    private static void FailOn(Rig rig, Period failing, Exception failure)
        => rig.Reports.OnRefresh = (period, _) => period == failing ? Task.FromException(failure) : Task.CompletedTask;

    // ---------------------------------------------------------------------------------------------------------------
    // The codes that end the run
    // ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("quota")]
    [InlineData("noAccess")]
    [InlineData("apiDisabled")]
    [InlineData("propertyNotFound")]
    [InlineData("keyUnreadable")]
    [InlineData("keyInvalid")]
    [InlineData("notConfigured")]
    public async Task These_failures_end_the_run_because_another_month_would_fail_the_same_way(string code)
    {
        var rig = await Rig.ReadyAsync();
        FailOn(rig, ThirdMonth, await FailureForAsync(code));

        await rig.RunAsync();

        Assert.Equal(["2026-07", "2026-06", "2026-05"], rig.Reports.Refreshed.Select(p => p.ToString()));
        Assert.Equal(new FetchProgress(false, 2, 13, null, code), rig.Coordinator.Progress);
        Assert.True(await rig.LockIsFreeAsync());
        var state = await rig.StateAsync();
        Assert.Equal(code, state.LastErrorCode);
        Assert.Equal(ReadySettingsHarness.Now, state.LastFailureUtc);
        Assert.Equal(ReadySettingsHarness.Now, state.LastSuccessUtc);   // two months were archived before it
        Assert.True(rig.Coordinator.CurrentRun!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task A_failure_in_the_very_first_month_records_no_success()
    {
        var rig = await Rig.ReadyAsync();
        FailOn(rig, Period.Parse("2026-07"), await FailureForAsync("quota"));

        await rig.RunAsync();

        Assert.Equal(new FetchProgress(false, 0, 13, null, "quota"), rig.Coordinator.Progress);
        var state = await rig.StateAsync();
        Assert.Null(state.LastSuccessUtc);
        Assert.Equal(ReadySettingsHarness.Now, state.LastAttemptUtc);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The failures that only skip a month
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_failure_with_no_code_skips_that_month_and_the_run_goes_on()
    {
        var rig = await Rig.ReadyAsync();
        FailOn(rig, ThirdMonth, new InvalidOperationException($"boom {Marker}"));

        await rig.RunAsync();

        Assert.Equal(ThirteenMonths, rig.Reports.Refreshed.Select(p => p.ToString()));
        // The failed month is not done; the run itself ended well, so there is no error code on it.
        Assert.Equal(new FetchProgress(false, 12, 13, null, null), rig.Coordinator.Progress);
        var state = await rig.StateAsync();
        Assert.Equal("unknown", state.LastErrorCode);
        Assert.NotNull(state.LastFailureUtc);
        Assert.NotNull(state.LastSuccessUtc);
        Assert.True(await rig.LockIsFreeAsync());
    }

    [Fact]
    public async Task When_every_month_that_was_tried_failed_the_last_code_is_the_final_error()
    {
        var rig = await Rig.ReadyAsync();
        rig.Reports.OnRefresh = (_, _) => Task.FromException(new HttpRequestException("no route"));

        await rig.RunAsync();

        Assert.Equal(13, rig.Reports.Refreshed.Count);
        Assert.Equal(new FetchProgress(false, 0, 13, null, "unknown"), rig.Coordinator.Progress);
        var state = await rig.StateAsync();
        Assert.Null(state.LastSuccessUtc);
        Assert.Equal("unknown", state.LastErrorCode);
    }

    [Fact]
    public async Task A_timeout_that_is_not_the_application_stopping_is_a_failed_month_and_not_a_cancelled_run()
    {
        var rig = await Rig.ReadyAsync();
        FailOn(rig, ThirdMonth, new TaskCanceledException("the request timed out"));

        await rig.RunAsync();

        Assert.Equal(13, rig.Reports.Refreshed.Count);
        Assert.Equal(new FetchProgress(false, 12, 13, null, null), rig.Coordinator.Progress);
        Assert.Equal("unknown", (await rig.StateAsync()).LastErrorCode);
    }

    [Fact]
    public async Task A_failure_with_no_code_is_logged_as_a_warning_with_the_exception_type_and_status_and_a_coded_one_only_by_its_code()
    {
        var unknown = await GoogleFailureAsync(HttpStatusCode.InternalServerError, GoogleErrorBodies.Error(500, "INTERNAL"));
        var rig = await Rig.ReadyAsync();
        FailOn(rig, ThirdMonth, unknown);

        await rig.RunAsync();

        var warning = Assert.Single(rig.Log.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("GoogleApiException", warning.Text);
        Assert.Contains("500", warning.Text);
        Assert.Contains("2026-05", warning.Text);

        var coded = await Rig.ReadyAsync();
        FailOn(coded, ThirdMonth, await FailureForAsync("noAccess"));

        await coded.RunAsync();

        var information = Assert.Single(coded.Log.Entries, e => e.Level == LogLevel.Information);
        Assert.Contains("noAccess", information.Text);
        Assert.DoesNotContain(coded.Log.Entries, e => e.Level >= LogLevel.Warning);
        Assert.DoesNotContain(Marker, coded.Log.AllText);   // the text of Google's answer is not logged for a failure it has a code for
    }

    [Fact]
    public async Task Neither_the_progress_nor_the_record_carries_the_text_of_an_exception()
    {
        var rig = await Rig.ReadyAsync();
        FailOn(rig, ThirdMonth, new InvalidOperationException($"boom {Marker}"));

        await rig.RunAsync("Test User Name");

        Assert.DoesNotContain(Marker, rig.Coordinator.Progress!.ToString());
        Assert.DoesNotContain(Marker, rig.Store.Documents[DocumentKeys.FetchState]);
    }

    [Fact]
    public async Task A_manual_start_is_not_held_back_by_an_earlier_failure()
    {
        var rig = await Rig.ReadyAsync();
        FailOn(rig, Period.Parse("2026-07"), await FailureForAsync("quota"));
        await rig.RunAsync();
        Assert.Equal("quota", rig.Coordinator.Progress!.ErrorCode);

        rig.Reports.OnRefresh = (_, _) => Task.CompletedTask;

        Assert.Equal(FetchStartResult.Started, await rig.RunAsync("Test User"));
        Assert.Equal(new FetchProgress(false, 13, 13, null, null), rig.Coordinator.Progress);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The key goes away in the middle of a run
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_key_that_disappears_in_the_middle_of_a_run_ends_it_with_notConfigured_and_nothing_escapes()
    {
        // The setup check at the start passed; the refresh of the fourth month finds there is no key any more.
        var rig = await Rig.ReadyAsync();
        FailOn(rig, Period.Parse("2026-04"), new ReportFetchException("notConfigured"));

        await rig.RunAsync();

        Assert.Equal(4, rig.Reports.Refreshed.Count);   // the remaining months are not tried
        Assert.Equal(new FetchProgress(false, 3, 13, null, "notConfigured"), rig.Coordinator.Progress);
        Assert.True(rig.Coordinator.CurrentRun!.IsCompletedSuccessfully);
        Assert.True(await rig.LockIsFreeAsync());
        Assert.Equal("notConfigured", (await rig.StateAsync()).LastErrorCode);
    }

    [Fact]
    public async Task The_setup_is_checked_again_before_every_month_so_a_removed_key_is_noticed_before_Google_is_asked()
    {
        var rig = await Rig.ReadyAsync();
        rig.Reports.OnRefresh = async (period, _) =>
        {
            if (period == Period.Parse("2026-06"))
            {
                await rig.Settings.Settings.SaveAsync(
                    new SeoReportSettings { Ga4PropertyId = ReadySettingsHarness.Ga4Property, SearchConsoleSite = ReadySettingsHarness.Site },
                    newKeyJson: null,
                    removeKey: true);
            }
        };

        await rig.RunAsync();

        Assert.Equal(["2026-07", "2026-06"], rig.Reports.Refreshed.Select(p => p.ToString()));
        Assert.Equal(new FetchProgress(false, 2, 13, null, "notConfigured"), rig.Coordinator.Progress);
        Assert.True(await rig.LockIsFreeAsync());
        Assert.Equal("notConfigured", (await rig.StateAsync()).LastErrorCode);
    }

    [Fact]
    public async Task A_key_that_becomes_unreadable_in_the_middle_of_a_run_ends_it_with_keyUnreadable()
    {
        var other = new SettingsHarness();   // another key ring: what a restored backup looks like
        await other.SaveKeyAsync(FakeServiceAccount.Json());
        var rig = await Rig.ReadyAsync();
        rig.Reports.OnRefresh = async (period, _) =>
        {
            if (period != Period.Parse("2026-07")) return;

            await rig.Store.SaveDocumentAsync(DocumentKeys.ProtectedKey, other.Store.Documents[DocumentKeys.ProtectedKey]);
            rig.Time.Advance(TimeSpan.FromMinutes(2));   // the settings are cached for a minute
        };

        await rig.RunAsync();

        Assert.Equal(["2026-07"], rig.Reports.Refreshed.Select(p => p.ToString()));
        Assert.Equal(new FetchProgress(false, 1, 13, null, "keyUnreadable"), rig.Coordinator.Progress);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Surprises
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_unexpected_error_ends_the_run_as_unknown_and_nothing_escapes_the_background_task()
    {
        var rig = await Rig.ReadyAsync();
        rig.Reports.NeedsFetchFails = new InvalidOperationException($"the store broke {Marker}");

        await rig.RunAsync();

        Assert.Equal(new FetchProgress(false, 0, 0, null, "unknown"), rig.Coordinator.Progress);
        Assert.True(rig.Coordinator.CurrentRun!.IsCompletedSuccessfully);
        Assert.True(await rig.LockIsFreeAsync());
        Assert.Equal("unknown", (await rig.StateAsync()).LastErrorCode);
        Assert.Contains(rig.Log.Entries, e => e.Level == LogLevel.Warning && e.Text.Contains("InvalidOperationException"));
    }

    [Fact]
    public async Task A_store_that_cannot_release_the_lock_or_save_the_record_does_not_break_the_run_or_the_next_start()
    {
        FlakyStore? flaky = null;
        var rig = await Rig.ReadyAsync(inner => flaky = new FlakyStore(inner));
        flaky!.ReleaseLockFails = new InvalidOperationException("the database does not answer");
        flaky.SaveDocumentFails = new InvalidOperationException("the database does not answer");

        await rig.RunAsync();

        Assert.True(rig.Coordinator.CurrentRun!.IsCompletedSuccessfully);
        Assert.Equal(new FetchProgress(false, 13, 13, null, null), rig.Coordinator.Progress);
        Assert.Equal(2, rig.Log.Entries.Count(e => e.Level == LogLevel.Warning));

        // The lock stayed in the database (it lapses by itself), but it is ours: this process may start again.
        flaky.ReleaseLockFails = null;
        flaky.SaveDocumentFails = null;
        Assert.Equal(FetchStartResult.Started, await rig.RunAsync());
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The application stops
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task When_the_application_stops_the_month_in_flight_is_cancelled_the_lock_is_released_and_the_progress_stays_coherent()
    {
        var rig = await Rig.ReadyAsync();
        var entered = NewGate();
        rig.Reports.OnRefresh = async (period, ct) =>
        {
            if (period != Period.Parse("2026-06")) return;

            entered.SetResult();
            await Task.Delay(Timeout.Infinite, ct);   // a Google call that only ends when it is cancelled
        };
        Assert.Equal(FetchStartResult.Started, rig.Coordinator.TryStart("Test User"));
        await entered.Task.WaitAsync(Bounded);

        rig.Lifetime.StopApplication();
        await rig.FinishedAsync();

        Assert.Equal(["2026-07", "2026-06"], rig.Reports.Refreshed.Select(p => p.ToString()));   // nothing after the cancelled one
        Assert.Equal(new FetchProgress(false, 1, 13, null, null), rig.Coordinator.Progress);   // a shutdown is not an error
        Assert.True(rig.Coordinator.CurrentRun!.IsCompletedSuccessfully);
        Assert.True(await rig.LockIsFreeAsync());
        var state = await rig.StateAsync();
        Assert.Null(state.LastFailureUtc);   // the interrupted month is not a failure
        Assert.NotNull(state.LastSuccessUtc);
    }

    [Fact]
    public async Task A_run_started_while_the_application_is_stopping_tries_nothing_and_lets_go_of_the_lock()
    {
        var rig = await Rig.ReadyAsync();
        rig.Lifetime.StopApplication();

        Assert.Equal(FetchStartResult.Started, await rig.RunAsync());

        Assert.Empty(rig.Reports.Refreshed);
        Assert.False(rig.Coordinator.Progress!.Running);
        Assert.Null(rig.Coordinator.Progress.ErrorCode);
        Assert.True(await rig.LockIsFreeAsync());
        Assert.False(rig.Store.Documents.ContainsKey(DocumentKeys.FetchState));
    }
}
