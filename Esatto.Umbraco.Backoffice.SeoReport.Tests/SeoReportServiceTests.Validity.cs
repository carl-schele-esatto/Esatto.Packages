using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// What an archived report is valid for. It is valid for the GA4 property and the Search Console site it was built from, and for
/// the months a fetch can honestly cover: a report built for another property counts as missing (a wrong but valid id would
/// otherwise archive another site's data for good), a month that is not complete or no longer inside Search Console's window is
/// not fetched at all, and a refresh that waited is only answered by a refresh made with the same settings.
/// </summary>
public partial class SeoReportServiceTests
{
    private const string OtherGa4 = "properties/987654321";

    private const string OtherSite = "sc-domain:other.example.com";

    private const string OutOfRangeMessage =
        "The month is later than the latest month that should be archived, or earlier than the months Search Console keeps.";

    // ---------------------------------------------------------------------------------------------------------------
    // A report is only valid for the settings it was built from
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task AReport_IsServed_WhileTheSettingsItWasBuiltForStayInForce()
    {
        var rig = await Rig.ReadyAsync();
        await rig.SeedAsync("2026-07", marker: "july");
        await rig.UseAsync(); // saved again with the same values

        Assert.Equal("july", (await rig.Service.GetReportAsync("2026-07"))!.GeneratedAt);
        Assert.NotNull(await rig.Service.GetFetchedUtcAsync("2026-07"));
        Assert.Equal(["2026-07"], await rig.Service.GetAvailablePeriodsAsync());
        Assert.False(await rig.Service.NeedsFetchAsync(July));
        Assert.True(await rig.Service.IsUpToDateAsync());
    }

    [Theory]
    [InlineData(OtherGa4, ReadySettingsHarness.Site)]
    [InlineData(ReadySettingsHarness.Ga4Property, OtherSite)]
    [InlineData(OtherGa4, OtherSite)]
    public async Task AMonthBuiltForAnotherProperty_CountsAsMissingEverywhere(string ga4Property, string site)
    {
        var rig = await Rig.ReadyAsync();
        await rig.SeedAsync("2026-06");
        await rig.SeedAsync("2026-07");

        await rig.UseAsync(ga4Property, site);

        Assert.True(await rig.Service.NeedsFetchAsync(July));
        Assert.True(await rig.Service.NeedsFetchAsync(June));
        Assert.Null(await rig.Service.GetReportAsync("2026-07"));
        Assert.Null(await rig.Service.GetFetchedUtcAsync("2026-07"));
        Assert.Empty(await rig.Service.GetAvailablePeriodsAsync());
        Assert.Null(await rig.Service.GetReportAsync());      // nothing matches, so there is no newest
        Assert.Null(await rig.Service.GetFetchedUtcAsync());
        Assert.False(await rig.Service.IsUpToDateAsync());
    }

    [Fact]
    public async Task TheNewestReport_IsTheNewestOneBuiltForTheSettingsInForce()
    {
        var rig = await Rig.ReadyAsync();
        var may = new DateTimeOffset(2026, 6, 6, 5, 0, 0, TimeSpan.Zero);
        await rig.SeedAsync("2026-05", marker: "may", fetched: may, ga4Property: OtherGa4);
        await rig.SeedAsync("2026-06", marker: "june");
        await rig.SeedAsync("2026-07", marker: "july");

        await rig.UseAsync(OtherGa4);

        Assert.Equal(["2026-05"], await rig.Service.GetAvailablePeriodsAsync());
        Assert.Equal("may", (await rig.Service.GetReportAsync())!.GeneratedAt);
        Assert.Equal(may, await rig.Service.GetFetchedUtcAsync());
    }

    [Fact]
    public async Task OnlyTheMonthsBuiltForTheSettingsInForce_AreListed_OldestFirst()
    {
        var rig = await Rig.ReadyAsync();
        await rig.SeedAsync("2026-07");
        await rig.SeedAsync("2026-05", ga4Property: OtherGa4);
        await rig.SeedAsync("2026-06", site: OtherSite);
        await rig.SeedAsync("2026-04");

        Assert.Equal(["2026-04", "2026-07"], await rig.Service.GetAvailablePeriodsAsync());
        Assert.Equal(July, rig.Service.ExpectedLatestPeriod);
        Assert.False(await rig.Service.NeedsFetchAsync(July));
        Assert.True(await rig.Service.NeedsFetchAsync(June));
    }

