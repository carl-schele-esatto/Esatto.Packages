namespace Esatto.Umbraco.Backoffice.AICosts;

/// <summary>What the "Cost by" card can group by besides model (model is part of the report).</summary>
public enum AICostDimension { Profile, User, Provider }

/// <param name="Cost">Null only when none of the row's tokens come from a priced model.</param>
public sealed record AICostBreakdownRow(string Id, string Name, long Requests, long InputTokens,
    long CachedInputTokens, long OutputTokens, long TotalTokens, decimal? Cost, long UnpricedTokens);

public sealed record AICostBreakdown(string Dimension, IReadOnlyList<AICostBreakdownRow> Rows);
