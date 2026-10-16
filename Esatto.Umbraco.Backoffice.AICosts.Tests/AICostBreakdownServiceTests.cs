using Esatto.Umbraco.Backoffice.AICosts;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.AI.Core.Analytics.Usage;
using Xunit;

namespace Esatto.Umbraco.Backoffice.AICosts.Tests;

public class AICostBreakdownServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 15, 30, 0, DateTimeKind.Utc);
    // Umbraco user keys are GUIDs (anything else in the user breakdown is Umbraco.AI's "Anonymous" row).
    private static readonly string U1 = "3f1c2b7e-5d4a-4e8b-9a6c-1d2e3f4a5b6c";
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
        _analytics.GetBreakdownByUserAsync(default, default, default, default).ReturnsForAnyArgs([Usage.Item(U1, "Jane Doe")]);
        UserModel(U1, "sonnet", 500_000);
        UserModel(U1, "haiku", 1_000_000);

        var row = Assert.Single((await Sut().GetBreakdownAsync(AICostDimension.User, 1, CancellationToken.None)).Rows);

        // days = 1 → today only → Hourly part only, so each summary counts once.
        Assert.Equal((U1, "Jane Doe"), (row.Id, row.Name));
        Assert.Equal(2m, row.Cost); // 0.5M × 2 + 1M × 1
        Assert.Equal(2, row.Requests);
    }

    [Fact]
    public async Task A_partly_unpriced_row_keeps_its_cost_and_counts_unpriced_tokens()
    {
        UsedModels("sonnet", "mystery");
        _analytics.GetBreakdownByUserAsync(default, default, default, default).ReturnsForAnyArgs([Usage.Item(U1)]);
        UserModel(U1, "sonnet", 1_000_000);
        UserModel(U1, "mystery", 300);

        var row = Assert.Single((await Sut().GetBreakdownAsync(AICostDimension.User, 1, CancellationToken.None)).Rows);

        Assert.Equal(2m, row.Cost);
        Assert.Equal(300, row.UnpricedTokens);
        Assert.Equal(U1, row.Name); // no display name → the ID
    }

    [Fact]
    public async Task A_fully_unpriced_row_has_no_cost()
    {
        UsedModels("mystery");
        _analytics.GetBreakdownByUserAsync(default, default, default, default).ReturnsForAnyArgs([Usage.Item(U1)]);
        UserModel(U1, "mystery", 300);

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

    // Final review, Important 1: Umbraco.AI reports usage without a backoffice user (frontend, API,
    // scheduled agents, or every request with IncludeUsageUserDimension=false) as the row "Anonymous",
    // which can't be filtered on. It must get the usage no user row claims, not a fabricated zero.
    [Fact]
    public async Task Anonymous_user_row_gets_the_usage_no_user_row_claims()
    {
        var userKey = Guid.NewGuid().ToString();
        UsedModels("sonnet");
        _analytics.GetBreakdownByUserAsync(default, default, default, default).ReturnsForAnyArgs([Usage.Item(userKey, "Jane Doe"), Usage.Item("Anonymous")]);
        UserModel(userKey, "sonnet", 1_000_000);
        _analytics.GetSummaryAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<AIUsagePeriod?>(),
                Arg.Is<AIUsageFilter?>(f => f!.UserId == null && f.ModelId == "sonnet"), Arg.Any<CancellationToken>())
            .Returns(Usage.Summary(5, 3_000_000, 0));

        var rows = (await Sut().GetBreakdownAsync(AICostDimension.User, 1, CancellationToken.None)).Rows;

        var anonymous = rows.Single(r => r.Id == "Anonymous");
        Assert.Equal(4m, anonymous.Cost); // (3M − 1M) × 2
        Assert.Equal(4, anonymous.Requests);
        Assert.Equal(2m, rows.Single(r => r.Id == userKey).Cost);
    }

    // Review Focus 2. (ProfileId is a required Guid in Umbraco.AI, so this is a guard, not an expected case.)
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
