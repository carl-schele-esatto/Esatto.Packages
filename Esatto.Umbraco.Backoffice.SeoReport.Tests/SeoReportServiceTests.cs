using System.Text.Json;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The report archive and what reads it. One report per month, kept for good and never overwritten by another month; the lists
/// and the reports are cached for a while and the cache is dropped by a fetch; a report written under an older contract counts as
/// missing; and a period that is not a month never reaches the store. This half covers reading and the caches, the other half
/// (<c>SeoReportServiceTests.Refresh.cs</c>) the fetch itself and how fetches queue up.
/// </summary>
public partial class SeoReportServiceTests
{
    private static readonly Period July = Period.Parse("2026-07");

    private static readonly TimeSpan PeriodListLife = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan ReportLife = TimeSpan.FromHours(1);

    /// <summary>
    /// The service over an in-memory archive, a synthetic Google and a clock the test moves. The clock starts on
    /// <see cref="ReadySettingsHarness.Now"/>, 2026-08-20, when July is the latest month that should be archived.
    /// </summary>
    private sealed class Rig
    {
        private Rig(SettingsHarness settings, bool empty, DateTimeOffset? now = null)
        {
            settings.Time.SetUtcNow(now ?? ReadySettingsHarness.Now);
            Settings = settings;
            Raw = settings.Store;
            Store = new CountingStore(Raw);
            Google = new SyntheticGoogle(new DateOnly(2026, 8, 12), empty);
            Log = new CapturingLogger<SeoReportService>();
            Service = new SeoReportService(Store, settings.Settings, Google, new MemoryCache(new MemoryCacheOptions()), settings.Time, Log);
        }

        public SettingsHarness Settings { get; }

        public InMemoryStore Raw { get; }

        public CountingStore Store { get; }

        public SyntheticGoogle Google { get; }

        public CapturingLogger<SeoReportService> Log { get; }

        public SeoReportService Service { get; }

        public FakeTimeProvider Time => Settings.Time;

        /// <summary>A rig with a key and both properties set (and the given brand terms), so a fetch can run.</summary>
        public static async Task<Rig> ReadyAsync(bool empty = false, string[]? brandTerms = null, DateTimeOffset? now = null)
            => new(await ReadySettingsHarness.ReadyAsync(brandTerms ?? []), empty, now);

        /// <summary>A rig on a new installation: no key, no property.</summary>
        public static Rig NotConfigured(DateTimeOffset? now = null) => new(new SettingsHarness(), empty: false, now);

        /// <summary>A rig over settings the test has prepared itself.</summary>
        public static Rig Over(SettingsHarness settings) => new(settings, empty: false);

        /// <summary>
        /// Puts a report in the archive behind the service's back, as another instance or an earlier version would. By default the
        /// report records the properties a ready rig is set up with, so it counts as built for them; pass others to seed a report
        /// built for a different property, or <paramref name="recordMeasured"/> false for one that records nothing.
        /// </summary>
        public Task SeedAsync(
            string period,
            int contractVersion = SeoReportService.ContractVersion,
            DateTimeOffset? fetched = null,
            string marker = "seeded",
            string? status = null,
            string ga4Property = ReadySettingsHarness.Ga4Property,
            string site = ReadySettingsHarness.Site,
            bool recordMeasured = true)
        {
            var report = TestReports.With(period) with
            {
                GeneratedAt = marker,
                DataStatus = status,
                Measured = recordMeasured ? new Measured(site, ga4Property, null) : null,
            };
            return SeedJsonAsync(period, JsonSerializer.Serialize(report), contractVersion, fetched);
        }

        /// <summary>Saves new settings, the way a person does on the Settings tab; they are in force at once.</summary>
        public Task UseAsync(string ga4Property = ReadySettingsHarness.Ga4Property, string site = ReadySettingsHarness.Site, string[]? brandTerms = null)
            => Settings.Settings.SaveAsync(
                new SeoReportSettings { Ga4PropertyId = ga4Property, SearchConsoleSite = site, BrandTerms = brandTerms ?? [] }, null, false);

