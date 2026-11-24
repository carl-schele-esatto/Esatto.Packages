using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Google;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The day-fresh traffic curve and the sources of one day. Rolling data, kept apart from the archive on purpose: it is cached
/// for half an hour and not for good, and the date is part of what is cached, so "the last 30 days" never outlives midnight.
/// </summary>
public class DailyTrafficServiceTests
{
    private static readonly TimeSpan CacheLife = TimeSpan.FromMinutes(30);

    private sealed class Rig(SettingsHarness settings, SyntheticGoogle google, DailyTrafficService service)
    {
        public SettingsHarness Settings => settings;

        public SyntheticGoogle Google => google;

        public DailyTrafficService Service => service;

        public Task<(Ga4DailySeries Series, Ga4DailyWindow Window)> SeriesAsync(string? month = null)
            => service.GetSeriesAsync(month, CancellationToken.None);

        public void Advance(TimeSpan by) => settings.Time.Advance(by);
    }

    private static Rig Create(SettingsHarness settings)
    {
        var google = new SyntheticGoogle(new DateOnly(2026, 8, 12));
        return new Rig(settings, google, new DailyTrafficService(settings.Settings, google, new MemoryCache(new MemoryCacheOptions()), settings.Time));
    }

    private static async Task<Rig> ReadyAsync() => Create(await ReadySettingsHarness.ReadyAsync());

    // ---------------------------------------------------------------------------------------------------------------
    // The curve
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Series_WithoutAMonth_IsTheRollingThirtyDaysUpToYesterday_FromOneCall()
    {
        var rig = await ReadyAsync();

        var (series, window) = await rig.SeriesAsync();

        Assert.Null(window.Month);
        Assert.Equal(new DateOnly(2026, 7, 21), window.Start);
        Assert.Equal(new DateOnly(2026, 8, 19), window.End);
        Assert.Equal(30, series.Points.Count);
        Assert.Equal(SyntheticGoogle.DailySessions(new DateOnly(2026, 7, 21)), series.Points[0].Sessions);

        var call = Assert.Single(rig.Google.Ga4.Calls);
        Assert.Contains("properties/123456789:runReport", call.Uri.AbsoluteUri);
    }

    [Fact]
    public async Task Series_ForAMonth_IsThatMonth()
    {
        var rig = await ReadyAsync();

        var (series, window) = await rig.SeriesAsync("2026-06");

        Assert.Equal("2026-06", window.Month);
        Assert.Equal(new DateOnly(2026, 6, 1), window.Start);
        Assert.Equal(new DateOnly(2026, 6, 30), window.End);
        Assert.Equal(30, series.Points.Count);
    }

    [Fact]
    public async Task Series_ANullMonthAndAnEmptyOne_AreTheSameRollingEntry()
    {
        var rig = await ReadyAsync();

        var first = await rig.SeriesAsync(null);
        var second = await rig.SeriesAsync("");

        Assert.Single(rig.Google.Ga4.Calls);
        Assert.Same(first.Series, second.Series);
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("../x")]
    [InlineData(" ")]
    [InlineData("2026-7")]
    [InlineData("202607")]
    [InlineData("2026-07 ")]
    [InlineData("2026-07-01")]
    public async Task Series_ForAMonthThatIsNotYyyyMm_IsRefused_BeforeAnythingIsAskedOrCached(string month)
    {
        var rig = await ReadyAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => rig.SeriesAsync(month));

        Assert.StartsWith("The month must be written YYYY-MM.", ex.Message);
        if (month.Trim().Length > 0) Assert.DoesNotContain(month.Trim(), ex.Message); // nothing the caller typed travels in the message
        Assert.Equal(0, rig.Google.Ga4ServiceRequests);
        Assert.Empty(rig.Google.Ga4.Calls);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The cache: half an hour, per month, and never across a change of day or of property
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Series_IsCachedForThirtyMinutes()
    {
        var rig = await ReadyAsync();

        await rig.SeriesAsync();
        rig.Advance(CacheLife - TimeSpan.FromMinutes(1));
        await rig.SeriesAsync();
        Assert.Single(rig.Google.Ga4.Calls);

        rig.Advance(TimeSpan.FromMinutes(2)); // 31 minutes since the fetch
        await rig.SeriesAsync();
        Assert.Equal(2, rig.Google.Ga4.Calls.Count);
    }

