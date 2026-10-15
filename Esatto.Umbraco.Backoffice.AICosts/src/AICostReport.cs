namespace Esatto.Umbraco.Backoffice.AICosts;

public sealed record AICostDay(
    DateOnly Date,
    long Requests,
    long InputTokens,
    long OutputTokens,
    long CachedInputTokens,
    long TotalTokens,
    decimal Cost,
    long UnpricedTokens);

public sealed record AICostModel(
    string ModelId,
    long Requests,
    long InputTokens,
    long OutputTokens,
    long CachedInputTokens,
    long TotalTokens,
    decimal? Cost);

public sealed record AICostTotals(long Requests, long TotalTokens, decimal Cost, long UnpricedTokens);

/// <summary>The monthly budget as configured: amount and its currency (not converted).</summary>
public sealed record AICostBudget(decimal Amount, string Currency);

/// <summary>The calendar month to date (UTC), in the base currency, with the projected month-end cost.</summary>
public sealed record AICostMonth(DateOnly From, DateOnly To, decimal Cost, decimal ProjectedCost, long UnpricedTokens, AICostBudget? Budget);

/// <summary>Totals for the period of the same length just before the selected one.</summary>
public sealed record AICostPrevious(DateOnly From, DateOnly To, AICostTotals Totals);

/// <summary>Costs are in <see cref="BaseCurrency"/>; the tab converts with <see cref="Rates"/>.</summary>
public sealed record AICostReport(
    DateOnly From,
    DateOnly To,
    string BaseCurrency,
    string DefaultCurrency,
    IReadOnlyList<string> Currencies,
    ExchangeRates? Rates,
    string? PricesSource,
    IReadOnlyList<AICostDay> Days,
    IReadOnlyList<AICostModel> Models,
    AICostTotals Totals,
    AICostMonth Month,
    AICostPrevious? Previous);