        public Task SeedJsonAsync(string period, string json, int contractVersion = SeoReportService.ContractVersion, DateTimeOffset? fetched = null)
            => Raw.SaveReportAsync(ReportEntity.From(Period.Parse(period), json, (fetched ?? Time.GetUtcNow()).UtcDateTime, contractVersion));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // An empty archive
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task AnEmptyArchive_HasNoPeriods_NoReport_NoFetchTime_AndIsNotUpToDate()
    {
        var rig = Rig.NotConfigured();

        Assert.Empty(await rig.Service.GetAvailablePeriodsAsync());
        Assert.Null(await rig.Service.GetReportAsync());
        Assert.Null(await rig.Service.GetReportAsync("2026-07"));
        Assert.Null(await rig.Service.GetFetchedUtcAsync());
        Assert.False(await rig.Service.IsUpToDateAsync());
        Assert.True(await rig.Service.NeedsFetchAsync(July));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Lookup
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Periods_AreListedOldestFirst_WhateverOrderTheyWereArchivedIn()
    {
        var rig = Rig.NotConfigured();
        await rig.SeedAsync("2026-07");
        await rig.SeedAsync("2025-12");
        await rig.SeedAsync("2026-05");

        Assert.Equal(["2025-12", "2026-05", "2026-07"], await rig.Service.GetAvailablePeriodsAsync());
    }

    [Fact]
    public async Task WithoutAPeriod_TheNewestReportIsReturned()
    {
        var rig = Rig.NotConfigured();
        await rig.SeedAsync("2026-05", marker: "may", fetched: new DateTimeOffset(2026, 6, 6, 5, 0, 0, TimeSpan.Zero));
        await rig.SeedAsync("2026-07", marker: "july", fetched: new DateTimeOffset(2026, 8, 6, 5, 0, 0, TimeSpan.Zero));
        await rig.SeedAsync("2026-06", marker: "june", fetched: new DateTimeOffset(2026, 7, 6, 5, 0, 0, TimeSpan.Zero));

        Assert.Equal("july", (await rig.Service.GetReportAsync())!.GeneratedAt);
        Assert.Equal(new DateTimeOffset(2026, 8, 6, 5, 0, 0, TimeSpan.Zero), await rig.Service.GetFetchedUtcAsync());
    }

    [Fact]
    public async Task AGivenPeriod_ReturnsThatMonthsReportAndFetchTime()
    {
        var rig = Rig.NotConfigured();
        await rig.SeedAsync("2026-05", marker: "may", fetched: new DateTimeOffset(2026, 6, 6, 5, 0, 0, TimeSpan.Zero));
        await rig.SeedAsync("2026-07", marker: "july");

        var report = await rig.Service.GetReportAsync("2026-05");

        Assert.Equal("may", report!.GeneratedAt);
        Assert.Equal(new DateTimeOffset(2026, 6, 6, 5, 0, 0, TimeSpan.Zero), await rig.Service.GetFetchedUtcAsync("2026-05"));
        Assert.Equal(TimeSpan.Zero, (await rig.Service.GetFetchedUtcAsync("2026-05"))!.Value.Offset);
    }

    [Fact]
    public async Task AMonthThatIsNotArchived_IsNull_EvenWhenOthersAre()
    {
        var rig = Rig.NotConfigured();
        await rig.SeedAsync("2026-07");

        Assert.Null(await rig.Service.GetReportAsync("2026-06"));
        Assert.Null(await rig.Service.GetFetchedUtcAsync("2026-06"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public async Task ANullOrBlankPeriod_MeansTheNewest(string? period)
    {
        var rig = Rig.NotConfigured();
        await rig.SeedAsync("2026-05", marker: "may");
        await rig.SeedAsync("2026-07", marker: "july");

        Assert.Equal("july", (await rig.Service.GetReportAsync(period))!.GeneratedAt);
        Assert.NotNull(await rig.Service.GetFetchedUtcAsync(period));
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026-00")]
    [InlineData("../x")]
    [InlineData("2026-07/../2026-06")]
    [InlineData(" 2026-07")]
    [InlineData("2026-07 ")]
    [InlineData("2026-7")]
    [InlineData("202607")]
    [InlineData("2026-07-01")]
    [InlineData("'; DROP TABLE esattoSeoReportReport;--")]
    public async Task APeriodThatIsNotYyyyMm_IsNull_AndNeverReachesTheStore_NorFallsBackToTheNewest(string period)
    {
        var rig = Rig.NotConfigured();
        await rig.SeedAsync("2026-07"); // a newest report exists: a fallback would find it

        Assert.Null(await rig.Service.GetReportAsync(period));
        Assert.Null(await rig.Service.GetFetchedUtcAsync(period));
        Assert.Equal(0, rig.Store.ReportReads);
        Assert.Equal(0, rig.Store.PeriodListReads);
    }

    [Fact]
    public async Task ADefaultPeriod_IsRefused_BeforeTheStoreOrGoogleIsTouched()
    {
        var rig = await Rig.ReadyAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => rig.Service.NeedsFetchAsync(default));
        await Assert.ThrowsAsync<ArgumentException>(() => rig.Service.RefreshAsync(default));

        Assert.Equal(0, rig.Store.ReportReads);
        Assert.Equal(0, rig.Store.ReportWrites);
        Assert.Equal(0, rig.Google.Ga4ServiceRequests);
        Assert.Equal(0, rig.Google.SearchConsoleServiceRequests);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The latest month that should be archived
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// "Five days after the month ends": today minus five days, rounded to the latest month that is entirely behind it. On the
    /// 4th of September that still points at 30 August, so the latest month is July; on the 5th it reaches 31 August.
    /// </summary>
    [Theory]
    [InlineData("2026-09-04", "2026-07")]
    [InlineData("2026-09-05", "2026-08")]
    [InlineData("2026-09-30", "2026-08")]
    [InlineData("2026-08-20", "2026-07")]
    [InlineData("2026-01-01", "2025-11")]
    [InlineData("2026-01-04", "2025-11")]
    [InlineData("2026-01-05", "2025-12")]
    [InlineData("2026-03-05", "2026-02")]
    [InlineData("2026-03-04", "2026-01")]
    [InlineData("2024-03-05", "2024-02")]
    [InlineData("2024-03-04", "2024-01")]
    public void ExpectedLatestPeriod_IsTheLatestMonthEndedFiveDaysAgo(string today, string expected)
    {
        var rig = Rig.NotConfigured(DateTimeOffset.Parse($"{today}T09:30:00Z", System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(Period.Parse(expected), rig.Service.ExpectedLatestPeriod);
    }

    [Fact]
    public void ExpectedLatestPeriod_FollowsTheInjectedClock_Not_TheSystemClock()
    {
        var rig = Rig.NotConfigured();

        rig.Time.SetUtcNow(new DateTimeOffset(2031, 2, 14, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(Period.Parse("2031-01"), rig.Service.ExpectedLatestPeriod);
    }

    [Fact]
    public async Task IsUpToDate_IsTrueExactlyWhenTheExpectedPeriodIsArchived()
    {
        var rig = Rig.NotConfigured(); // 2026-08-20: July is expected
        Assert.Equal(July, rig.Service.ExpectedLatestPeriod);
        Assert.False(await rig.Service.IsUpToDateAsync());

        await rig.SeedAsync("2026-06");
        Assert.False(await rig.Service.IsUpToDateAsync()); // an older month is not enough

        await rig.SeedAsync("2026-08");
        Assert.False(await rig.Service.IsUpToDateAsync()); // nor is a later one: it is that month that counts

        await rig.SeedAsync("2026-07");
        Assert.True(await rig.Service.IsUpToDateAsync());
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The contract version, and rows that cannot be read
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheContractVersion_IsOne() => Assert.Equal(1, SeoReportService.ContractVersion);

    [Fact]
    public async Task AReportFromAnOlderContract_CountsAsMissingEverywhere()
    {
        var rig = Rig.NotConfigured();
        await rig.SeedAsync("2026-06");
        await rig.SeedAsync("2026-07", contractVersion: SeoReportService.ContractVersion - 1);

        Assert.Null(await rig.Service.GetReportAsync("2026-07"));
        Assert.Null(await rig.Service.GetFetchedUtcAsync("2026-07"));
        Assert.Equal(["2026-06"], await rig.Service.GetAvailablePeriodsAsync());
        Assert.Equal("seeded", (await rig.Service.GetReportAsync())!.GeneratedAt); // the newest USABLE one: June
        Assert.True(await rig.Service.NeedsFetchAsync(July));
        Assert.False(await rig.Service.NeedsFetchAsync(Period.Parse("2026-06")));
        Assert.False(await rig.Service.IsUpToDateAsync()); // July is the expected one
    }

    [Fact]
    public async Task AReportFromANewerContract_IsServed_NotRefetched()
    {
        // Written by a newer version running beside this one during a rolling deploy: refetching would only write the old
        // contract over it and make the two versions undo each other's work.
        var rig = Rig.NotConfigured();
        await rig.SeedAsync("2026-07", contractVersion: SeoReportService.ContractVersion + 1, marker: "newer");

        Assert.Equal("newer", (await rig.Service.GetReportAsync("2026-07"))!.GeneratedAt);
        Assert.False(await rig.Service.NeedsFetchAsync(July));
        Assert.True(await rig.Service.IsUpToDateAsync());
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    public async Task ARowThatCannotBeRead_CountsAsMissing_AndNeverThrows(string json)
    {
        var rig = Rig.NotConfigured();
        await rig.SeedJsonAsync("2026-07", json);

        Assert.Null(await rig.Service.GetReportAsync("2026-07"));
        Assert.Null(await rig.Service.GetReportAsync());
        Assert.Empty(await rig.Service.GetAvailablePeriodsAsync());
        Assert.True(await rig.Service.NeedsFetchAsync(July));
    }

    [Fact]
    public async Task ADamagedRow_IsLoggedByPeriod_NeverByContent()
    {
        var rig = Rig.NotConfigured();
        await rig.SeedJsonAsync("2026-07", "{ \"secret-looking\": not json");

        await rig.Service.GetReportAsync("2026-07");

        Assert.Contains(rig.Log.Entries, entry => entry.Level == LogLevel.Warning && entry.Text.Contains("2026-07"));
        Assert.DoesNotContain("secret-looking", rig.Log.AllText);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // An archived "no data" report goes stale after a day
    // ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, false)]
    [InlineData(3_600, false)]
    [InlineData(86_399, false)]
    [InlineData(86_400, false)] // exactly a day is not older than a day
    [InlineData(86_401, true)]
    [InlineData(172_800, true)]
    public async Task ANoDataReport_NeedsAFetchOnlyOnceItIsOlderThanADay(int ageSeconds, bool needsFetch)
    {
        var rig = Rig.NotConfigured();
        await rig.SeedAsync("2026-07", status: "noData", fetched: rig.Time.GetUtcNow() - TimeSpan.FromSeconds(ageSeconds));

        Assert.Equal(needsFetch, await rig.Service.NeedsFetchAsync(July));
    }

    [Fact]
    public async Task ALiveReport_NeverGoesStaleByAge()
    {
        var rig = Rig.NotConfigured();
        await rig.SeedAsync("2026-07", status: "live", fetched: rig.Time.GetUtcNow() - TimeSpan.FromDays(200));
        await rig.SeedAsync("2026-06", status: null, fetched: rig.Time.GetUtcNow() - TimeSpan.FromDays(200));

        Assert.False(await rig.Service.NeedsFetchAsync(July));
        Assert.False(await rig.Service.NeedsFetchAsync(Period.Parse("2026-06")));
        Assert.True(await rig.Service.IsUpToDateAsync());
    }

    [Fact]
    public async Task ANoDataReport_StillCountsAsArchived_ForTheDashboard()
    {
        var rig = Rig.NotConfigured();
        await rig.SeedAsync("2026-07", status: "noData", fetched: rig.Time.GetUtcNow() - TimeSpan.FromDays(3));

        Assert.Equal(["2026-07"], await rig.Service.GetAvailablePeriodsAsync());
        Assert.Equal("noData", (await rig.Service.GetReportAsync("2026-07"))!.DataStatus);
        Assert.True(await rig.Service.NeedsFetchAsync(July));
        Assert.False(await rig.Service.IsUpToDateAsync());
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The caches
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task ThePeriodList_IsCachedForFiveMinutes()
    {
        var rig = Rig.NotConfigured();
        await rig.SeedAsync("2026-06");

        Assert.Equal(["2026-06"], await rig.Service.GetAvailablePeriodsAsync());
        Assert.Equal(1, rig.Store.PeriodListReads);

        await rig.SeedAsync("2026-07"); // another instance archived a month
        rig.Time.Advance(PeriodListLife - TimeSpan.FromMinutes(1));
        Assert.Equal(["2026-06"], await rig.Service.GetAvailablePeriodsAsync());
        Assert.Equal(1, rig.Store.PeriodListReads);

        rig.Time.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(["2026-06", "2026-07"], await rig.Service.GetAvailablePeriodsAsync());
        Assert.Equal(2, rig.Store.PeriodListReads);
    }

    [Fact]
    public async Task AReport_IsCachedForAnHour()
    {
        var rig = Rig.NotConfigured();
        await rig.SeedAsync("2026-07", marker: "v1");

        Assert.Equal("v1", (await rig.Service.GetReportAsync("2026-07"))!.GeneratedAt);
        Assert.Equal(1, rig.Store.ReportReads);

        await rig.SeedAsync("2026-07", marker: "v2"); // replaced behind the service's back
        rig.Time.Advance(ReportLife - TimeSpan.FromMinutes(1));
        Assert.Equal("v1", (await rig.Service.GetReportAsync("2026-07"))!.GeneratedAt);
        Assert.Equal(1, rig.Store.ReportReads);

        rig.Time.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal("v2", (await rig.Service.GetReportAsync("2026-07"))!.GeneratedAt);
        Assert.Equal(2, rig.Store.ReportReads);
    }

    [Fact]
    public async Task ACachedReport_IsTheSameObjectForEveryReader()
    {
        // Shared across requests, so nobody may change it (the findings filter returns copies); this pins that it is shared.
        var rig = Rig.NotConfigured();
        await rig.SeedAsync("2026-07");

        var first = await rig.Service.GetReportAsync("2026-07");
        var second = await rig.Service.GetReportAsync("2026-07");
        var newest = await rig.Service.GetReportAsync();

        Assert.Same(first, second);
        Assert.Same(first, newest);
    }

    [Fact]
    public async Task AMissingMonth_IsNotCached_SoANewArchiveEntryShowsAtOnce()
    {
        var rig = Rig.NotConfigured();

        Assert.Null(await rig.Service.GetReportAsync("2026-07"));
        Assert.True(await rig.Service.NeedsFetchAsync(July));

        await rig.SeedAsync("2026-07");

        Assert.NotNull(await rig.Service.GetReportAsync("2026-07"));
        Assert.False(await rig.Service.NeedsFetchAsync(July));
    }

    [Fact]
    public async Task EachMonth_HasItsOwnCacheEntry()
    {
        var rig = Rig.NotConfigured();
        await rig.SeedAsync("2026-06", marker: "june");
        await rig.SeedAsync("2026-07", marker: "july");

        Assert.Equal("june", (await rig.Service.GetReportAsync("2026-06"))!.GeneratedAt);
        Assert.Equal("july", (await rig.Service.GetReportAsync("2026-07"))!.GeneratedAt);
        Assert.Equal("june", (await rig.Service.GetReportAsync("2026-06"))!.GeneratedAt);
    }

    [Fact]
    public async Task ACancelledToken_StopsAReadBeforeItTouchesTheStore()
    {
        var rig = Rig.NotConfigured();
        await rig.SeedAsync("2026-07");
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rig.Service.GetReportAsync("2026-07", cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rig.Service.GetAvailablePeriodsAsync(cancelled.Token));
        Assert.Equal(0, rig.Store.ReportReads);
    }
}
