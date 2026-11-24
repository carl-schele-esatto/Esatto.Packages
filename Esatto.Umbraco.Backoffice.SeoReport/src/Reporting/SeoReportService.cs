using System.Text.Json;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

/// <summary>
/// Fetches a month from GA4 and Search Console, builds the report with <see cref="ReportMapper"/>, and keeps the archive (see
/// <see cref="ISeoReportService"/>). Registered as a singleton: the gate and the caches are what make one fetch at a time and
/// cheap reads possible, and they only work if every request shares them.
///
/// THE ARCHIVE GROWS. Each month is a row of its own in the <c>Report</c> table. One row that every fetch overwrote would make
/// the previous month's view (KPI cards, brand split, target queries) vanish when the next month arrived.
/// </summary>
public sealed class SeoReportService : ISeoReportService
{
    /// <summary>
    /// The version of what a stored report contains, saved with every archived report. It is a different thing from the report's
    /// own <c>schema_version</c> (the shape of the JSON): it moves when the CONTENT of an existing member changes too, or when
    /// the fetch gathers more, so that an archived month can be told apart from what this code would make now. A report with a
    /// lower version counts as missing and is fetched again; a higher one (a newer version beside this one during a rolling
    /// deploy) is served as it is.
    /// </summary>
    public const int ContractVersion = 1;

    /// <summary>
    /// The margin before a finished month is considered fetchable. Deliberately more than <see cref="GscAgent.FallbackLagDays"/>
    /// and a constant of its own: that number documents Search Console's lag and is the fallback inside a fetch, while this is a
    /// decision about how long WE wait. The archive only fetches months that are missing, so a month fetched too early stays with
    /// its last days cut off, for good, and the next year's comparison is then made against it. Two extra days of waiting cost
    /// nothing in a monthly report.
    /// </summary>
    private const int PeriodSettleDays = 5;

    // The period list is short-lived because another instance can add a month; a report is immutable per month but can be fetched
    // again (Search Console adjusts its data for some days after a month ends), so an hour is short enough to show that and long
    // enough not to read the database for nothing.
    private static readonly TimeSpan PeriodsCacheLife = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ReportCacheLife = TimeSpan.FromHours(1);

    // A connected site that answered nothing is archived, so the dashboard can say "no data yet", and is looked at again after this.
    private static readonly TimeSpan NoDataReportLife = TimeSpan.FromHours(24);

    private const string PeriodsCacheKey = "Esatto.SeoReport.Periods";
    private const string ReportCachePrefix = "Esatto.SeoReport.Report.";

    // With no brand terms the brand split cannot be made. Search Console still gets a pattern, one that matches no query, so the
    // fetch works; the report then leaves the split out (see FetchAsync).
    private const string NeverMatchingPattern = "(?i)^$";

    private const string OutOfRangeMessage =
        "The month is later than the latest month that should be archived, or earlier than the months Search Console keeps.";

    private readonly ISeoReportStore _store;
    private readonly SettingsService _settings;
    private readonly IGoogleClients _clients;
    private readonly ClockedCache _cache;
    private readonly TimeProvider _time;
    private readonly ILogger<SeoReportService>? _logger;

    // ONE FETCH AT A TIME in this process. Without it two clicks start 90 calls in parallel and burn quota for nothing. The gate
    // surrounds ONE month, so a backfill lets go of it between the months. It is a different thing from the database lock the
    // fetch coordinator holds across a whole backfill: that one keeps other INSTANCES out, this one keeps our own requests out.
    private readonly SemaphoreSlim _gate = new(1, 1);

    // The refreshes finished so far, and for each month the number of the refresh that last archived it and the settings that one
    // was made with. Read and written only while the gate is held. A refresh that finds its month archived by a refresh numbered
    // higher than what it saw when it asked has waited while that one ran, and may use its report if it was asked with the same
    // settings. A counter and not a timestamp: a refresh that merely comes later must fetch, even on a coarse clock.
    private long _archived;
    private readonly Dictionary<Period, (long Number, ReadySettings Settings)> _archivedAt = [];

