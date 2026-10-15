using Esatto.Umbraco.Backoffice.AICosts;
using Xunit;

namespace Esatto.Umbraco.Backoffice.AICosts.Tests;

// Ported from Esatto.Web (AiCostCalculatorTests).
public class AICostCalculatorTests
{
    private static readonly AIModelPrice Sonnet = new() { Input = 2m, Output = 10m, CachedInput = 0.2m };

    [Fact]
    public void Prices_input_and_output_per_million_tokens() =>
        Assert.Equal(3m, AICostCalculator.Cost(Sonnet, 1_000_000, 100_000, 0)); // 2 + 1

    [Fact]
    public void Cached_tokens_are_a_subset_of_input_billed_at_the_cache_price() =>
        Assert.Equal(1.28m, AICostCalculator.Cost(Sonnet, 1_000_000, 0, 400_000)); // 600k × 2 + 400k × 0.2

    [Fact]
    public void Missing_cache_price_falls_back_to_the_input_price() =>
        Assert.Equal(0.1m, AICostCalculator.Cost(new AIModelPrice { Input = 0.1m, Output = 0.5m }, 1_000_000, 0, 500_000));

    [Fact]
    public void Cached_count_larger_than_input_is_clamped() =>
        Assert.Equal(0.02m, AICostCalculator.Cost(Sonnet, 100_000, 0, 500_000)); // all 100k at cache price

    [Fact]
    public void Negative_counts_count_as_zero() =>
        Assert.Equal(0m, AICostCalculator.Cost(Sonnet, -5, -5, -5));
}