    [Fact]
    public async Task WhenTheSettingsComeBack_TheArchivedMonthsAreValidAgain_WithoutAFetch()
    {
        var rig = await Rig.ReadyAsync();
        await rig.SeedAsync("2026-07", marker: "july");

        await rig.UseAsync(OtherGa4);
        Assert.Null(await rig.Service.GetReportAsync("2026-07"));
        Assert.True(await rig.Service.NeedsFetchAsync(July));

        await rig.UseAsync();
        Assert.Equal("july", (await rig.Service.GetReportAsync("2026-07"))!.GeneratedAt);
        Assert.False(await rig.Service.NeedsFetchAsync(July));
        Assert.Equal(["2026-07"], await rig.Service.GetAvailablePeriodsAsync());

        Assert.Equal(0, rig.Store.ReportWrites);
        Assert.Equal(0, rig.Google.Ga4ServiceRequests);
        Assert.Equal(0, rig.Google.SearchConsoleServiceRequests);
    }

    [Fact]
    public async Task ACachedReport_IsNotServedAfterTheSettingsChange_AndStillIsWhenTheyReturn()
    {
        var rig = await Rig.ReadyAsync();
        await rig.SeedAsync("2026-07", marker: "july");
        Assert.Equal("july", (await rig.Service.GetReportAsync("2026-07"))!.GeneratedAt); // cached for an hour
        Assert.Equal(1, rig.Store.ReportReads);

        await rig.UseAsync(OtherGa4); // no time passes: the change takes effect at once
        Assert.Null(await rig.Service.GetReportAsync("2026-07"));
        Assert.True(await rig.Service.NeedsFetchAsync(July));

        await rig.UseAsync();
        Assert.Equal("july", (await rig.Service.GetReportAsync("2026-07"))!.GeneratedAt);
        Assert.Equal(1, rig.Store.ReportReads); // the cache was never given up, only checked against the settings
    }

    [Fact]
    public async Task ACachedPeriodList_IsNotServedAfterTheSettingsChange()
    {
        var rig = await Rig.ReadyAsync();
        await rig.SeedAsync("2026-07");
        Assert.Equal(["2026-07"], await rig.Service.GetAvailablePeriodsAsync()); // cached for five minutes

        await rig.UseAsync(site: OtherSite);
        Assert.Empty(await rig.Service.GetAvailablePeriodsAsync());

        await rig.UseAsync();
        Assert.Equal(["2026-07"], await rig.Service.GetAvailablePeriodsAsync());
    }

    [Fact]
    public async Task ARefresh_AfterTheSettingsChange_ArchivesUnderTheNewProperty_AndReplacesTheRow()
    {
        var rig = await Rig.ReadyAsync();
        await rig.SeedAsync("2026-07", marker: "old");
        await rig.UseAsync(OtherGa4, OtherSite);
        Assert.True(await rig.Service.NeedsFetchAsync(July));

        var report = await rig.Service.RefreshAsync(July);

        Assert.Equal(new Measured(OtherSite, OtherGa4, null), report.Measured);
        Assert.Contains(OtherGa4, (await rig.Raw.GetReportAsync(July))!.Json);
        Assert.Single(await rig.Raw.ListPeriodsAsync()); // the row was replaced, not added to
        Assert.All(rig.Google.Ga4.Calls, call => Assert.Contains("properties/987654321:runReport", call.Uri.AbsoluteUri));
        Assert.Same(report, await rig.Service.GetReportAsync("2026-07"));
        Assert.Equal(["2026-07"], await rig.Service.GetAvailablePeriodsAsync());
        Assert.False(await rig.Service.NeedsFetchAsync(July));
    }

    [Fact]
    public async Task WhenTheSetupIsNotReady_ReadsReturnWhatIsArchived_AndNeverThrow()
    {
        // No key and no property: nothing to compare with, and the dashboard shows the not-connected card from the status anyway.
        var rig = Rig.NotConfigured();
        await rig.SeedAsync("2026-07", marker: "july", ga4Property: OtherGa4, site: OtherSite);
        await rig.SeedAsync("2026-06", recordMeasured: false);

        Assert.Equal(["2026-06", "2026-07"], await rig.Service.GetAvailablePeriodsAsync());
        Assert.Equal("july", (await rig.Service.GetReportAsync())!.GeneratedAt);
        Assert.NotNull(await rig.Service.GetReportAsync("2026-06"));
        Assert.NotNull(await rig.Service.GetFetchedUtcAsync("2026-07"));
        Assert.False(await rig.Service.NeedsFetchAsync(July));
    }

