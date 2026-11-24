namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

/// <summary>An opportunity to fix, with an estimate of how big the effort is.</summary>
public record Opportunity(
    // Opportunities.KindNear or Opportunities.KindCannibal.
    string Kind,

    // The text of the opportunity. WHERE it sits decides its key: area "opportunity", parts Title and Text, looked up with
    // ReportKeys.Lookup (opportunity<Key>Title, opportunity<Key>Text); both get the same arguments, as for an annotation.
    // The key is the Kind.
    TextRef Text,

    // Lower = cheaper to fix. Sets the order of the list.
    int Effort);

/// <summary>
/// Quick wins: the two findings that only became possible once search queries were fetched in depth and query-x-page came
/// along.
///
/// DELIBERATELY DOES NOT REDO THE THREE EXISTING FINDINGS in the report mapper ("seen but not clicked", "shown but ranking
/// deep", "non-brand: visibility without clicks"). Two implementations of one rule drift apart, and then the screen
/// contradicts itself about the same page. The analysis view shows the existing findings verbatim next to these.
///
/// THE POSITION THRESHOLDS ARE BORROWED from <see cref="ReportMapper"/>, for the same reason.
///
/// The texts are text references (a key and raw figures); the English wording is in en.ts. Arguments of "near": query,
/// position (one decimal), impressions. Arguments of "cannibal": query, firstPath, firstPosition, firstImpressions,
/// secondPath, secondPosition, secondImpressions (the positions with one decimal).
/// </summary>
public static class Opportunities
{
    /// <summary>The query is on page two. One step, not a content project.</summary>
    public const string KindNear = "near";

    /// <summary>Two of the site's own pages compete for the same query.</summary>
    public const string KindCannibal = "cannibal";

    /// <summary>Every kind, which is also every text id the class emits (see ReportKeys).</summary>
    internal static readonly string[] Kinds = [KindNear, KindCannibal];

    private const int EffortNear = 2;
    private const int EffortCannibal = 3;

    /// <summary>
    /// The noise floor for a single SEARCH QUERY.
    ///
    /// Lower than the report mapper's FindingMinImpressions (300), which applies to a PAGE. A page aggregates hundreds of
    /// queries; a single query with 300 impressions is one of the largest a site has. With 300 as the requirement the list
    /// would always be empty.
    /// </summary>
    private const long MinQueryImpressions = 50;

    /// <summary>
    /// How large the second page must be relative to the first for it to count as cannibalization.
    ///
    /// A HEURISTIC, not a rule. Almost every query has a tail of incidental pages: a blog post that ranked one day at position
    /// 40. Without a ratio requirement every query becomes a "finding" and the list is worthless. A quarter is enough for it
    /// to be about two pages that really compete.
    /// </summary>
    private const double CannibalShare = 0.25;

    /// <summary>How many rows per kind. A longer list does not get read.</summary>
    private const int MaxPerKind = 5;

    public static IReadOnlyList<Opportunity> Find(Report? report)
    {
        var search = report?.Traffic?.Search;

        var found = new List<Opportunity>();
        found.AddRange(Near(search?.Queries));
        found.AddRange(Cannibal(search?.QueryPages));

        // Cheapest first. A stable sort, so the order within a kind is kept.
        return [.. found.OrderBy(x => x.Effort)];
    }

    /// <summary>
    /// Queries at position 11-20: the demand is there, the page is there, and it sits on page two where almost nobody clicks.
    /// The cheapest kind of content work there is.
    /// </summary>
    private static IEnumerable<Opportunity> Near(IReadOnlyList<SearchQuery>? queries) =>
        (queries ?? [])
            .Where(x => x.Impressions >= MinQueryImpressions
                        && x.Position > ReportMapper.FindingGoodPosition
                        && x.Position <= ReportMapper.FindingDeepPosition)
            .OrderByDescending(x => x.Impressions)
            .Take(MaxPerKind)
            .Select(x => new Opportunity(
                Kind: KindNear,
                Text: TextRef.Of(KindNear,
                    ("query", x.Query), ("position", ReportMapper.Shown1(x.Position)), ("impressions", x.Impressions)),
                Effort: EffortNear));

    /// <summary>
    /// Queries where two of the site's own pages compete. Both get part of the signals, neither gets all of them, and Google
    /// chooses which one is shown.
    /// </summary>
    private static IEnumerable<Opportunity> Cannibal(IReadOnlyList<SearchQueryPage>? pairs) =>
        (pairs ?? [])
            .Where(x => !string.IsNullOrEmpty(x.Query))
            .GroupBy(x => x.Query, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Query = g.Key,
                Pages = g.GroupBy(x => x.Path ?? x.Url)
                    .Select(p => new
                    {
                        Path = p.Key,
                        Impressions = p.Sum(x => x.Impressions),
                        Position = p.Min(x => x.Position),
                    })
                    .OrderByDescending(x => x.Impressions)
                    .ToList(),
            })
            .Where(x => x.Pages.Count >= 2
                        && x.Pages[0].Impressions > 0
                        && x.Pages[1].Impressions >= x.Pages[0].Impressions * CannibalShare)
            .OrderByDescending(x => x.Pages.Sum(p => p.Impressions))
            .Take(MaxPerKind)
            .Select(x => new Opportunity(
                Kind: KindCannibal,
                Text: TextRef.Of(KindCannibal,
                    ("query", x.Query),
                    ("firstPath", x.Pages[0].Path),
                    ("firstPosition", ReportMapper.Shown1(x.Pages[0].Position)),
                    ("firstImpressions", x.Pages[0].Impressions),
                    ("secondPath", x.Pages[1].Path),
                    ("secondPosition", ReportMapper.Shown1(x.Pages[1].Position)),
                    ("secondImpressions", x.Pages[1].Impressions)),
                Effort: EffortCannibal));
}
