using System.Net;
using System.Text.Json;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Google;
using Google.Apis.AnalyticsData.v1beta.Data;
using Google.Apis.Json;
using Google.Apis.SearchConsole.v1.Data;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>The fetch half of <see cref="SeoReportServiceTests"/>: what a refresh asks for and archives, what it refuses, and how refreshes queue up.</summary>
public partial class SeoReportServiceTests
{
    private static readonly Period June = Period.Parse("2026-06");

    private static RunReportRequest Ga4Request(FakeGoogleTransport.Call call)
        => NewtonsoftJsonSerializer.Instance.Deserialize<RunReportRequest>(call.Body);

    private static SearchAnalyticsQueryRequest SearchRequest(FakeGoogleTransport.Call call)
        => NewtonsoftJsonSerializer.Instance.Deserialize<SearchAnalyticsQueryRequest>(call.Body);

    /// <summary>Waits (briefly, polling) until a fetch that is parked on the hold has reached it.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition was not reached in time.");
            await Task.Delay(1);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // What a refresh archives
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Refresh_ArchivesTheReport_WithTheContractVersionAndTheFetchTime()
    {
        var rig = await Rig.ReadyAsync(brandTerms: ["acme"]);

        var report = await rig.Service.RefreshAsync(July);

        Assert.Equal("2026-07", report.ReportPeriod);
        Assert.Equal("live", report.DataStatus);

        var row = (await rig.Raw.GetReportAsync(July))!;
        Assert.Equal(1, row.ContractVersion);
        Assert.Equal(SeoReportService.ContractVersion, row.ContractVersion);
        Assert.Equal(rig.Time.GetUtcNow().UtcDateTime, row.FetchedUtc);
        Assert.Contains("\"report_period\":\"2026-07\"", row.Json); // the contract's own names
        Assert.Equal("2026-07", JsonSerializer.Deserialize<Report>(row.Json)!.ReportPeriod);

        Assert.Equal(["2026-07"], await rig.Service.GetAvailablePeriodsAsync());
        Assert.Same(report, await rig.Service.GetReportAsync("2026-07"));
        Assert.Equal(rig.Time.GetUtcNow(), await rig.Service.GetFetchedUtcAsync("2026-07"));
        Assert.False(await rig.Service.NeedsFetchAsync(July));
        Assert.True(await rig.Service.IsUpToDateAsync()); // on 2026-08-20 July is the month that should be archived
    }

    [Fact]
    public async Task Refresh_RecordsWhatWasMeasured_FromTheSettingsInForce()
    {
        var rig = await Rig.ReadyAsync(brandTerms: ["acme", "acme ab"]);

        var report = await rig.Service.RefreshAsync(July);

        Assert.Equal(new Measured("sc-domain:example.com", "properties/123456789", "(?i)(acme|acme ab)"), report.Measured);
    }

    [Fact]
    public async Task Refresh_UsesTheNormalizedSettings_NotWhatWasTyped()
    {
        var rig = await Rig.ReadyAsync();
        await rig.Settings.Settings.SaveAsync(
            new SeoReportSettings { Ga4PropertyId = "  123456789 ", SearchConsoleSite = "Example.com/" }, null, false);

        var report = await rig.Service.RefreshAsync(July);

        Assert.Equal(new Measured("sc-domain:example.com", "properties/123456789", null), report.Measured);
        Assert.All(rig.Google.Ga4.Calls, call => Assert.Contains("properties/123456789:runReport", call.Uri.AbsoluteUri));
        Assert.All(rig.Google.SearchConsole.Calls, call => Assert.Contains("/sites/sc-domain%3Aexample.com/", call.Uri.AbsoluteUri));
    }

    [Fact]
    public async Task Refresh_ReplacesAMonthsReport_AndNeverTouchesAnotherMonth()
    {
        var rig = await Rig.ReadyAsync();

        await rig.Service.RefreshAsync(June);
        var juneAtFirst = (await rig.Raw.GetReportAsync(June))!;

        rig.Time.Advance(TimeSpan.FromDays(1));
        await rig.Service.RefreshAsync(July);

        Assert.Equal(["2026-06", "2026-07"], await rig.Service.GetAvailablePeriodsAsync());
        var juneNow = (await rig.Raw.GetReportAsync(June))!;
        Assert.Equal(juneAtFirst.Json, juneNow.Json);
        Assert.Equal(juneAtFirst.FetchedUtc, juneNow.FetchedUtc);

        // The same month again replaces its own row and nothing else.
        rig.Time.Advance(TimeSpan.FromDays(1));
        await rig.Service.RefreshAsync(July);

        Assert.Equal(["2026-06", "2026-07"], await rig.Service.GetAvailablePeriodsAsync());
        Assert.Equal(rig.Time.GetUtcNow().UtcDateTime, (await rig.Raw.GetReportAsync(July))!.FetchedUtc);
        Assert.Equal(juneAtFirst.FetchedUtc, (await rig.Raw.GetReportAsync(June))!.FetchedUtc);
    }