    [Fact]
    public async Task WhenTheKeyCannotBeRead_ReadsReturnWhatIsArchived_AndNeverThrow()
    {
        var settings = new SettingsHarness();
        await settings.SeedSettingsAsync(new { ga4PropertyId = OtherGa4, searchConsoleSite = OtherSite });
        await settings.Store.SaveDocumentAsync(DocumentKeys.ProtectedKey, "not json");
        var rig = Rig.Over(settings);
        await rig.SeedAsync("2026-07", marker: "july"); // built for other properties than the ones configured

        Assert.Equal("july", (await rig.Service.GetReportAsync("2026-07"))!.GeneratedAt);
        Assert.Equal(["2026-07"], await rig.Service.GetAvailablePeriodsAsync());
        Assert.False(await rig.Service.NeedsFetchAsync(July));
    }

    [Fact]
    public async Task AReportThatRecordsNothingAboutItsSources_IsMissingWhenReady_ButServedWhenNot()
    {
        var ready = await Rig.ReadyAsync();
        await ready.SeedAsync("2026-07", recordMeasured: false);

        Assert.Null(await ready.Service.GetReportAsync("2026-07"));
        Assert.Empty(await ready.Service.GetAvailablePeriodsAsync());
        Assert.True(await ready.Service.NeedsFetchAsync(July));

        var notReady = Rig.NotConfigured();
        await notReady.SeedAsync("2026-07", recordMeasured: false);

        Assert.NotNull(await notReady.Service.GetReportAsync("2026-07"));
        Assert.Equal(["2026-07"], await notReady.Service.GetAvailablePeriodsAsync());
    }

    // ---------------------------------------------------------------------------------------------------------------
    // A refresh stays within what can be fetched
    // ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("2026-07")] // exactly the latest month that should be archived on 2026-08-20
    [InlineData("2025-05")] // exactly the earliest month inside Search Console's window on 2026-08-20
    [InlineData("2026-01")]
    public async Task Refresh_AcceptsTheMonthsFromTheEarliestToTheLatest_Inclusive(string month)
    {
        var rig = await Rig.ReadyAsync();

        var report = await rig.Service.RefreshAsync(Period.Parse(month));

        Assert.Equal(month, report.ReportPeriod);
        Assert.Equal([month], await rig.Service.GetAvailablePeriodsAsync());
    }

    [Theory]
    [InlineData("2026-08")] // the month that is still running: one later than the latest
    [InlineData("2026-09")]
    [InlineData("2027-01")]
    [InlineData("9999-12")]
    [InlineData("2025-04")] // one earlier than the earliest
    [InlineData("2020-01")]
    [InlineData("0001-01")]
    public async Task Refresh_RefusesAMonthOutsideThatRange_BeforeAnythingIsAskedOrWritten(string month)
    {
        var rig = await Rig.ReadyAsync();

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => rig.Service.RefreshAsync(Period.Parse(month)));

