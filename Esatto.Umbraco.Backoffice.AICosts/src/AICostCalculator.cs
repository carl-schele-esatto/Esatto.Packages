namespace Esatto.Umbraco.Backoffice.AICosts;

public static class AICostCalculator
{
    private const decimal TokensPerUnit = 1_000_000m;

    /// <summary>
    /// Estimated cost in the price's currency. Cached input tokens are a subset of input tokens (the
    /// Microsoft.Extensions.AI convention): the cached part is billed at <see cref="AIModelPrice.CachedInput"/>
    /// and only the remainder at the full input price. Negative counts count as zero.
    /// </summary>
    public static decimal Cost(AIModelPrice price, long inputTokens, long outputTokens, long cachedInputTokens)
    {
        var input = Math.Max(inputTokens, 0);
        var cached = Math.Clamp(cachedInputTokens, 0, input);

        return ((input - cached) * price.Input
                + cached * (price.CachedInput ?? price.Input)
                + Math.Max(outputTokens, 0) * price.Output) / TokensPerUnit;
    }
}
