namespace Esatto.Umbraco.Backoffice.AICosts;

/// <summary>
/// Bound from <c>Esatto:AICosts</c>. Umbraco.AI records tokens, not money — cost is tokens × these list
/// prices, so it is an estimate that excludes discounts, batch pricing, taxes and provider surcharges.
/// </summary>
public sealed class AICostOptions
{
    public const string SectionName = "Esatto:AICosts";

    /// <summary>Default for <see cref="Currencies"/> when the config section doesn't set any.</summary>
    public static readonly IReadOnlyList<string> DefaultCurrencies = ["SEK", "EUR", "USD", "NOK", "DKK", "GBP"];

    /// <summary>Currency the prices below are quoted in (ISO 4217). Provider list prices are in USD.</summary>
    public string BaseCurrency { get; set; } = "USD";

    /// <summary>Currencies offered in the currency switcher (ISO 4217, ECB-quoted). Deliberately not
    /// pre-filled: the configuration binder appends to an existing list, so an initialiser here would
    /// duplicate every configured currency. See <see cref="DefaultCurrencies"/>.</summary>
    public List<string> Currencies { get; set; } = [];

    /// <summary>Currency the tab opens in.</summary>
    public string DefaultCurrency { get; set; } = "SEK";

    /// <summary>Where the prices came from and when they were checked — shown in the footnote.</summary>
    public string? PricesSource { get; set; }

    /// <summary>Optional budget per calendar month (UTC), for the "This month" tile.</summary>
    public decimal? MonthlyBudget { get; set; }

    /// <summary>Currency of <see cref="MonthlyBudget"/>; the default currency when not set.</summary>
    public string? BudgetCurrency { get; set; }

    /// <summary>Prices per 1 million tokens in <see cref="BaseCurrency"/>, keyed by model ID exactly as
    /// Umbraco.AI reports it (e.g. <c>claude-sonnet-5-5</c>). Models without an entry show tokens but no cost.</summary>
    public Dictionary<string, AIModelPrice> PricesPerMillionTokens { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>List price per 1 million tokens. <see cref="CachedInput"/> is the cache-read price;
/// when omitted, cached tokens are charged at the <see cref="Input"/> price (never under-estimates).</summary>
public sealed class AIModelPrice
{
    public decimal Input { get; set; }
    public decimal Output { get; set; }
    public decimal? CachedInput { get; set; }
}
