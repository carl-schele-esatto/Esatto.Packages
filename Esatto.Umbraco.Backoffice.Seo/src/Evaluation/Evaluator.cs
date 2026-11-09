using Esatto.Umbraco.Backoffice.Seo.Checks;

namespace Esatto.Umbraco.Backoffice.Seo.Evaluation;

/// <summary>
/// One check's verdict on one page (or, with <see cref="ContentKey"/> null, on the whole site).
/// Findings are the outcomes that didn't pass.
/// </summary>
public sealed record Outcome(string CheckId, Guid? ContentKey, string Culture, string? Url, bool Passed, string? Value)
{
    public static Outcome For(string checkId, PageContext p, bool passed, string? value)
        => new(checkId, p.Page.ContentKey, p.Page.Culture, p.Page.Url, passed, passed ? null : value);

    public static Outcome ForSite(string checkId, bool passed, string? value)
        => new(checkId, null, string.Empty, null, passed, passed ? null : value);
}

/// <summary>
/// "Don't report this". <see cref="ContentKey"/> null = the whole check on the site;
/// <see cref="Culture"/> null = every culture of that page.
/// </summary>
public sealed record IgnoreRule(int Id, string CheckId, Guid? ContentKey, string? Culture)
{
    public bool Matches(Outcome o)
        => o.CheckId == CheckId
            && (ContentKey is null || (o.ContentKey == ContentKey && (Culture is null || string.Equals(Culture, o.Culture, StringComparison.OrdinalIgnoreCase))));
}

/// <summary>Ignored outcomes are kept apart: they count neither as pass nor fail.</summary>
public sealed record Evaluation(IReadOnlyList<Outcome> Outcomes, IReadOnlyList<Outcome> Ignored)
{
    public IEnumerable<Outcome> Failures => Outcomes.Where(o => !o.Passed);
}

public static class Evaluator
{
    public static Evaluation Evaluate(ScanSnapshot snapshot, IEnumerable<ICheck> checks, IReadOnlyList<IgnoreRule> ignores)
    {
        var kept = new List<Outcome>();
        var ignored = new List<Outcome>();
        foreach (var outcome in checks.SelectMany(c => c.Evaluate(snapshot)))
            (ignores.Any(i => i.Matches(outcome)) ? ignored : kept).Add(outcome);
        return new Evaluation(kept, ignored);
    }
}
