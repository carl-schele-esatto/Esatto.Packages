using Microsoft.Extensions.Options;
using Umbraco.AI.Core.Analytics;
using Umbraco.AI.Core.Analytics.Usage;

namespace Esatto.Umbraco.Backoffice.AICosts;

/// <summary>
/// Builds the AI → Analytics → Costs report from Umbraco.AI's usage statistics (daily ones are kept 365
/// days by default, <see cref="AIAnalyticsOptions.UsageDailyRetentionDays"/>). Cost is computed per model
/// per data point, because each model has its own price and the input/output split only exists on the
/// time series. Also returns the calendar month to date, the previous period of the same length, and
/// the data behind the charts (cost per model per day, caching savings, reliability).
/// </summary>
public sealed class AICostReportService
{
    public const int MaxDays = 365;

    private readonly AICostQueries _queries;
    private readonly IExchangeRateProvider _exchangeRates;
    private readonly IOptionsMonitor<AICostOptions> _options;
    private readonly IOptions<AIAnalyticsOptions> _analyticsOptions;
    private readonly TimeProvider _timeProvider;

    public AICostReportService(
        AICostQueries queries,
        IExchangeRateProvider exchangeRates,
        IOptionsMonitor<AICostOptions> options,
        IOptions<AIAnalyticsOptions> analyticsOptions,
        TimeProvider timeProvider)
    {
        _queries = queries;
        _exchangeRates = exchangeRates;
        _options = options;
        _analyticsOptions = analyticsOptions;
        _timeProvider = timeProvider;
    }

