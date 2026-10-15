using Esatto.Umbraco.Backoffice.AICosts;
using NSubstitute;
using Umbraco.AI.Core.Analytics.Usage;
using Xunit;

namespace Esatto.Umbraco.Backoffice.AICosts.Tests;

public class AICostQueriesTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 15, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime Today = new(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);
    private readonly IAIUsageAnalyticsService _analytics = Usage.EmptyAnalytics();
    private readonly UsageWindow _window = UsageWindow.Create(Today.AddDays(-6), Today.AddDays(1), Now);

    [Fact]
    public async Task Totals_add_the_daily_and_hourly_summaries()
    {
        _analytics.GetSummaryAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), AIUsagePeriod.Daily, Arg.Any<AIUsageFilter?>(), Arg.Any<CancellationToken>())
            .Returns(Usage.Summary(2, 100, 10, 40));
        _analytics.GetSummaryAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), AIUsagePeriod.Hourly, Arg.Any<AIUsageFilter?>(), Arg.Any<CancellationToken>())
            .Returns(Usage.Summary(1, 50, 5));

        var totals = await new AICostQueries(_analytics).GetTotalsAsync(_window, new AIUsageFilter(), CancellationToken.None);

        Assert.Equal(new UsageTotals(3, 150, 15, 40, 165), totals);
    }

    [Fact]
    public async Task Breakdown_merges_daily_and_hourly_rows_keeping_a_name()
    {
        _analytics.GetBreakdownByUserAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), AIUsagePeriod.Daily, Arg.Any<CancellationToken>())
            .Returns([Usage.Item("u1", "Jane"), Usage.Item("u2")]);
        _analytics.GetBreakdownByUserAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), AIUsagePeriod.Hourly, Arg.Any<CancellationToken>())
            .Returns([Usage.Item("u1"), Usage.Item("u2", "John")]);

        var rows = await new AICostQueries(_analytics).GetBreakdownAsync(_window, AICostDimension.User, CancellationToken.None);

        Assert.Equal(["u1:Jane", "u2:John"], rows.Select(r => $"{r.Dimension}:{r.DimensionName}"));
    }

    [Fact]
    public async Task Prices_each_models_totals_and_counts_unpriced_tokens()
    {
        _analytics.GetSummaryAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), AIUsagePeriod.Daily, Arg.Is<AIUsageFilter?>(f => f!.ModelId == "priced"), Arg.Any<CancellationToken>())
            .Returns(Usage.Summary(1, 1_000_000, 100_000));
        _analytics.GetSummaryAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), AIUsagePeriod.Daily, Arg.Is<AIUsageFilter?>(f => f!.ModelId == "unpriced"), Arg.Any<CancellationToken>())
            .Returns(Usage.Summary(1, 500, 500));
        var prices = new Dictionary<string, AIModelPrice> { ["priced"] = new() { Input = 2m, Output = 10m } };

        var priced = await new AICostQueries(_analytics).PriceByModelAsync(
            _window, ["priced", "unpriced"], m => new AIUsageFilter { ModelId = m }, prices, CancellationToken.None);

        Assert.Equal(3m, priced.Cost);
        Assert.Equal(1_100_000, priced.PricedTokens);
        Assert.Equal(1_000, priced.UnpricedTokens);
        Assert.Equal(2, priced.Totals.Requests);
    }
}
