using Esatto.Umbraco.Backoffice.AICosts;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.AI.Core.Analytics.Usage;
using Xunit;

namespace Esatto.Umbraco.Backoffice.AICosts.Tests;

public class AICostBreakdownServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 15, 30, 0, DateTimeKind.Utc);
    private readonly IAIUsageAnalyticsService _analytics = Usage.EmptyAnalytics();
    private readonly AICostOptions _options = new()
    {
        PricesPerMillionTokens = new(StringComparer.OrdinalIgnoreCase)
        {
            ["sonnet"] = new() { Input = 2m, Output = 10m },
            ["haiku"] = new() { Input = 1m, Output = 5m },
        },
    };

    private AICostBreakdownService Sut()
    {
        var monitor = Substitute.For<IOptionsMonitor<AICostOptions>>();
        monitor.CurrentValue.Returns(_options);
        return new AICostBreakdownService(new AICostQueries(_analytics), monitor, new TestClock(Now));
    }

    private void UsedModels(params string[] ids) =>
        _analytics.GetBreakdownByModelAsync(default, default, default, default).ReturnsForAnyArgs(ids.Select(id => Usage.Item(id)).ToArray());

    private void UserModel(string userId, string model, long input) =>
        _analytics.GetSummaryAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<AIUsagePeriod?>(),
                Arg.Is<AIUsageFilter?>(f => f!.UserId == userId && f.ModelId == model), Arg.Any<CancellationToken>())
            .Returns(Usage.Summary(1, input, 0));

    [Fact]
    public async Task A_row_is_priced_across_its_models()
    {
        UsedModels("sonnet", "haiku");
        _analytics.GetBreakdownByUserAsync(default, default, default, default).ReturnsForAnyArgs([Usage.Item("u1", "Jane Doe")]);
        UserModel("u1", "sonnet", 500_000);
        UserModel("u1", "haiku", 1_000_000);

        var row = Assert.Single((await Sut().GetBreakdownAsync(AICostDimension.User, 1, CancellationToken.None)).Rows);

        // days = 1 → today only → Hourly part only, so each summary counts once.
        Assert.Equal(("u1", "Jane Doe"), (row.Id, row.Name));
        Assert.Equal(2m, row.Cost); // 0.5M × 2 + 1M × 1
        Assert.Equal(2, row.Requests);
    }

    [Fact]
    public async Task A_partly_unpriced_row_keeps_its_cost_and_counts_unpriced_tokens()
    {
        UsedModels("sonnet", "mystery");
        _analytics.GetBreakdownByUserAsync(default, default, default, default).ReturnsForAnyArgs([Usage.Item("u1")]);
        UserModel("u1", "sonnet", 1_000_000);
        UserModel("u1", "mystery", 300);

        var row = Assert.Single((await Sut().GetBreakdownAsync(AICostDimension.User, 1, CancellationToken.None)).Rows);

        Assert.Equal(2m, row.Cost);
        Assert.Equal(300, row.UnpricedTokens);
        Assert.Equal("u1", row.Name); // no display name → the ID
    }

    [Fact]
    public async Task A_fully_unpriced_row_has_no_cost()
    {
        UsedModels("mystery");
        _analytics.GetBreakdownByUserAsync(default, default, default, default).ReturnsForAnyArgs([Usage.Item("u1")]);
        UserModel("u1", "mystery", 300);

        Assert.Null(Assert.Single((await Sut().GetBreakdownAsync(AICostDimension.User, 1, CancellationToken.None)).Rows).Cost);
    }

    [Fact]
    public async Task Profile_rows_filter_by_profile_id()
    {
        var profileId = Guid.NewGuid();
        UsedModels("sonnet");
        _analytics.GetBreakdownByProfileAsync(default, default, default, default).ReturnsForAnyArgs([Usage.Item(profileId.ToString(), "Content Assistant")]);
        _analytics.GetSummaryAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<AIUsagePeriod?>(),
                Arg.Is<AIUsageFilter?>(f => f!.ProfileId == profileId && f.ModelId == "sonnet"), Arg.Any<CancellationToken>())
            .Returns(Usage.Summary(3, 1_000_000, 0));

        var breakdown = await Sut().GetBreakdownAsync(AICostDimension.Profile, 1, CancellationToken.None);

        Assert.Equal("profile", breakdown.Dimension);
        Assert.Equal(2m, Assert.Single(breakdown.Rows).Cost);
    }

    // Review Focus 2.
    [Fact]
    public async Task Profile_row_with_a_non_guid_id_is_skipped()
    {
        UsedModels("sonnet");
        _analytics.GetBreakdownByProfileAsync(default, default, default, default).ReturnsForAnyArgs([Usage.Item("not-a-guid")]);

        Assert.Empty((await Sut().GetBreakdownAsync(AICostDimension.Profile, 1, CancellationToken.None)).Rows);
    }

    [Fact]
    public async Task Rows_are_sorted_by_cost_then_tokens()
    {
        UsedModels("sonnet");
        _analytics.GetBreakdownByProviderAsync(default, default, default, default).ReturnsForAnyArgs([Usage.Item("cheap"), Usage.Item("dear")]);
        _analytics.GetSummaryAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<AIUsagePeriod?>(),
                Arg.Is<AIUsageFilter?>(f => f!.ProviderId == "cheap"), Arg.Any<CancellationToken>()).Returns(Usage.Summary(1, 100, 0));
        _analytics.GetSummaryAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<AIUsagePeriod?>(),
                Arg.Is<AIUsageFilter?>(f => f!.ProviderId == "dear"), Arg.Any<CancellationToken>()).Returns(Usage.Summary(1, 1_000_000, 0));

        var rows = (await Sut().GetBreakdownAsync(AICostDimension.Provider, 1, CancellationToken.None)).Rows;

        Assert.Equal(["dear", "cheap"], rows.Select(r => r.Id));
    }
}