        Assert.StartsWith(OutOfRangeMessage, ex.Message);
        Assert.Equal("period", ex.ParamName); // the coordinator's and the controller's "outside the range" handling both depend on this name
        Assert.DoesNotContain(month, ex.Message); // nothing the caller sent travels in the message
        Assert.Equal(0, rig.Google.Ga4ServiceRequests);
        Assert.Equal(0, rig.Google.SearchConsoleServiceRequests);
        Assert.Equal(0, rig.Store.ReportReads);
        Assert.Equal(0, rig.Store.ReportWrites);
        Assert.Empty(await rig.Service.GetAvailablePeriodsAsync());
    }

    [Fact]
    public async Task Refresh_RefusesAMonthOutsideThatRange_AtOnce_WithoutWaitingBehindAFetch()
    {
        var rig = await Rig.ReadyAsync();
        var hold = new TaskCompletionSource();
        rig.Google.Hold = hold.Task;

        var running = rig.Service.RefreshAsync(July);
        await WaitUntilAsync(() => rig.Google.Ga4ServiceRequests == 1);

        var refused = rig.Service.RefreshAsync(Period.Parse("2026-08"));
        var first = await Task.WhenAny(refused, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(refused, first); // refused while the other fetch is still parked, not after it
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => refused);

        hold.SetResult();
        await running;
        Assert.Equal(3, rig.Google.Ga4ServiceRequests);
    }

    [Theory]
    [InlineData("2026-09-04", "2026-07", "2026-08")] // the 4th: August has not been over for five days yet
    [InlineData("2026-09-05", "2026-08", "2026-09")]
    [InlineData("2027-01-04", "2026-11", "2026-12")]
    [InlineData("2027-01-05", "2026-12", "2027-01")]
    public async Task TheLatestMonthAllowed_FollowsTheClock(string today, string allowed, string refused)
    {
        var rig = await Rig.ReadyAsync(now: DateTimeOffset.Parse($"{today}T09:30:00Z", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(Period.Parse(allowed), rig.Service.ExpectedLatestPeriod);

        Assert.Equal(allowed, (await rig.Service.RefreshAsync(Period.Parse(allowed))).ReportPeriod);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => rig.Service.RefreshAsync(Period.Parse(refused)));
    }

    [Theory]
    [InlineData("2026-08-31", "2025-05", "2025-04")]
    [InlineData("2026-09-01", "2025-06", "2025-05")]
    [InlineData("2027-01-15", "2025-10", "2025-09")]
    public async Task TheEarliestMonthAllowed_FollowsTheClock(string today, string allowed, string refused)
    {
        var rig = await Rig.ReadyAsync(now: DateTimeOffset.Parse($"{today}T09:30:00Z", System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(allowed, (await rig.Service.RefreshAsync(Period.Parse(allowed))).ReportPeriod);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => rig.Service.RefreshAsync(Period.Parse(refused)));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // A refresh that waited is only answered by a refresh made with the same settings
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task AWaitingRefresh_WithOtherBrandTerms_RunsItsOwnFetch_NotTheFinishedOnesReport()
    {
        var rig = await Rig.ReadyAsync();
        var hold = new TaskCompletionSource();
        rig.Google.Hold = hold.Task;

        var first = rig.Service.RefreshAsync(July); // asked with no brand terms
        await WaitUntilAsync(() => rig.Google.Ga4ServiceRequests == 1);
        await rig.UseAsync(brandTerms: ["acme"]);
        var second = rig.Service.RefreshAsync(July); // asked with a brand term
        Assert.False(second.IsCompleted);

        hold.SetResult();
        var reports = await Task.WhenAll(first, second);

        Assert.Equal(6, rig.Google.Ga4ServiceRequests); // two fetches
        Assert.Equal(2, rig.Store.ReportWrites);
        Assert.Null(reports[0].Measured!.BrandRegex);
        Assert.Equal("(?i)(acme)", reports[1].Measured!.BrandRegex);
        Assert.Same(reports[1], await rig.Service.GetReportAsync("2026-07"));
    }

    [Fact]
    public async Task AWaitingRefresh_WithAnotherProperty_RunsItsOwnFetch()
    {
        var rig = await Rig.ReadyAsync();
        var hold = new TaskCompletionSource();
        rig.Google.Hold = hold.Task;

        var first = rig.Service.RefreshAsync(July);
        await WaitUntilAsync(() => rig.Google.Ga4ServiceRequests == 1);
        await rig.UseAsync(OtherGa4, OtherSite);
        var second = rig.Service.RefreshAsync(July);

        hold.SetResult();
        var reports = await Task.WhenAll(first, second);

        Assert.Equal(new Measured(ReadySettingsHarness.Site, ReadySettingsHarness.Ga4Property, null), reports[0].Measured);
        Assert.Equal(new Measured(OtherSite, OtherGa4, null), reports[1].Measured);
        Assert.Equal(6, rig.Google.Ga4ServiceRequests);
        Assert.Contains(OtherGa4, (await rig.Raw.GetReportAsync(July))!.Json); // the later fetch is the one that stays
    }

    [Fact]
    public async Task AWaitingRefresh_WithEqualSettingsSavedAgain_StillReusesTheFinishedReport()
    {
        // Saving the same values again makes the settings service hand out a new list of brand terms: equal, not the same object.
        var rig = await Rig.ReadyAsync(brandTerms: ["acme", "acme ab"]);
        var hold = new TaskCompletionSource();
        rig.Google.Hold = hold.Task;

        var first = rig.Service.RefreshAsync(July);
        await WaitUntilAsync(() => rig.Google.Ga4ServiceRequests == 1);
        await rig.UseAsync(brandTerms: ["acme", "acme ab"]);
        var second = rig.Service.RefreshAsync(July);

        hold.SetResult();
        var reports = await Task.WhenAll(first, second);

        Assert.Equal(3, rig.Google.Ga4ServiceRequests); // one fetch
        Assert.Equal(1, rig.Store.ReportWrites);
        Assert.Same(reports[0], reports[1]);
    }
}
