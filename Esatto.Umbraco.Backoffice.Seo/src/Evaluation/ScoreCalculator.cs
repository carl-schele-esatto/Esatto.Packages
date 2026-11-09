using Esatto.Umbraco.Backoffice.Seo.Checks;

namespace Esatto.Umbraco.Backoffice.Seo.Evaluation;

/// <summary>
/// The 0–100 site score (spec: Score). Each indexable page scores 0 with any Critical failure, otherwise
/// its weighted pass rate; the average is multiplied by the weighted pass rate of the site-wide checks.
/// No indexable page = 0.
/// </summary>
public static class ScoreCalculator
{
    /// <summary>Stored with each scan; bump when the formula changes so trends don't mix incomparable numbers.</summary>
    public const int Version = 1;

    public static int Weight(Severity severity) => severity switch
    {
        Severity.Critical => 10,
        Severity.Warning => 3,
        _ => 1,
    };

    public static int Calculate(ScanSnapshot s, Evaluation e, IReadOnlyDictionary<string, CheckInfo> info)
    {
        // Indexable pages; while the whole site is hidden, every page counts as indexable.
        var scored = s.Pages.Where(p => !p.Hidden || s.SiteHidden).ToList();
        if (scored.Count == 0) return 0;

        var byPage = e.Outcomes
            .Where(o => o.ContentKey is not null && info.TryGetValue(o.CheckId, out var i) && i.Scope != CheckScope.Site)
            .ToLookup(o => (o.ContentKey!.Value, o.Culture.ToLowerInvariant()));

        var average = scored.Average(p => PageScore(byPage[(p.Page.ContentKey, p.Page.Culture.ToLowerInvariant())], info));

        var site = e.Outcomes.Where(o => info.TryGetValue(o.CheckId, out var i) && i.Scope == CheckScope.Site).ToList();
        var factor = site.Count == 0 ? 1.0 : Ratio(site, info);

        return (int)Math.Round(average * factor, MidpointRounding.AwayFromZero);
    }

    private static double PageScore(IEnumerable<Outcome> outcomes, IReadOnlyDictionary<string, CheckInfo> info)
    {
        var list = outcomes.ToList();
        if (list.Count == 0) return 100;
        if (list.Any(o => !o.Passed && info[o.CheckId].Severity == Severity.Critical)) return 0;
        return 100 * Ratio(list, info);
    }

    private static double Ratio(IReadOnlyCollection<Outcome> outcomes, IReadOnlyDictionary<string, CheckInfo> info)
    {
        double total = 0, passed = 0;
        foreach (var o in outcomes)
        {
            var w = Weight(info[o.CheckId].Severity);
            total += w;
            if (o.Passed) passed += w;
        }

        return total == 0 ? 1 : passed / total;
    }
}