    [Fact]
    public async Task Series_ACachedCurve_IsNotFetchedAgain_ForTheSameMonth_OnlyForADifferentOne()
    {
        var rig = await ReadyAsync();

        await rig.SeriesAsync("2026-05");
        await rig.SeriesAsync("2026-06");
        await rig.SeriesAsync();
        Assert.Equal(3, rig.Google.Ga4.Calls.Count);

        await rig.SeriesAsync("2026-05");
        await rig.SeriesAsync("2026-06");
        await rig.SeriesAsync();
        Assert.Equal(3, rig.Google.Ga4.Calls.Count);
    }

    [Fact]
    public async Task Series_ADayChange_MissesTheCache_EvenWithinTheLifetime()
    {
        // Shortly before midnight: the curve ends on the 19th. Fifteen minutes later it is the 21st and the curve must reach the
        // 20th, although half an hour has not passed.
        var rig = await ReadyAsync();
        rig.Settings.Time.SetUtcNow(new DateTimeOffset(2026, 8, 20, 23, 50, 0, TimeSpan.Zero));

        var before = await rig.SeriesAsync();
        rig.Advance(TimeSpan.FromMinutes(15));
        var after = await rig.SeriesAsync();

        Assert.Equal(new DateOnly(2026, 8, 19), before.Window.End);
        Assert.Equal(new DateOnly(2026, 8, 20), after.Window.End);
        Assert.Equal(2, rig.Google.Ga4.Calls.Count);
    }