    public SeoReportService(
        ISeoReportStore store,
        SettingsService settings,
        IGoogleClients clients,
        [FromKeyedServices(ServiceCollectionExtensions.CacheKey)] IMemoryCache cache,
        TimeProvider time,
        ILogger<SeoReportService>? logger = null)
    {
        _store = store;
        _settings = settings;
        _clients = clients;
        _cache = new ClockedCache(cache, time);
        _time = time;
        _logger = logger;
    }

    /// <summary>The report as it was archived, with when.</summary>
    private sealed record Archived(Report Report, DateTimeOffset FetchedUtc);

    public Period ExpectedLatestPeriod => GscAgent.LatestCompleteMonth(Today().AddDays(-PeriodSettleDays));

    private DateOnly Today() => DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

    // -------------------------------------------------------------------------
    // Reading
    // -------------------------------------------------------------------------

    // EVERY READ IS MADE AGAINST THE SETTINGS IN FORCE AT THAT MOMENT. A report is only valid for the GA4 property and the Search
    // Console site it was built from (see BuiltFor), and the settings can change at any time, so each read asks for them (the
    // settings service keeps them cached, and a save takes effect on the next read). The caches below hold what was archived
    // whatever it was built for and are never keyed by the settings: the comparison is what keeps a changed property from being
    // served from a cache, and what lets the old months be valid again if the settings go back. When the package is not ready
    // (no usable key or a property missing) there is nothing to compare with, and a read returns what is archived.

    public async Task<IReadOnlyList<string>> GetAvailablePeriodsAsync(CancellationToken ct = default)
        => [.. (await ListAsync(await _settings.TryGetReadyAsync(), ct)).Select(period => period.ToString())];

    public async Task<bool> IsUpToDateAsync(CancellationToken ct = default)
        => !await NeedsFetchAsync(ExpectedLatestPeriod, ct);

    public async Task<bool> NeedsFetchAsync(Period period, CancellationToken ct = default)
    {
        PeriodGuard.Require(period);

        // Missing, damaged, from an older contract, or built for other settings than the ones in force.
        if (await LoadAsync(period, await _settings.TryGetReadyAsync(), ct) is not { } archived) return true;

        return archived.Report.DataStatus == ReportMapper.NoDataStatus && _time.GetUtcNow() - archived.FetchedUtc > NoDataReportLife;
    }

    public async Task<Report?> GetReportAsync(string? period = null, CancellationToken ct = default)
        => (await ResolveAsync(period, ct))?.Report;

    public async Task<DateTimeOffset?> GetFetchedUtcAsync(string? period = null, CancellationToken ct = default)
        => (await ResolveAsync(period, ct))?.FetchedUtc;

    /// <summary>
    /// The archived months that can be returned for <paramref name="current"/>, oldest first. A month whose report is damaged,
    /// from an older contract or built for other settings is left out, so that "listed" and "can be returned" always agree.
    /// What is readable at all is cached for five minutes (the store is asked once, and the rows that cannot be read are not
    /// looked at again on every call); which of those were built for <paramref name="current"/> is worked out on every call from
    /// the report cache, which costs a database read per month only the first time.
    /// </summary>
    private async Task<IReadOnlyList<Period>> ListAsync(ReadySettings? current, CancellationToken ct)
    {
        var readable = await ReadableAsync(ct);
        if (current is null) return readable;

        var matching = new List<Period>(readable.Count);
        foreach (var period in readable)
        {
            if (await LoadAsync(period, current, ct) is not null) matching.Add(period);
        }

        return matching;
    }

