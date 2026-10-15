using Microsoft.Extensions.Options;
using Umbraco.AI.Core.Analytics;
using Umbraco.AI.Core.Analytics.Usage;

namespace Esatto.Umbraco.Backoffice.AICosts;

/// <summary>
/// Builds the AI → Analytics → Costs report from Umbraco.AI's usage statistics (daily ones are kept 365
/// days by default, <see cref="AIAnalyticsOptions.UsageDailyRetentionDays"/>). Cost is computed per model
/// per data point, because each model has its own price and the input/output split only exists on the
/// time series. Also returns the calendar month to date and the previous period of the same length.
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
        var from = firstDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = today.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // Daily before yesterday, Hourly for yesterday and today: see UsageWindow.
        var window = UsageWindow.Create(from, to, now);

        var dayRows = Enumerable.Range(0, days)
            .Select(i => firstDay.AddDays(i))
            .ToDictionary(d => d, d => new DayAccumulator());

        foreach (var point in await _queries.GetSeriesAsync(window, new AIUsageFilter(), cancellationToken))
        {
            if (!dayRows.TryGetValue(DateOnly.FromDateTime(point.Timestamp), out var row)) continue;
            row.Requests += point.RequestCount;
            row.InputTokens += point.InputTokens;
            row.OutputTokens += point.OutputTokens;
            row.CachedInputTokens += point.CachedInputTokens ?? 0;
            row.TotalTokens += point.TotalTokens;
        }

        var models = new List<AICostModel>();
        foreach (var modelId in await _queries.GetModelIdsAsync(window, cancellationToken))
        {
            options.PricesPerMillionTokens.TryGetValue(modelId, out var price);
            long requests = 0, input = 0, output = 0, cached = 0, total = 0;
            decimal cost = 0;

            foreach (var point in await _queries.GetSeriesAsync(window, new AIUsageFilter { ModelId = modelId }, cancellationToken))
            {
                requests += point.RequestCount;
                input += point.InputTokens;
                output += point.OutputTokens;
                cached += point.CachedInputTokens ?? 0;
                total += point.TotalTokens;
                if (!dayRows.TryGetValue(DateOnly.FromDateTime(point.Timestamp), out var row)) continue;

                if (price is null)
                {
                    row.UnpricedTokens += point.TotalTokens;
                    continue;
                }

                var pointCost = AICostCalculator.Cost(price, point.InputTokens, point.OutputTokens, point.CachedInputTokens ?? 0);
                row.Cost += pointCost;
                cost += pointCost;
            }

            models.Add(new AICostModel(modelId, requests, input, output, cached, total, price is null ? null : cost));
        }

        var dayList = dayRows
            .OrderBy(kv => kv.Key)
            .Select(kv => kv.Value.ToDay(kv.Key))
            .ToList();

        var totals = new AICostTotals(
            dayList.Sum(d => d.Requests),
            dayList.Sum(d => d.TotalTokens),
            dayList.Sum(d => d.Cost),
            dayList.Sum(d => d.UnpricedTokens));

        var currencies = options.Currencies
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();
        if (currencies.Count == 0) currencies = [.. AICostOptions.DefaultCurrencies];
        var defaultCurrency = currencies.Contains(options.DefaultCurrency, StringComparer.OrdinalIgnoreCase)
            ? options.DefaultCurrency
            : currencies[0];

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
            dayList,
            models.OrderByDescending(m => m.Cost ?? -1).ThenByDescending(m => m.TotalTokens).ToList(),
            totals,
            month,
            previous);
    }

    // Calendar month to date (UTC); projection = cost per elapsed day × days in the month. Today counts
    // as elapsed, so on the 1st the projection is the day's cost × days in the month.
    private async Task<AICostMonth> GetMonthAsync(DateOnly today, DateTime now, AICostOptions options, string defaultCurrency, CancellationToken ct)
    {
        var first = new DateOnly(today.Year, today.Month, 1);
        var window = UsageWindow.Create(first.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            today.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), now);
        var priced = await PriceAsync(window, options, ct);
        // Multiply before dividing: decimal division first leaves rounding residue (6 / 31 × 31 ≠ 6).
        var projected = priced.Cost * DateTime.DaysInMonth(today.Year, today.Month) / today.Day;
        var budget = options.MonthlyBudget is { } amount
            ? new AICostBudget(amount, string.IsNullOrWhiteSpace(options.BudgetCurrency) ? defaultCurrency : options.BudgetCurrency.Trim().ToUpperInvariant())
            : null;
        return new AICostMonth(first, today, priced.Cost, projected, priced.UnpricedTokens, budget);
    }

    // The same number of days right before the selected period — only when Umbraco.AI still has all of
    // it: daily statistics older than UsageDailyRetentionDays are deleted, and a comparison against
    // partly deleted data would mislead.
    private async Task<AICostPrevious?> GetPreviousAsync(DateOnly firstDay, int days, DateOnly today, DateTime now, AICostOptions options, CancellationToken ct)
    {
        var previousFrom = firstDay.AddDays(-days);
        if (previousFrom < today.AddDays(-_analyticsOptions.Value.UsageDailyRetentionDays)) return null;

        var window = UsageWindow.Create(previousFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            firstDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), now);
        var priced = await PriceAsync(window, options, ct);
        return new AICostPrevious(previousFrom, firstDay.AddDays(-1),
            new AICostTotals(priced.Totals.Requests, priced.Totals.TotalTokens, priced.Cost, priced.UnpricedTokens));
    }

    private async Task<PricedUsage> PriceAsync(UsageWindow window, AICostOptions options, CancellationToken ct) =>
        await _queries.PriceByModelAsync(window, await _queries.GetModelIdsAsync(window, ct),
            modelId => new AIUsageFilter { ModelId = modelId }, options.PricesPerMillionTokens, ct);

    private sealed class DayAccumulator
    {
        public long Requests;
        public long InputTokens;
        public long OutputTokens;
        public long CachedInputTokens;
        public long TotalTokens;
        public decimal Cost;
        public long UnpricedTokens;

        public AICostDay ToDay(DateOnly date) =>
            new(date, Requests, InputTokens, OutputTokens, CachedInputTokens, TotalTokens, Cost, UnpricedTokens);
    }
}
