using Umbraco.AI.Core.Analytics.Usage;

namespace Esatto.Umbraco.Backoffice.AICosts;

public sealed record UsageTotals(long Requests, long InputTokens, long OutputTokens, long CachedInputTokens, long TotalTokens)
{
    public static readonly UsageTotals Empty = new(0, 0, 0, 0, 0);

    public UsageTotals Plus(UsageTotals o) => new(
        Requests + o.Requests, InputTokens + o.InputTokens, OutputTokens + o.OutputTokens,
        CachedInputTokens + o.CachedInputTokens, TotalTokens + o.TotalTokens);

    public UsageTotals Minus(UsageTotals o) => new(
        Requests - o.Requests, InputTokens - o.InputTokens, OutputTokens - o.OutputTokens,
        CachedInputTokens - o.CachedInputTokens, TotalTokens - o.TotalTokens);
}

/// <param name="PricedTokens">Tokens from models with a configured price.</param>
public sealed record PricedUsage(UsageTotals Totals, decimal Cost, long PricedTokens, long UnpricedTokens)
{
    public static readonly PricedUsage Empty = new(UsageTotals.Empty, 0, 0, 0);

    public PricedUsage Plus(PricedUsage o) => new(Totals.Plus(o.Totals), Cost + o.Cost, PricedTokens + o.PricedTokens, UnpricedTokens + o.UnpricedTokens);

    public PricedUsage Minus(PricedUsage o) => new(Totals.Minus(o.Totals), Cost - o.Cost, PricedTokens - o.PricedTokens, UnpricedTokens - o.UnpricedTokens);
}

/// <summary>Reads Umbraco.AI's usage statistics for a <see cref="UsageWindow"/> (Daily + Hourly parts).</summary>
public sealed class AICostQueries
{
    private readonly IAIUsageAnalyticsService _analytics;

    public AICostQueries(IAIUsageAnalyticsService analytics) => _analytics = analytics;

    public async Task<List<AIUsageTimeSeriesPoint>> GetSeriesAsync(UsageWindow window, AIUsageFilter filter, CancellationToken ct)
    {
        var points = new List<AIUsageTimeSeriesPoint>();
        if (window.HasDaily)
            points.AddRange(await _analytics.GetTimeSeriesAsync(window.From, window.HourlyFrom, AIUsagePeriod.Daily, filter, ct));
        if (window.HasHourly)
            points.AddRange(await _analytics.GetTimeSeriesAsync(window.HourlyFrom, window.To, AIUsagePeriod.Hourly, filter, ct));
        return points;
    }

    public async Task<IReadOnlyList<string>> GetModelIdsAsync(UsageWindow window, CancellationToken ct) =>
        (await GetItemsAsync(window, (from, to, period) => _analytics.GetBreakdownByModelAsync(from, to, period, ct)))
            .Select(i => i.Dimension).ToList();

    public Task<IReadOnlyList<AIUsageBreakdownItem>> GetBreakdownAsync(UsageWindow window, AICostDimension dimension, CancellationToken ct) =>
        GetItemsAsync(window, dimension switch
        {
            AICostDimension.Profile => (from, to, period) => _analytics.GetBreakdownByProfileAsync(from, to, period, ct),
            AICostDimension.User => (from, to, period) => _analytics.GetBreakdownByUserAsync(from, to, period, ct),
            AICostDimension.Provider => (from, to, period) => _analytics.GetBreakdownByProviderAsync(from, to, period, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(dimension)),
        });

    public async Task<UsageTotals> GetTotalsAsync(UsageWindow window, AIUsageFilter filter, CancellationToken ct)
    {
        var totals = UsageTotals.Empty;
        if (window.HasDaily)
            totals = totals.Plus(ToTotals(await _analytics.GetSummaryAsync(window.From, window.HourlyFrom, AIUsagePeriod.Daily, filter, ct)));
        if (window.HasHourly)
            totals = totals.Plus(ToTotals(await _analytics.GetSummaryAsync(window.HourlyFrom, window.To, AIUsagePeriod.Hourly, filter, ct)));
        return totals;
    }

    /// <summary>Average response time over the window, weighted by each part's request count (0 without requests).</summary>
    public async Task<int> GetAverageDurationMsAsync(UsageWindow window, CancellationToken ct)
    {
        long requests = 0;
        double weighted = 0;
        if (window.HasDaily) Add(await _analytics.GetSummaryAsync(window.From, window.HourlyFrom, AIUsagePeriod.Daily, new AIUsageFilter(), ct));
        if (window.HasHourly) Add(await _analytics.GetSummaryAsync(window.HourlyFrom, window.To, AIUsagePeriod.Hourly, new AIUsageFilter(), ct));
        return requests == 0 ? 0 : (int)Math.Round(weighted / requests);

        void Add(AIUsageSummary s)
        {
            requests += s.TotalRequests;
            weighted += (double)s.AverageDurationMs * s.TotalRequests;
        }
    }

    /// <summary>
    /// Totals per model, each priced with <see cref="AICostCalculator"/>. Pricing is linear in tokens, so
    /// pricing a model's totals equals pricing each of its data points.
    /// </summary>
    public async Task<PricedUsage> PriceByModelAsync(
        UsageWindow window, IReadOnlyList<string> modelIds, Func<string, AIUsageFilter> filterForModel,
        IReadOnlyDictionary<string, AIModelPrice> prices, CancellationToken ct)
    {
        var totals = UsageTotals.Empty;
        decimal cost = 0;
        long priced = 0, unpriced = 0;
        foreach (var modelId in modelIds)
        {
            var model = await GetTotalsAsync(window, filterForModel(modelId), ct);
            totals = totals.Plus(model);
            if (prices.TryGetValue(modelId, out var price))
            {
                cost += AICostCalculator.Cost(price, model.InputTokens, model.OutputTokens, model.CachedInputTokens);
                priced += model.TotalTokens;
            }
            else
            {
                unpriced += model.TotalTokens;
            }
        }
        return new PricedUsage(totals, cost, priced, unpriced);
    }

    // Daily + Hourly breakdown rows merged by dimension; the first non-empty display name wins.
    private static async Task<IReadOnlyList<AIUsageBreakdownItem>> GetItemsAsync(
        UsageWindow window, Func<DateTime, DateTime, AIUsagePeriod, Task<IEnumerable<AIUsageBreakdownItem>>> query)
    {
        var items = new List<AIUsageBreakdownItem>();
        if (window.HasDaily) items.AddRange(await query(window.From, window.HourlyFrom, AIUsagePeriod.Daily));
        if (window.HasHourly) items.AddRange(await query(window.HourlyFrom, window.To, AIUsagePeriod.Hourly));

        return items
            .GroupBy(i => i.Dimension, StringComparer.Ordinal)
            .Select(g => g.FirstOrDefault(i => !string.IsNullOrEmpty(i.DimensionName)) ?? g.First())
            .ToList();
    }

    private static UsageTotals ToTotals(AIUsageSummary s) =>
        new(s.TotalRequests, s.InputTokens, s.OutputTokens, s.CachedInputTokens ?? 0, s.TotalTokens);
}
