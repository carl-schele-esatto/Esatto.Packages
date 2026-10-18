using Esatto.Umbraco.Backoffice.AICosts;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.AI.Core.Analytics;
using Umbraco.AI.Core.Analytics.Usage;
using Xunit;

namespace Esatto.Umbraco.Backoffice.AICosts.Tests;

public class AICostReportServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 15, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime Yesterday = new(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);

    private readonly IAIUsageAnalyticsService _analytics = Usage.EmptyAnalytics();
    private readonly IExchangeRateProvider _rates = Substitute.For<IExchangeRateProvider>();
    private readonly AIAnalyticsOptions _analyticsOptions = new();
    private readonly AICostOptions _options = new()
    {
        PricesPerMillionTokens = new(StringComparer.OrdinalIgnoreCase)
        {
            ["claude-sonnet-5-5"] = new() { Input = 2m, Output = 10m, CachedInput = 0.2m },
        },
    };

    private AICostReportService Sut(DateTime? now = null)
    {
        var monitor = Substitute.For<IOptionsMonitor<AICostOptions>>();
        monitor.CurrentValue.Returns(_options);
        return new AICostReportService(new AICostQueries(_analytics), _rates, monitor,
            Options.Create(_analyticsOptions), new TestClock(now ?? Now));
    }

    private void Series(AIUsagePeriod period, string? modelId, params AIUsageTimeSeriesPoint[] points) =>
        _analytics.GetTimeSeriesAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), period,
                Arg.Is<AIUsageFilter?>(f => f!.ModelId == modelId), Arg.Any<CancellationToken>())
            .Returns(points);

    private void Models(AIUsagePeriod period, params string[] ids) =>
        _analytics.GetBreakdownByModelAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), period, Arg.Any<CancellationToken>())
            .Returns(ids.Select(id => Usage.Item(id)).ToArray());

    // Ported.
    [Fact]
    public async Task Combines_daily_history_with_hourly_recent_days_and_prices_each_model()
    {
        var oct10 = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
        Series(AIUsagePeriod.Daily, null, Usage.Point(oct10, 1, 500_000, 500));
        Series(AIUsagePeriod.Daily, "gpt-unpriced", Usage.Point(oct10, 1, 500_000, 500));
        Models(AIUsagePeriod.Daily, "gpt-unpriced");
        var todayAt10 = new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);
        Series(AIUsagePeriod.Hourly, null, Usage.Point(todayAt10, 2, 1_000_000, 100_000));
        Series(AIUsagePeriod.Hourly, "claude-sonnet-5-5", Usage.Point(todayAt10, 2, 1_000_000, 100_000));
        Models(AIUsagePeriod.Hourly, "claude-sonnet-5-5");

        var report = await Sut().GetReportAsync(3, CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 10, 10), report.From);
        Assert.Equal(new DateOnly(2026, 10, 12), report.To);
        Assert.Equal(3, report.Days.Count);
        var today = report.Days.Single(d => d.Date == new DateOnly(2026, 10, 12));
        Assert.Equal(2, today.Requests);
        Assert.Equal(3m, today.Cost);
        Assert.Equal(500_500, report.Days.Single(d => d.Date == new DateOnly(2026, 10, 10)).UnpricedTokens);
        var sonnet = report.Models.Single(m => m.ModelId == "claude-sonnet-5-5");
        Assert.Equal((3m, 2L), (sonnet.Cost, sonnet.Requests));
        Assert.Null(report.Models.Single(m => m.ModelId == "gpt-unpriced").Cost);
        Assert.Equal(3m, report.Totals.Cost);
        Assert.Equal(3, report.Totals.Requests);
    }

    // Ported.
    [Fact]
    public async Task Reads_yesterday_and_today_from_hourly_statistics()
    {
        await Sut().GetReportAsync(30, CancellationToken.None);

        await _analytics.Received().GetTimeSeriesAsync(Arg.Any<DateTime>(), Yesterday, AIUsagePeriod.Daily, Arg.Any<AIUsageFilter?>(), Arg.Any<CancellationToken>());
        await _analytics.Received().GetTimeSeriesAsync(Yesterday, Yesterday.AddDays(2), AIUsagePeriod.Hourly, Arg.Any<AIUsageFilter?>(), Arg.Any<CancellationToken>());
    }

    // Ported.
    [Fact]
    public async Task Currencies_are_distinct_and_default_when_unset()
    {
        _options.Currencies = ["SEK", "sek", "EUR", "SEK "];
        Assert.Equal(["SEK", "EUR"], (await Sut().GetReportAsync(7, CancellationToken.None)).Currencies);

        _options.Currencies = [];
        Assert.Equal(AICostOptions.DefaultCurrencies, (await Sut().GetReportAsync(7, CancellationToken.None)).Currencies);
    }

    // Deferred minor (fixed): "sek" must come back as the list's "SEK", or the client (which matches
    // currency codes exactly) opens the tab in the base currency.
    [Fact]
    public async Task Default_currency_is_returned_as_written_in_the_currency_list()
    {
        _options.Currencies = ["SEK", "EUR"];
        _options.DefaultCurrency = "sek";

        Assert.Equal("SEK", (await Sut().GetReportAsync(7, CancellationToken.None)).DefaultCurrency);
    }

    // Ported.
    [Fact]
    public async Task Clamps_days_to_the_retention_window() =>
        Assert.Equal(AICostReportService.MaxDays, (await Sut().GetReportAsync(10_000, CancellationToken.None)).Days.Count);

    // The month is priced from data points (like the report), so its usage is a time series point today.
    private void MonthUsage(decimal costInMillionInputTokensAtTwo, DateTime? at = null)
    {
        Models(AIUsagePeriod.Hourly, "claude-sonnet-5-5");
        Series(AIUsagePeriod.Hourly, "claude-sonnet-5-5",
            Usage.Point(at ?? Now, 1, (long)(costInMillionInputTokensAtTwo / 2m * 1_000_000), 0));
    }

    [Fact]
    public async Task Month_cost_and_projection()
    {
        MonthUsage(6m); // cost so far 6 USD on Oct 12 (12 days elapsed)
        _options.MonthlyBudget = 1000m;
        _options.BudgetCurrency = "SEK";

        var month = (await Sut().GetReportAsync(30, CancellationToken.None)).Month;

        Assert.Equal(new DateOnly(2026, 10, 1), month.From);
        Assert.Equal(new DateOnly(2026, 10, 12), month.To);
        Assert.Equal(6m, month.Cost);
        Assert.Equal(15.5m, month.ProjectedCost); // 6 / 12 × 31
        Assert.Equal(new AICostBudget(1000m, "SEK"), month.Budget);
    }

    // Review Focus 5.
    [Fact]
    public async Task Month_projection_on_the_first_day()
    {
        var firstOfMonth = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        MonthUsage(2m, firstOfMonth);

        var month = (await Sut(firstOfMonth).GetReportAsync(30, CancellationToken.None)).Month;

        Assert.Equal(62m, month.ProjectedCost); // 2 × 31
        Assert.Null(month.Budget);
    }

    [Fact]
    public async Task Month_projection_on_the_last_day_is_the_cost_so_far()
    {
        var lastOfMonth = new DateTime(2026, 10, 31, 20, 0, 0, DateTimeKind.Utc);
        MonthUsage(6m, lastOfMonth);

        Assert.Equal(6m, (await Sut(lastOfMonth).GetReportAsync(30, CancellationToken.None)).Month.ProjectedCost);
    }

    [Fact]
    public async Task Budget_currency_defaults_to_the_default_currency()
    {
        _options.MonthlyBudget = 50m;
        Assert.Equal(new AICostBudget(50m, "SEK"), (await Sut().GetReportAsync(30, CancellationToken.None)).Month.Budget);
    }

    [Fact]
    public async Task Previous_period_is_the_same_length_just_before()
    {
        Models(AIUsagePeriod.Daily, "claude-sonnet-5-5");
        _analytics.GetSummaryAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), AIUsagePeriod.Daily,
                Arg.Is<AIUsageFilter?>(f => f!.ModelId == "claude-sonnet-5-5"), Arg.Any<CancellationToken>())
            .Returns(Usage.Summary(4, 1_000_000, 0));

        var previous = (await Sut().GetReportAsync(30, CancellationToken.None)).Previous!;

        Assert.Equal(new DateOnly(2026, 8, 14), previous.From);
        Assert.Equal(new DateOnly(2026, 9, 12), previous.To);
        Assert.Equal(new AICostTotals(4, 1_000_000, 2m, 0), previous.Totals);
    }

    // Charts (Task 12).
    [Fact]
    public async Task Days_count_succeeded_and_failed_requests()
    {
        Series(AIUsagePeriod.Hourly, null, Usage.Point(Now, 4, 100, 10, failed: 1));

        var today = (await Sut().GetReportAsync(1, CancellationToken.None)).Days.Single();

        Assert.Equal((3L, 1L), (today.SucceededRequests, today.FailedRequests));
    }

    [Fact]
    public async Task Model_days_are_aligned_with_days_and_priced_models_only()
    {
        Models(AIUsagePeriod.Hourly, "claude-sonnet-5-5", "gpt-unpriced");
        Series(AIUsagePeriod.Hourly, "claude-sonnet-5-5", Usage.Point(Now, 1, 1_000_000, 100_000));
        Series(AIUsagePeriod.Hourly, "gpt-unpriced", Usage.Point(Now, 1, 500, 500));

        var modelDays = Assert.Single((await Sut().GetReportAsync(3, CancellationToken.None)).ModelDays);

        Assert.Equal("claude-sonnet-5-5", modelDays.ModelId);
        Assert.Equal([0m, 0m, 3m], modelDays.Costs);
    }

    [Fact]
    public async Task Caching_savings_price_cached_tokens_at_the_difference()
    {
        Models(AIUsagePeriod.Hourly, "claude-sonnet-5-5");
        Series(AIUsagePeriod.Hourly, "claude-sonnet-5-5", Usage.Point(Now, 1, 2_000_000, 0, cached: 1_000_000));

        Assert.Equal(1.8m, (await Sut().GetReportAsync(7, CancellationToken.None)).CachingSavings); // 1M × (2 − 0.2)
    }

    [Fact]
    public async Task Month_days_run_from_the_first_to_today_and_sum_to_the_month_cost()
    {
        Models(AIUsagePeriod.Daily, "claude-sonnet-5-5");
        Models(AIUsagePeriod.Hourly, "claude-sonnet-5-5");
        Series(AIUsagePeriod.Daily, "claude-sonnet-5-5", Usage.Point(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), 1, 1_000_000, 0));
        Series(AIUsagePeriod.Hourly, "claude-sonnet-5-5", Usage.Point(Now, 1, 500_000, 0));

        var month = (await Sut().GetReportAsync(30, CancellationToken.None)).Month;

        Assert.Equal(12, month.Days.Count);
        Assert.Equal(new AICostMonthDay(new DateOnly(2026, 10, 1), 2m), month.Days[0]);
        Assert.Equal(3m, month.Days.Sum(d => d.Cost));
        Assert.Equal(month.Cost, month.Days.Sum(d => d.Cost));
    }

    [Fact]
    public async Task Report_has_the_average_response_time()
    {
        _analytics.GetSummaryAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), AIUsagePeriod.Hourly, Arg.Is<AIUsageFilter?>(f => f!.ModelId == null), Arg.Any<CancellationToken>())
            .Returns(Usage.Summary(10, 0, 0, durationMs: 1200));

        Assert.Equal(1200, (await Sut().GetReportAsync(1, CancellationToken.None)).AverageDurationMs);
    }

    [Fact]
    public async Task No_previous_period_beyond_daily_retention()
    {
        Assert.Null((await Sut().GetReportAsync(365, CancellationToken.None)).Previous);

        _analyticsOptions.UsageDailyRetentionDays = 30;
        Assert.Null((await Sut().GetReportAsync(30, CancellationToken.None)).Previous);
    }
}
