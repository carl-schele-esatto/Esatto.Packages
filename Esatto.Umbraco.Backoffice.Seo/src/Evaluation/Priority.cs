using Esatto.Umbraco.Backoffice.Seo.Checks;

namespace Esatto.Umbraco.Backoffice.Seo.Evaluation;

/// <summary>One check's failures summed up. <see cref="Pages"/> = distinct pages (0 for site-wide).</summary>
public sealed record IssueSummary(string CheckId, Severity Severity, Owner Owner, int Pages, int Occurrences, int Priority);

/// <summary>
/// Orders issues for "Fix these first": severity weight (Critical 100, Warning 10, Notice 1) × affected
/// pages (at least 1). A future priority provider (e.g. Search Console impressions) would plug in here.
/// </summary>
public static class Priority
{
    public static int Weight(Severity severity) => severity switch
    {
        Severity.Critical => 100,
        Severity.Warning => 10,
        _ => 1,
    };

    public static IReadOnlyList<IssueSummary> Rank(Evaluation e, IReadOnlyDictionary<string, CheckInfo> info) => Rank(e.Failures, info);

    /// <summary>Ranks the given failures (e.g. a filtered subset).</summary>
    public static IReadOnlyList<IssueSummary> Rank(IEnumerable<Outcome> failures, IReadOnlyDictionary<string, CheckInfo> info)
        => failures
            .Where(o => !o.Passed && info.ContainsKey(o.CheckId))
            .GroupBy(o => o.CheckId)
            .Select(g =>
            {
                var i = info[g.Key];
                var pages = g.Where(o => o.ContentKey is not null).Select(o => (o.ContentKey, o.Culture)).Distinct().Count();
                return new IssueSummary(g.Key, i.Severity, i.Owner, pages, g.Count(), Weight(i.Severity) * Math.Max(1, pages));
            })
            .OrderByDescending(s => s.Priority)
            .ThenBy(s => s.CheckId, StringComparer.Ordinal)
            .ToList();
}