    [Fact]
    public async Task Series_ChangingTheGa4Property_DoesNotServeTheOldPropertysCurve()
    {
        var rig = await ReadyAsync();
        await rig.SeriesAsync();

        await rig.Settings.Settings.SaveAsync(
            new SeoReportSettings { Ga4PropertyId = "987654321", SearchConsoleSite = ReadySettingsHarness.Site }, null, false);
        await rig.SeriesAsync();

        Assert.Equal(2, rig.Google.Ga4.Calls.Count);
        Assert.Contains("properties/987654321:runReport", rig.Google.Ga4.Calls[1].Uri.AbsoluteUri);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Not configured, and what Google says
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task WithoutSettings_BothCallsThrowNotConfigured_AndAskNothing()
    {
        var rig = Create(new SettingsHarness());

        var series = await Assert.ThrowsAsync<ReportFetchException>(() => rig.SeriesAsync());
        var sources = await Assert.ThrowsAsync<ReportFetchException>(() => rig.Service.GetSourcesAsync(new DateOnly(2026, 8, 10), CancellationToken.None));

        Assert.Equal("notConfigured", series.Code);
        Assert.Equal("notConfigured", sources.Code);
        Assert.Equal(0, rig.Google.Ga4ServiceRequests);
    }

    [Fact]
    public async Task WithAKeyThatCannotBeRead_BothCallsThrowKeyUnreadable()
    {
        var settings = new SettingsHarness();
        await settings.SeedSettingsAsync(new { ga4PropertyId = ReadySettingsHarness.Ga4Property, searchConsoleSite = ReadySettingsHarness.Site });
        await settings.Store.SaveDocumentAsync(DocumentKeys.ProtectedKey, "not json");
        var rig = Create(settings);

        var series = await Assert.ThrowsAsync<ReportFetchException>(() => rig.SeriesAsync());
        var sources = await Assert.ThrowsAsync<ReportFetchException>(() => rig.Service.GetSourcesAsync(new DateOnly(2026, 8, 10), CancellationToken.None));

        Assert.Equal("keyUnreadable", series.Code);
        Assert.Equal("keyUnreadable", sources.Code);
        Assert.Equal(0, rig.Google.Ga4ServiceRequests);
    }

    [Fact]
    public async Task ACredentialErrorFromTheClients_PropagatesWithItsCode_AndIsNotCached()
    {
        var rig = await ReadyAsync();
        rig.Google.ServiceFailure = new ReportFetchException("keyInvalid");

        var ex = await Assert.ThrowsAsync<ReportFetchException>(() => rig.SeriesAsync());
        Assert.Equal("keyInvalid", ex.Code);

        rig.Google.ServiceFailure = null;
        var (series, _) = await rig.SeriesAsync();
        Assert.Equal(30, series.Points.Count);
    }

    [Fact]
    public async Task AGoogleError_PropagatesUnchanged_AndIsNotCached()
    {
        var rig = await ReadyAsync();
        rig.Google.Ga4Failure = System.Net.HttpStatusCode.Forbidden;

        await Assert.ThrowsAsync<GoogleApiException>(() => rig.SeriesAsync());
        await Assert.ThrowsAsync<GoogleApiException>(() => rig.Service.GetSourcesAsync(new DateOnly(2026, 8, 10), CancellationToken.None));

        rig.Google.Ga4Failure = null;
        await rig.SeriesAsync();
        await rig.Service.GetSourcesAsync(new DateOnly(2026, 8, 10), CancellationToken.None);
        Assert.Equal(4, rig.Google.Ga4.Calls.Count); // the two that failed and the two that went through
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The sources of one day
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Sources_AreThoseOfThatDay()
    {
        var rig = await ReadyAsync();
        var day = new DateOnly(2026, 8, 10);

        var sources = await rig.Service.GetSourcesAsync(day, CancellationToken.None);

        Assert.Equal(
            [new Ga4DailySource("Email", "newsletter", 40), new Ga4DailySource("Organic Search", "google", 25)],
            sources);
        var call = Assert.Single(rig.Google.Ga4.Calls);
        Assert.Contains("2026-08-10", call.Body);
        Assert.Contains("properties/123456789:runReport", call.Uri.AbsoluteUri);
    }

    [Fact]
    public async Task Sources_AreCachedForThirtyMinutes_PerDay()
    {
        var rig = await ReadyAsync();
        var tenth = new DateOnly(2026, 8, 10);

        await rig.Service.GetSourcesAsync(tenth, CancellationToken.None);
        await rig.Service.GetSourcesAsync(tenth, CancellationToken.None);
        Assert.Single(rig.Google.Ga4.Calls);

        await rig.Service.GetSourcesAsync(new DateOnly(2026, 8, 11), CancellationToken.None);
        Assert.Equal(2, rig.Google.Ga4.Calls.Count);

        rig.Advance(CacheLife - TimeSpan.FromMinutes(1));
        await rig.Service.GetSourcesAsync(tenth, CancellationToken.None);
        Assert.Equal(2, rig.Google.Ga4.Calls.Count);

        rig.Advance(TimeSpan.FromMinutes(2));
        await rig.Service.GetSourcesAsync(tenth, CancellationToken.None);
        Assert.Equal(3, rig.Google.Ga4.Calls.Count);
    }

    [Fact]
    public async Task Sources_OfAnEmptyDay_AreAnEmptyList_NotAnError()
    {
        var settings = await ReadySettingsHarness.ReadyAsync();
        var google = new SyntheticGoogle(new DateOnly(2026, 8, 12), empty: true);
        var service = new DailyTrafficService(settings.Settings, google, new MemoryCache(new MemoryCacheOptions()), settings.Time);

        Assert.Empty(await service.GetSourcesAsync(new DateOnly(2026, 8, 10), CancellationToken.None));

        var (series, _) = await service.GetSeriesAsync(null, CancellationToken.None);
        Assert.Equal(0, series.Total);
        Assert.Null(series.Peak);
        Assert.Null(series.ChangePercent);
    }
}
