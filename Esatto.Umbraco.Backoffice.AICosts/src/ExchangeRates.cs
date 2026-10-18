using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Esatto.Umbraco.Backoffice.AICosts;

/// <summary>Exchange rates expressed as units of each currency per 1 unit of the base currency.</summary>
public sealed record ExchangeRates(DateOnly Date, string Source, IReadOnlyDictionary<string, decimal> PerBaseUnit);

public interface IExchangeRateProvider
{
    /// <summary>Latest rates relative to <paramref name="baseCurrency"/>, or <c>null</c> when they
    /// can't be fetched (the Costs tab then shows the base currency only).</summary>
    Task<ExchangeRates?> GetRatesAsync(string baseCurrency, CancellationToken cancellationToken);
}

/// <summary>
/// European Central Bank daily reference rates (EUR-based, published around 16:00 CET on TARGET
/// working days; no API key). Cached in-process: 6 hours after a success, 15 minutes after a
/// failure so an ECB outage doesn't add a timeout to every Costs tab load.
/// </summary>
public sealed class EcbExchangeRateProvider : IExchangeRateProvider
{
    public const string FeedUrl = "https://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml";
    public const string HttpClientName = "Esatto.AICosts.Ecb";

    private static readonly TimeSpan SuccessTtl = TimeSpan.FromHours(6);
    private static readonly TimeSpan FailureTtl = TimeSpan.FromMinutes(15);

    // Registered as a singleton so the cache below survives across requests; HttpClients come
    // from the factory per fetch (a typed client would make this class transient).
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EcbExchangeRateProvider> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private ExchangeRates? _eurRates;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public EcbExchangeRateProvider(IHttpClientFactory httpClientFactory, TimeProvider timeProvider, ILogger<EcbExchangeRateProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ExchangeRates?> GetRatesAsync(string baseCurrency, CancellationToken cancellationToken)
    {
        var eurRates = await GetEurRatesAsync(cancellationToken);
        return eurRates is null ? null : Rebase(eurRates, baseCurrency);
    }

    private async Task<ExchangeRates?> GetEurRatesAsync(CancellationToken cancellationToken)
    {
        if (_timeProvider.GetUtcNow() < _expiresAt) return _eurRates;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_timeProvider.GetUtcNow() < _expiresAt) return _eurRates;

            try
            {
                var xml = await _httpClientFactory.CreateClient(HttpClientName).GetStringAsync(FeedUrl, cancellationToken);
                _eurRates = ParseEcbXml(xml);
                _expiresAt = _timeProvider.GetUtcNow() + SuccessTtl;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller went away (e.g. the browser aborted the page load): not an ECB outage, so
                // nothing is cached and the next request fetches again. A 10 s HttpClient timeout still
                // lands below, because the caller's token isn't cancelled then.
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FormatException or XmlException)
            {
                // Keep serving the last good rates if we have them; otherwise the Costs tab falls
                // back to the base currency.
                _logger.LogWarning(ex, "AICosts: could not fetch ECB exchange rates.");
                _expiresAt = _timeProvider.GetUtcNow() + FailureTtl;
            }

            return _eurRates;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Parses the ECB eurofxref-daily feed into EUR-based rates (EUR itself = 1).</summary>
    public static ExchangeRates ParseEcbXml(string xml)
    {
        var cubes = XDocument.Parse(xml).Descendants().Where(e => e.Name.LocalName == "Cube").ToList();

        var dated = cubes.FirstOrDefault(c => c.Attribute("time") is not null)
            ?? throw new FormatException("ECB feed has no dated Cube element.");
        var date = DateOnly.ParseExact(dated.Attribute("time")!.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["EUR"] = 1m };
        foreach (var cube in cubes)
        {
            var currency = cube.Attribute("currency")?.Value;
            var rate = cube.Attribute("rate")?.Value;
            if (currency is null || rate is null) continue;
            rates[currency] = decimal.Parse(rate, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        if (rates.Count == 1) throw new FormatException("ECB feed contains no rates.");
        return new ExchangeRates(date, "European Central Bank", rates);
    }

    /// <summary>Converts EUR-based rates to <paramref name="baseCurrency"/>-based rates.
    /// Returns <c>null</c> when the ECB doesn't quote the base currency.</summary>
    public static ExchangeRates? Rebase(ExchangeRates eurRates, string baseCurrency)
    {
        if (!eurRates.PerBaseUnit.TryGetValue(baseCurrency, out var basePerEur) || basePerEur == 0) return null;

        var rebased = eurRates.PerBaseUnit.ToDictionary(
            kv => kv.Key,
            kv => kv.Value / basePerEur,
            StringComparer.OrdinalIgnoreCase);

        return eurRates with { PerBaseUnit = rebased };
    }
}
