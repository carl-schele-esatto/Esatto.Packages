using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

/// <summary>
/// Day-fresh traffic for the strip at the top of the Traffic tab.
///
/// KEPT APART FROM <see cref="ISeoReportService"/> ON PURPOSE. The archive holds immutable months: a finished month never
/// changes, it is fetched once and kept. This is the opposite, rolling data, where the last days are revised afterwards and "the
/// last 30 days" means something new every morning. Putting both in one service would mean one cache with two incompatible
/// lifetimes.
/// </summary>
public interface IDailyTrafficService
{
    /// <summary>
    /// The daily curve with its comparison period, cached so repeated visits do not cost quota.
    /// </summary>
    /// <param name="month"><c>YYYY-MM</c> for a calendar month; null or empty for the rolling last thirty days.</param>
    /// <exception cref="ArgumentException"><paramref name="month"/> is anything else. The message is fixed and never repeats it.</exception>
    /// <exception cref="ReportFetchException">The package is not set up (<c>notConfigured</c>, <c>keyUnreadable</c>), or the key is invalid.</exception>
    /// <remarks>Errors from Google itself (<c>GoogleApiException</c>) propagate unchanged and are not cached.</remarks>
    Task<(Ga4DailySeries Series, Ga4DailyWindow Window)> GetSeriesAsync(string? month, CancellationToken ct);

    /// <summary>Where one day's sessions came from.</summary>
    /// <exception cref="ReportFetchException">As for <see cref="GetSeriesAsync"/>.</exception>
    Task<IReadOnlyList<Ga4DailySource>> GetSourcesAsync(DateOnly day, CancellationToken ct);
}

/// <inheritdoc />
public sealed class DailyTrafficService : IDailyTrafficService
{
    /// <summary>
    /// How long an answer is reused. The dashboard is opened often and the GA4 quota is shared with the monthly fetch, so without
    /// a cache every reload of the Traffic tab would cost a call. Half an hour is plenty: the data in practice updates once a
    /// day, so there is no fresher answer to be had.
    /// </summary>
    public static readonly TimeSpan CacheLife = TimeSpan.FromMinutes(30);

    private const string SeriesCachePrefix = "Esatto.SeoReport.Daily.Series.";
    private const string SourcesCachePrefix = "Esatto.SeoReport.Daily.Sources.";
    private const string RollingKey = "rolling";

    private readonly SettingsService _settings;
    private readonly IGoogleClients _clients;
    private readonly ClockedCache _cache;
    private readonly TimeProvider _time;

    public DailyTrafficService(
        SettingsService settings, IGoogleClients clients, [FromKeyedServices(ServiceCollectionExtensions.CacheKey)] IMemoryCache cache, TimeProvider time)
    {
        _settings = settings;
        _clients = clients;
        _cache = new ClockedCache(cache, time);
        _time = time;
    }

    private sealed record Curve(Ga4DailySeries Series, Ga4DailyWindow Window);

    private sealed record DaySources(IReadOnlyList<Ga4DailySource> Sources);

    public async Task<(Ga4DailySeries Series, Ga4DailyWindow Window)> GetSeriesAsync(string? month, CancellationToken ct)
    {
        // The month becomes part of a cache key, so it is checked before it goes anywhere.
        var period = ParseMonth(month);
        var settings = await _settings.RequireReadyAsync();
        var today = Today();

        // THE DATE IS IN THE KEY, so the cache lets go at midnight even when the half hour has not passed: otherwise the curve could
        // lack yesterday well into the morning. The month is in it so each month has an entry of its own instead of pushing out the
        // others, and the property so a changed property never serves the old one's curve.
        var key = $"{SeriesCachePrefix}{settings.Ga4PropertyId}|{Ga4Requests.Iso(today)}|{period ?? RollingKey}";
        if (_cache.TryGet(key, out Curve? cached)) return (cached.Series, cached.Window);

        var fetched = await new Ga4DailyAgent(settings.Ga4PropertyId, _clients).FetchAsync(today, period, ct);

        _cache.Set(key, new Curve(fetched.Series, fetched.Window), CacheLife);
        return fetched;
    }

    public async Task<IReadOnlyList<Ga4DailySource>> GetSourcesAsync(DateOnly day, CancellationToken ct)
    {
        var settings = await _settings.RequireReadyAsync();

        var key = $"{SourcesCachePrefix}{settings.Ga4PropertyId}|{Ga4Requests.Iso(day)}";
        if (_cache.TryGet(key, out DaySources? cached)) return cached.Sources;

        var sources = await new Ga4DailyAgent(settings.Ga4PropertyId, _clients).FetchSourcesAsync(day, ct);

        // A finished day hardly changes, but the first days after it can be revised. The same lifetime as the curve keeps the two
        // in step: a day in the curve showing one number and its sources adding up to another would only confuse.
        _cache.Set(key, new DaySources(sources), CacheLife);
        return sources;
    }

    private DateOnly Today() => DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

    /// <summary>Null (the rolling window) for null or empty text, the month as <c>YYYY-MM</c> for a valid one, otherwise a refusal.</summary>
    private static string? ParseMonth(string? month)
    {
        if (string.IsNullOrEmpty(month)) return null;

        return Period.TryParse(month, out var period)
            ? period.ToString()
            : throw new ArgumentException("The month must be written YYYY-MM.", nameof(month));
    }
}