    /// <summary>The archived months whose report can be read, whatever it was built for, oldest first.</summary>
    private async Task<IReadOnlyList<Period>> ReadableAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_cache.TryGet(PeriodsCacheKey, out IReadOnlyList<Period>? cached)) return cached;

        var readable = new List<Period>();
        foreach (var period in await _store.ListPeriodsAsync())
        {
            if (await LoadStoredAsync(period, ct) is not null) readable.Add(period);
        }

        // Ordered here and not left to the store: the order is this interface's promise.
        readable.Sort();

        IReadOnlyList<Period> result = readable;
        _cache.Set(PeriodsCacheKey, result, PeriodsCacheLife);
        return result;
    }

    /// <summary>
    /// The month asked for, or the newest when none was given (null or blank): the newest that was built for the settings in
    /// force. Anything else must be exactly <c>YYYY-MM</c>; a value that is not is "no such report" and goes no further, so it
    /// can never become a key or a query.
    /// </summary>
    private async Task<Archived?> ResolveAsync(string? text, CancellationToken ct)
    {
        var current = await _settings.TryGetReadyAsync();

        if (string.IsNullOrWhiteSpace(text))
        {
            var all = await ListAsync(current, ct);
            return all.Count > 0 ? await LoadAsync(all[^1], current, ct) : null;
        }

        return Period.TryParse(text, out var period) ? await LoadAsync(period, current, ct) : null;
    }

    /// <summary>
    /// Whether the month's report can be used for <paramref name="current"/>: it was built for the same GA4 property and the same
    /// Search Console site (compared exactly). Without this a wrong but valid property id archives ANOTHER site's data, which then
    /// stays for good: nothing would look missing after the id is corrected. A report that records no sources (older or made by
    /// hand) cannot be shown to match and counts as not built for these settings. With no current settings (the package is not
    /// ready) everything archived is usable. Only the two properties are compared; the brand terms are not.
    /// </summary>
    private static bool BuiltFor(Report report, ReadySettings? current)
        => current is null
           || (report.Measured is { } measured
               && string.Equals(measured.Ga4Property, current.Ga4PropertyId, StringComparison.Ordinal)
               && string.Equals(measured.GscProperty, current.SearchConsoleSite, StringComparison.Ordinal));

    /// <summary>The month's archived report if it is usable for <paramref name="current"/> (see <see cref="BuiltFor"/>), otherwise null.</summary>
    private async Task<Archived?> LoadAsync(Period period, ReadySettings? current, CancellationToken ct)
        => await LoadStoredAsync(period, ct) is { } archived && BuiltFor(archived.Report, current) ? archived : null;

    /// <summary>
    /// The cache first, the store as the fallback. Null (and not cached) when there is no row, when its contract is older than
    /// this code's, or when it cannot be read: a missing month is looked for again on the next call, so a month archived by
    /// another instance shows up without waiting for an expiry.
    /// </summary>
    private async Task<Archived?> LoadStoredAsync(Period period, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var key = ReportCachePrefix + period;
        if (_cache.TryGet(key, out Archived? cached)) return cached;

        var row = await _store.GetReportAsync(period);
        if (row is null || row.ContractVersion < ContractVersion) return null;

        if (Deserialize(row.Json, period) is not { } report) return null;

        // The column holds no time zone and is always UTC.
        var archived = new Archived(report, new DateTimeOffset(DateTime.SpecifyKind(row.FetchedUtc, DateTimeKind.Utc)));
        _cache.Set(key, archived, ReportCacheLife);
        return archived;
    }

    /// <summary>
    /// The report in a row, or null when the row cannot be read: not JSON, or JSON that is not a report (a stored <c>{}</c> has
    /// no traffic section, and a reader would fail on it). A damaged row must not take the dashboard down; it counts as missing,
    /// so the next fetch of the month replaces it. Only the period is logged, never the content.
    /// </summary>
    private Report? Deserialize(string json, Period period)
    {
        try
        {
            if (JsonSerializer.Deserialize<Report>(json) is { Traffic: not null, Sources: not null } report) return report;
        }
        catch (JsonException)
        {
            // handled below, with the other kind of damage
        }

        _logger?.LogWarning("The archived report for {Period} cannot be read and counts as missing.", period);
        return null;
    }

    // -------------------------------------------------------------------------
    // Refreshing
    // -------------------------------------------------------------------------

    public async Task<Report> RefreshAsync(Period period, CancellationToken ct = default)
    {
        // First of all, before anything is awaited: how many refreshes had finished when this one asked.
        var seen = Interlocked.Read(ref _archived);

        PeriodGuard.Require(period);
        RequireFetchable(period);

        // Before queueing: with no usable setup there is nothing to wait for. The values read here are the ones in force when the
        // refresh was asked for, which is what the caller meant even if it then waits behind another fetch.
        var settings = await _settings.RequireReadyAsync();

        await _gate.WaitAsync(ct);
        try
        {
            // This month was archived by a refresh that ran while this one waited, and that refresh was asked with the SAME
            // settings: its report is as new as ours would be and says the same about what it measured. After a change of
            // property or brand terms in between it is not, and this refresh fetches for itself.
            if (_archivedAt.TryGetValue(period, out var last)
                && last.Number > seen
                && settings.Equals(last.Settings)
                && await LoadAsync(period, settings, ct) is { } fresh)
            {
                return fresh.Report;
            }

            _logger?.LogInformation("Fetching the report for {Period} from Google.", period);

            var report = await FetchAsync(period, settings, ct);
            await ArchiveAsync(period, report, settings);

            _logger?.LogInformation("The report for {Period} is archived.", period);
            return report;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Refuses a month a fetch cannot honestly cover, before anything else happens. Later than <see cref="ExpectedLatestPeriod"/>
    /// (a month still running, or ended less than <see cref="PeriodSettleDays"/> days ago) it would be archived incomplete, and
    /// an archived month is never looked at again, so the cut-off figures would stay for good. Earlier than the first month
    /// inside Search Console's window (<see cref="GscAgent.EarliestAvailableMonth"/>) the Search Console half does not exist and
    /// cannot ever be fetched. Both ends are inclusive and follow the injected clock. The message is fixed text.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The month is outside that range.</exception>
    private void RequireFetchable(Period period)
    {
        if (period > ExpectedLatestPeriod || period < GscAgent.EarliestAvailableMonth(Today()))
        {
            throw new ArgumentOutOfRangeException(nameof(period), OutOfRangeMessage);
        }
    }

    /// <summary>
    /// GA4 first, then Search Console, one call at a time. Both are hard requirements: a report with half the traffic section
    /// silently missing is harder to notice than an error. Nothing is caught here, so a failed month archives nothing.
    /// </summary>
    private async Task<Report> FetchAsync(Period period, ReadySettings settings, CancellationToken ct)
    {
        var brandPattern = BrandPattern.Build(settings.BrandTerms);

        var rows = await new Ga4Agent(settings.Ga4PropertyId, _clients).FetchAsync(period, ct);
        var search = await new GscAgent(settings.SearchConsoleSite, brandPattern ?? NeverMatchingPattern, _clients, Today()).FetchAsync(period, ct);

        // The properties and the brand pattern go INTO the report, so it can say what it measured: without them no figure can be
        // reproduced in Google's own interfaces. With no brand terms the pattern is null and the report omits the brand split.
        return ReportMapper.Build(period, rows, search, settings.SearchConsoleSite, settings.Ga4PropertyId, brandPattern);
    }

    /// <summary>
    /// Writes the report, makes it the cached one and drops the cached period list (the month may be new), and notes which
    /// refresh archived the month and with which settings. Runs under the gate.
    /// </summary>
    private async Task ArchiveAsync(Period period, Report report, ReadySettings settings)
    {
        var fetched = _time.GetUtcNow();
        await _store.SaveReportAsync(ReportEntity.From(period, JsonSerializer.Serialize(report), fetched.UtcDateTime, ContractVersion));

        _cache.Set(ReportCachePrefix + period, new Archived(report, fetched), ReportCacheLife);
        _cache.Remove(PeriodsCacheKey);

        _archivedAt[period] = (Interlocked.Increment(ref _archived), settings);
    }
}