    public async Task<AICostReport> GetReportAsync(int days, CancellationToken cancellationToken)
    {
        days = Math.Clamp(days, 1, MaxDays);
        var options = _options.CurrentValue;

        // Whole UTC days, today included: [from 00:00, tomorrow 00:00).
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var firstDay = today.AddDays(-(days - 1));

        // Daily before yesterday, Hourly for yesterday and today: see UsageWindow.
        var window = UsageWindow.Create(Utc(firstDay), Utc(today.AddDays(1)), now);
        var period = await PriceDaysAsync(window, firstDay, days, options, cancellationToken);

        var totals = new AICostTotals(
            period.Days.Sum(d => d.Requests),
            period.Days.Sum(d => d.TotalTokens),
            period.Days.Sum(d => d.Cost),
            period.Days.Sum(d => d.UnpricedTokens));

        var currencies = options.Currencies
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();
        if (currencies.Count == 0) currencies = [.. AICostOptions.DefaultCurrencies];
        // As written in the list ("sek" → "SEK"): the client matches currency codes exactly.
        var defaultCurrency = currencies.FirstOrDefault(c => c.Equals(options.DefaultCurrency?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? currencies[0];

        var month = await GetMonthAsync(today, now, options, defaultCurrency, cancellationToken);
        var previous = await GetPreviousAsync(firstDay, days, today, now, options, cancellationToken);

        return new AICostReport(
            firstDay,
            today,
            options.BaseCurrency,
            defaultCurrency,
            currencies,
            await _exchangeRates.GetRatesAsync(options.BaseCurrency, cancellationToken),
            options.PricesSource,
            period.Days,
            period.Models.OrderByDescending(m => m.Cost ?? -1).ThenByDescending(m => m.TotalTokens).ToList(),
            totals,
            month,
            previous,
            period.ModelDays,
            period.CachingSavings,
            await _queries.GetAverageDurationMsAsync(window, cancellationToken));
    }

    /// <summary>
    /// One row per day from <paramref name="firstDay"/>, priced per model per data point; also each
    /// model's totals, each priced model's cost per day, and what prompt caching saved.
    /// </summary>
    private async Task<PricedDays> PriceDaysAsync(UsageWindow window, DateOnly firstDay, int dayCount, AICostOptions options, CancellationToken ct)
    {
        var rows = Enumerable.Range(0, dayCount).Select(_ => new DayAccumulator()).ToArray();
        int? Index(DateTime timestamp)
        {
            var i = DateOnly.FromDateTime(timestamp).DayNumber - firstDay.DayNumber;
            return i >= 0 && i < dayCount ? i : null;
        }

        foreach (var point in await _queries.GetSeriesAsync(window, new AIUsageFilter(), ct))
        {
            if (Index(point.Timestamp) is not { } i) continue;
            var row = rows[i];
            row.Requests += point.RequestCount;
            row.InputTokens += point.InputTokens;
            row.OutputTokens += point.OutputTokens;
            row.CachedInputTokens += point.CachedInputTokens ?? 0;
            row.TotalTokens += point.TotalTokens;
            row.SucceededRequests += point.SuccessCount;
            row.FailedRequests += point.FailureCount;
        }

        var models = new List<AICostModel>();
        var modelDays = new List<AICostModelDays>();
        decimal savings = 0;
        foreach (var modelId in await _queries.GetModelIdsAsync(window, ct))
        {
            options.PricesPerMillionTokens.TryGetValue(modelId, out var price);
            long requests = 0, input = 0, output = 0, cached = 0, total = 0;
            decimal cost = 0;
            var costs = new decimal[dayCount];

            foreach (var point in await _queries.GetSeriesAsync(window, new AIUsageFilter { ModelId = modelId }, ct))
            {
                requests += point.RequestCount;
                input += point.InputTokens;
                output += point.OutputTokens;
                cached += point.CachedInputTokens ?? 0;
                total += point.TotalTokens;
                if (Index(point.Timestamp) is not { } i) continue;

                if (price is null)
                {
                    rows[i].UnpricedTokens += point.TotalTokens;
                    continue;
                }

                var pointCost = AICostCalculator.Cost(price, point.InputTokens, point.OutputTokens, point.CachedInputTokens ?? 0);
                rows[i].Cost += pointCost;
                costs[i] += pointCost;
                cost += pointCost;
            }

            models.Add(new AICostModel(modelId, requests, input, output, cached, total, price is null ? null : cost));
            if (price is null) continue;

            modelDays.Add(new AICostModelDays(modelId, costs));
            // What the cached tokens would have cost at the full input price, minus what they cost.
            savings += AICostCalculator.Cost(price, cached, 0, 0) - AICostCalculator.Cost(price, cached, 0, cached);
        }

        return new PricedDays(
            rows.Select((r, i) => r.ToDay(firstDay.AddDays(i))).ToList(),
            models,
            modelDays,
            savings);
    }

    // Calendar month to date (UTC), priced per day like the report; projection = cost per elapsed day ×
    // days in the month. Today counts as elapsed, so on the 1st it's the day's cost × days in the month.
    private async Task<AICostMonth> GetMonthAsync(DateOnly today, DateTime now, AICostOptions options, string defaultCurrency, CancellationToken ct)
    {
        var first = new DateOnly(today.Year, today.Month, 1);
        var window = UsageWindow.Create(Utc(first), Utc(today.AddDays(1)), now);
        var month = await PriceDaysAsync(window, first, today.Day, options, ct);
        var cost = month.Days.Sum(d => d.Cost);
        // Multiply before dividing: decimal division first leaves rounding residue (6 / 31 × 31 ≠ 6).
        var projected = cost * DateTime.DaysInMonth(today.Year, today.Month) / today.Day;
        var budget = options.MonthlyBudget is { } amount
            ? new AICostBudget(amount, string.IsNullOrWhiteSpace(options.BudgetCurrency) ? defaultCurrency : options.BudgetCurrency.Trim().ToUpperInvariant())
            : null;
        return new AICostMonth(first, today, cost, projected, month.Days.Sum(d => d.UnpricedTokens), budget,
            month.Days.Select(d => new AICostMonthDay(d.Date, d.Cost)).ToList());
    }

    // The same number of days right before the selected period — only when Umbraco.AI still has all of
    // it: daily statistics older than UsageDailyRetentionDays are deleted, and a comparison against
    // partly deleted data would mislead.
    private async Task<AICostPrevious?> GetPreviousAsync(DateOnly firstDay, int days, DateOnly today, DateTime now, AICostOptions options, CancellationToken ct)
    {
        var previousFrom = firstDay.AddDays(-days);
        if (previousFrom < today.AddDays(-_analyticsOptions.Value.UsageDailyRetentionDays)) return null;

        var window = UsageWindow.Create(Utc(previousFrom), Utc(firstDay), now);
        var priced = await _queries.PriceByModelAsync(window, await _queries.GetModelIdsAsync(window, ct),
            modelId => new AIUsageFilter { ModelId = modelId }, options.PricesPerMillionTokens, ct);
        return new AICostPrevious(previousFrom, firstDay.AddDays(-1),
            new AICostTotals(priced.Totals.Requests, priced.Totals.TotalTokens, priced.Cost, priced.UnpricedTokens));
    }

    private static DateTime Utc(DateOnly day) => day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    private sealed record PricedDays(
        IReadOnlyList<AICostDay> Days,
        IReadOnlyList<AICostModel> Models,
        IReadOnlyList<AICostModelDays> ModelDays,
        decimal CachingSavings);

    private sealed class DayAccumulator
    {
        public long Requests;
        public long InputTokens;
        public long OutputTokens;
        public long CachedInputTokens;
        public long TotalTokens;
        public decimal Cost;
        public long UnpricedTokens;
        public long SucceededRequests;
        public long FailedRequests;

        public AICostDay ToDay(DateOnly date) =>
            new(date, Requests, InputTokens, OutputTokens, CachedInputTokens, TotalTokens, Cost, UnpricedTokens, SucceededRequests, FailedRequests);
    }
}