    [Fact]
    public async Task Refresh_ReplacesAReportFromAnOlderContract()
    {
        var rig = await Rig.ReadyAsync();
        await rig.SeedAsync("2026-07", contractVersion: 0, marker: "old");
        Assert.True(await rig.Service.NeedsFetchAsync(July));

        await rig.Service.RefreshAsync(July);

        Assert.Equal(1, (await rig.Raw.GetReportAsync(July))!.ContractVersion);
        Assert.NotEqual("old", (await rig.Service.GetReportAsync("2026-07"))!.GeneratedAt);
        Assert.False(await rig.Service.NeedsFetchAsync(July));
    }

    [Fact]
    public async Task Refresh_DropsTheCachedPeriodList_SoTheNewMonthShowsAtOnce()
    {
        var rig = await Rig.ReadyAsync();
        Assert.Empty(await rig.Service.GetAvailablePeriodsAsync()); // cached, empty, for five minutes

        await rig.Service.RefreshAsync(July);

        Assert.Equal(["2026-07"], await rig.Service.GetAvailablePeriodsAsync()); // no time has passed
    }

    [Fact]
    public async Task Refresh_ReplacesTheCachedReport_SoTheNewContentShowsAtOnce()
    {
        var rig = await Rig.ReadyAsync();
        await rig.SeedAsync("2026-07", marker: "old");
        Assert.Equal("old", (await rig.Service.GetReportAsync("2026-07"))!.GeneratedAt); // cached for an hour

        var refreshed = await rig.Service.RefreshAsync(July);

        Assert.NotEqual("old", refreshed.GeneratedAt);
        Assert.Same(refreshed, await rig.Service.GetReportAsync("2026-07"));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // What a refresh asks Google for
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Refresh_AsksGa4ForThreeSegmentsOverTwoYears_AndSearchConsoleForTheProbeThirteenMonthsAndThreeLists()
    {
        var rig = await Rig.ReadyAsync(brandTerms: ["acme"]);

        await rig.Service.RefreshAsync(July);

        // GA4: organic, total and organic from Google, each over January of the year before up to the end of the month.
        var ga4 = rig.Google.Ga4.Calls.Select(Ga4Request).ToList();
        Assert.Equal(3, ga4.Count);
        Assert.All(ga4, request => Assert.Equal("2025-01-01", request.DateRanges[0].StartDate));
        Assert.All(ga4, request => Assert.Equal("2026-07-31", request.DateRanges[0].EndDate));
        Assert.Equal("Organic Search", ga4[0].DimensionFilter.Filter.StringFilter.Value);
        Assert.Null(ga4[1].DimensionFilter);
        Assert.NotNull(ga4[2].DimensionFilter.AndGroup);

        // Search Console: the probe first (asked on the injected clock's day), then 13 months by segment, then the three lists.
        var search = rig.Google.SearchConsole.Calls.Select(SearchRequest).ToList();
        Assert.Equal(1 + (13 * 3) + 3, search.Count);
        Assert.Equal(["date"], search[0].Dimensions);
        Assert.Equal("2026-08-20", search[0].EndDate);
        Assert.Equal(["query"], search[^3].Dimensions);
        Assert.Equal(["page"], search[^2].Dimensions);
        Assert.Equal(["query", "page"], search[^1].Dimensions);
        Assert.Equal("2025-07-01", search[1].StartDate);
        Assert.Equal("2026-07-31", search[1 + (13 * 3) - 1].EndDate);
    }

    [Fact]
    public async Task Refresh_SendsTheBrandPatternOfTheBrandTerms_ToSearchConsole()
    {
        var rig = await Rig.ReadyAsync(brandTerms: ["acme", "acme ab"]);

        await rig.Service.RefreshAsync(July);

        var filters = rig.Google.SearchConsole.Calls.Select(SearchRequest)
            .SelectMany(request => request.DimensionFilterGroups ?? [])
            .SelectMany(group => group.Filters)
            .ToList();
        Assert.Equal(26, filters.Count); // brand and non-brand for each of 13 months
        Assert.All(filters, filter => Assert.Equal("(?i)(acme|acme ab)", filter.Expression));
    }

    [Fact]
    public async Task Refresh_WithBrandTerms_ReportsTheBrandSplit()
    {
        var rig = await Rig.ReadyAsync(brandTerms: ["acme"]);

        var report = await rig.Service.RefreshAsync(July);

        Assert.NotEmpty(report.Traffic.Search!.Segments);
    }

    [Fact]
    public async Task Refresh_WithNoBrandTerms_SendsANeverMatchingPattern_AndReportsNoBrandSplit()
    {
        var rig = await Rig.ReadyAsync();

        var report = await rig.Service.RefreshAsync(July);

        var filters = rig.Google.SearchConsole.Calls.Select(SearchRequest)
            .SelectMany(request => request.DimensionFilterGroups ?? [])
            .SelectMany(group => group.Filters)
            .ToList();
        Assert.NotEmpty(filters);
        Assert.All(filters, filter => Assert.Equal("(?i)^$", filter.Expression));

        Assert.Null(report.Measured!.BrandRegex);
        Assert.Empty(report.Traffic.Search!.Segments); // no brand terms, no brand split
        Assert.Equal("live", report.DataStatus);
    }

    [Fact]
    public async Task Refresh_AsksGa4First_AndStopsThereWhenItFails()
    {
        var rig = await Rig.ReadyAsync();
        rig.Google.Ga4Failure = HttpStatusCode.Forbidden;

        await Assert.ThrowsAsync<GoogleApiException>(() => rig.Service.RefreshAsync(July));

        Assert.Equal(0, rig.Google.SearchConsoleServiceRequests);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // What a refresh refuses, and failures
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Refresh_OnANewInstallation_ThrowsNotConfigured_AndAsksNothing()
    {
        var rig = Rig.NotConfigured();

        var ex = await Assert.ThrowsAsync<ReportFetchException>(() => rig.Service.RefreshAsync(July));

        Assert.Equal("notConfigured", ex.Code);
        Assert.Equal("notConfigured", ex.Message);
        Assert.Equal(0, rig.Google.Ga4ServiceRequests);
        Assert.Equal(0, rig.Google.SearchConsoleServiceRequests);
        Assert.Equal(0, rig.Store.ReportWrites);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Refresh_WithAPropertyMissing_ThrowsNotConfigured(bool withoutGa4, bool withoutSite)
    {
        var settings = new SettingsHarness();
        await settings.Settings.SaveAsync(
            new SeoReportSettings
            {
                Ga4PropertyId = withoutGa4 ? null : ReadySettingsHarness.Ga4Property,
                SearchConsoleSite = withoutSite ? null : ReadySettingsHarness.Site,
            },
            FakeServiceAccount.Json(),
            removeKey: false);
        var rig = Rig.Over(settings);

        var ex = await Assert.ThrowsAsync<ReportFetchException>(() => rig.Service.RefreshAsync(July));

        Assert.Equal("notConfigured", ex.Code);
        Assert.Equal(0, rig.Google.Ga4ServiceRequests);
    }

    [Fact]
    public async Task Refresh_WithAKeyThatCannotBeRead_ThrowsKeyUnreadable_AndAsksNothing()
    {
        var settings = new SettingsHarness();
        await settings.SeedSettingsAsync(new { ga4PropertyId = ReadySettingsHarness.Ga4Property, searchConsoleSite = ReadySettingsHarness.Site });
        await settings.Store.SaveDocumentAsync(DocumentKeys.ProtectedKey, "not json");
        var rig = Rig.Over(settings);

        var ex = await Assert.ThrowsAsync<ReportFetchException>(() => rig.Service.RefreshAsync(July));

        Assert.Equal("keyUnreadable", ex.Code);
        Assert.Equal(0, rig.Google.Ga4ServiceRequests);
        Assert.Equal(0, rig.Store.ReportWrites);
    }

    [Fact]
    public async Task Refresh_LetsACredentialErrorFromTheClientsThrough_WithItsCode_AndArchivesNothing()
    {
        var rig = await Rig.ReadyAsync();
        rig.Google.ServiceFailure = new ReportFetchException("keyInvalid");

        var ex = await Assert.ThrowsAsync<ReportFetchException>(() => rig.Service.RefreshAsync(July));

        Assert.Equal("keyInvalid", ex.Code);
        Assert.Equal(0, rig.Store.ReportWrites);
        Assert.Empty(await rig.Service.GetAvailablePeriodsAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Refresh_LetsAGoogleErrorThroughUnchanged_AndArchivesNothing(bool ga4Fails)
    {
        var rig = await Rig.ReadyAsync();
        if (ga4Fails) rig.Google.Ga4Failure = HttpStatusCode.Forbidden;
        else rig.Google.SearchConsoleFailure = HttpStatusCode.Forbidden;

        var ex = await Assert.ThrowsAsync<GoogleApiException>(() => rig.Service.RefreshAsync(July));

        Assert.Equal(HttpStatusCode.Forbidden, ex.HttpStatusCode);
        Assert.Equal(0, rig.Store.ReportWrites);
        Assert.Empty(await rig.Service.GetAvailablePeriodsAsync());
        Assert.True(await rig.Service.NeedsFetchAsync(July));
    }

    [Fact]
    public async Task AFailedRefresh_LeavesTheArchivedReportAsItWas_AndTheNextOneWorks()
    {
        var rig = await Rig.ReadyAsync();
        await rig.Service.RefreshAsync(July);
        var before = (await rig.Raw.GetReportAsync(July))!;
        rig.Time.Advance(TimeSpan.FromDays(2));

        rig.Google.Ga4Failure = HttpStatusCode.Forbidden;
        await Assert.ThrowsAsync<GoogleApiException>(() => rig.Service.RefreshAsync(July));
        var after = (await rig.Raw.GetReportAsync(July))!;
        Assert.Equal(before.Json, after.Json);
        Assert.Equal(before.FetchedUtc, after.FetchedUtc);

        rig.Google.Ga4Failure = null;
        await rig.Service.RefreshAsync(July);
        Assert.Equal(rig.Time.GetUtcNow().UtcDateTime, (await rig.Raw.GetReportAsync(July))!.FetchedUtc);
    }

    [Fact]
    public async Task Refresh_LogsThePeriod_NeverASettingOrTheKey()
    {
        var rig = await Rig.ReadyAsync(brandTerms: ["acme"]);
        rig.Google.SearchConsoleFailure = HttpStatusCode.Forbidden;

        await Assert.ThrowsAsync<GoogleApiException>(() => rig.Service.RefreshAsync(June)); // a failed one first
        rig.Google.SearchConsoleFailure = null;
        await rig.Service.RefreshAsync(July);

        var log = rig.Log.AllText;
        Assert.Contains("2026-07", log);
        Assert.DoesNotContain("123456789", log);
        Assert.DoesNotContain("example.com", log);
        Assert.DoesNotContain("acme", log);
        Assert.DoesNotContain(FakeServiceAccount.DefaultClientEmail, log);
        Assert.DoesNotContain("PRIVATE KEY", log);
        Assert.DoesNotContain(FakeServiceAccount.PrivateKeyPem[..40], log);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // A new site: nothing comes back
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Refresh_OfASiteWithNoData_ArchivesANoDataReport_WithoutThrowing()
    {
        var rig = await Rig.ReadyAsync(empty: true, brandTerms: ["acme"]);

        var report = await rig.Service.RefreshAsync(July);

        Assert.Equal("noData", report.DataStatus);
        Assert.NotNull(report.DataStatusNote);
        Assert.Equal(["2026-07"], await rig.Service.GetAvailablePeriodsAsync());
        Assert.Equal("noData", (await rig.Service.GetReportAsync("2026-07"))!.DataStatus);
        Assert.DoesNotContain("NaN", (await rig.Raw.GetReportAsync(July))!.Json);
        Assert.DoesNotContain("Infinity", (await rig.Raw.GetReportAsync(July))!.Json);
    }

    [Fact]
    public async Task ANoDataReport_NeedsAFetchAgainAfterADay_AndAFetchWithDataReplacesIt()
    {
        var rig = await Rig.ReadyAsync(empty: true);
        await rig.Service.RefreshAsync(July);
        Assert.False(await rig.Service.NeedsFetchAsync(July));
        Assert.True(await rig.Service.IsUpToDateAsync());

        rig.Time.Advance(TimeSpan.FromHours(23));
        Assert.False(await rig.Service.NeedsFetchAsync(July));

        rig.Time.Advance(TimeSpan.FromHours(2)); // 25 hours after the fetch
        Assert.True(await rig.Service.NeedsFetchAsync(July));
        Assert.False(await rig.Service.IsUpToDateAsync());

        rig.Google.Empty = false; // the site has data now
        var report = await rig.Service.RefreshAsync(July);

        Assert.Equal("live", report.DataStatus);
        Assert.False(await rig.Service.NeedsFetchAsync(July));
        rig.Time.Advance(TimeSpan.FromDays(30));
        Assert.False(await rig.Service.NeedsFetchAsync(July));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // One fetch at a time
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task TwoConcurrentRefreshesOfTheSameMonth_FetchOnce_AndBothGetTheArchivedReport()
    {
        var rig = await Rig.ReadyAsync();
        var hold = new TaskCompletionSource();
        rig.Google.Hold = hold.Task;

        var first = rig.Service.RefreshAsync(July);
        await WaitUntilAsync(() => rig.Google.Ga4ServiceRequests == 1); // the first fetch is parked at GA4's first call

        var second = rig.Service.RefreshAsync(July);
        Assert.False(second.IsCompleted);
        Assert.Equal(1, rig.Google.Ga4ServiceRequests); // the second did not start a fetch of its own: it waits its turn

        hold.SetResult();
        var reports = await Task.WhenAll(first, second);

        Assert.Equal(3, rig.Google.Ga4ServiceRequests); // one fetch is three GA4 calls
        Assert.Equal(1, rig.Google.SearchConsoleServiceRequests);
        Assert.Equal(1, rig.Store.ReportWrites);
        Assert.Same(reports[0], reports[1]);
        Assert.Equal("2026-07", reports[1].ReportPeriod);
    }

    [Fact]
    public async Task ARefreshThatComesAfterAnother_FetchesAgain_EvenWhenNoTimeHasPassed()
    {
        // Only a refresh that was waiting while another finished may reuse its result; a person who presses Refresh later gets
        // fresh data, however coarse the clock.
        var rig = await Rig.ReadyAsync();

        await rig.Service.RefreshAsync(July);
        await rig.Service.RefreshAsync(July);

        Assert.Equal(6, rig.Google.Ga4ServiceRequests);
        Assert.Equal(2, rig.Google.SearchConsoleServiceRequests);
        Assert.Equal(2, rig.Store.ReportWrites);
    }

    [Fact]
    public async Task TwoConcurrentRefreshesOfDifferentMonths_RunOneAfterTheOther_AndBothArchive()
    {
        var rig = await Rig.ReadyAsync();
        var hold = new TaskCompletionSource();
        rig.Google.Hold = hold.Task;

        var june = rig.Service.RefreshAsync(June);
        await WaitUntilAsync(() => rig.Google.Ga4ServiceRequests == 1);
        var july = rig.Service.RefreshAsync(July);
        Assert.False(july.IsCompleted);
        Assert.Equal(1, rig.Google.Ga4ServiceRequests); // July has not started

        hold.SetResult();
        await Task.WhenAll(june, july);

        Assert.Equal(["2026-06", "2026-07"], await rig.Service.GetAvailablePeriodsAsync());
        var ends = rig.Google.Ga4.Calls.Select(call => Ga4Request(call).DateRanges[0].EndDate).ToList();
        Assert.Equal(["2026-06-30", "2026-06-30", "2026-06-30", "2026-07-31", "2026-07-31", "2026-07-31"], ends);
    }

    [Fact]
    public async Task WhenTheFetchInFrontFails_TheOneWaitingRunsItsOwnFetch()
    {
        var rig = await Rig.ReadyAsync();
        var hold = new TaskCompletionSource();
        rig.Google.Hold = hold.Task;
        rig.Google.FailNextGa4Calls(1);

        var first = rig.Service.RefreshAsync(July);
        await WaitUntilAsync(() => rig.Google.Ga4ServiceRequests == 1);
        var second = rig.Service.RefreshAsync(July);

        hold.SetResult();

        await Assert.ThrowsAsync<GoogleApiException>(() => first);
        var report = await second;

        Assert.Equal("2026-07", report.ReportPeriod);
        Assert.Equal(1, rig.Store.ReportWrites);
        Assert.Equal(["2026-07"], await rig.Service.GetAvailablePeriodsAsync());
    }

    [Fact]
    public async Task ARefreshCancelledWhileWaiting_NeverFetches_AndDoesNotBlockTheNextOne()
    {
        var rig = await Rig.ReadyAsync();
        var hold = new TaskCompletionSource();
        rig.Google.Hold = hold.Task;

        var first = rig.Service.RefreshAsync(July);
        await WaitUntilAsync(() => rig.Google.Ga4ServiceRequests == 1);
        using var cancel = new CancellationTokenSource();
        var cancelled = rig.Service.RefreshAsync(June, cancel.Token);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        hold.SetResult();
        await first;
        Assert.Equal(3, rig.Google.Ga4ServiceRequests); // only July was fetched

        await rig.Service.RefreshAsync(June); // the gate was not left held by the cancelled call
        Assert.Equal(["2026-06", "2026-07"], await rig.Service.GetAvailablePeriodsAsync());
    }
}
