namespace Esatto.Umbraco.Backoffice.Accessibility.Knowledge;

/// <summary>
/// One WCAG success criterion plus our own guidance for it. Ids, titles, levels and versions come from
/// W3C's published WCAG data; <see cref="Why"/>, <see cref="HowToFix"/>, <see cref="Caveats"/> and
/// <see cref="HowToTest"/> are written for this package.
/// </summary>
public sealed record Criterion(
    string Id,
    string Title,
    WcagLevel Level,
    WcagVersion Since,
    WcagVersion? ObsoleteIn,
    int Principle,
    string Guideline,
    bool FullyAutomatable,
    Owner Owner,
    string Why,
    string HowToFix,
    string Caveats,
    string HowToTest,
    string UnderstandingUrl);

/// <summary>Rule-specific guidance for an axe-core rule, overriding the criterion's generic text where set.</summary>
public sealed record AxeRuleGuide(string RuleId, Owner? Owner, string? HowToFix, string? Caveats);
