namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

/// <summary>
/// A target query held up against one month's data.
///
/// <see cref="Position"/> is null when the term has no match at all: there is then no position to report, and 0 would read as
/// "position zero", that is, as the best possible.
/// </summary>
public record TargetRow(
    string Term,

    // One of TargetAnalysis.Statuses, a plain code and not a TextRef. Its label is looked up by the code: area "target", part
    // Label (ReportKeys.Lookup("target", status, "Label"), for example targetMissingLabel).
    string Status,
    long Impressions,
    long Clicks,
    double CtrPercent,
    double? Position,
    int Matches,
    string? Example,

    // What to do about it. WHERE it sits decides its key: area "target", part Text (ReportKeys.Lookup("target", Advice.Key,
    // "Text")), with the arguments in Advice.Args. TargetAnalysis documents each advice id and its arguments.
    TextRef Advice,

    // The page that actually ranks for the topic. Null when the term has no match, and when the month was archived before
    // query-x-page was fetched.
    TargetPage? Page = null);

/// <summary>
/// The page that ranks for a target query, aggregated over the queries the term matches.
///
/// THIS RECORD IS THE ANSWER to "what should I do". Without it the advice says "strengthen the page that already ranks"
/// without saying which, which leaves the work undone.
/// </summary>
public record TargetPage(
    string Path,
    double Position,
    double CtrPercent,
    long Impressions,
    long Clicks,

    // How many of the term's search queries this page ranks for.
    int Queries);

/// <summary>
/// The target-query analysis: the editor's list held up against the month's search queries, with a status and a piece of advice
/// per term.
///
/// A PURE PROJECTION, NOTHING ARCHIVED. A report stores measurements and never changes; the target queries are a grid laid over
/// it at READING time. The reason is concrete: add a term tomorrow and every archived month is analysed again at once, whereas
/// an archived analysis could never have contained it. The job only fetches months that are missing entirely.
///
/// A TERM IS A TOPIC, NOT AN EXACT PHRASE. Search Console delivers whole search phrases, so a bare word hardly ever occurs
/// while "word consultant", "word guide" and "word hosting" do. The term therefore matches every query that CONTAINS it, and the
/// row adds up the hits. The price is false hits ("word error 500"), which is exactly why <see cref="TargetRow.Matches"/> and
/// <see cref="TargetRow.Example"/> exist: they make the aggregation checkable by whoever reads the row.
///
/// THE THRESHOLDS ARE BORROWED from <see cref="ReportMapper"/>. Values of its own could have said that a search performs while
/// the "To fix" tab points at a page with the same figures.
///
/// The advice is a <see cref="TextRef"/>: a key and raw figures, never prose (the English wording is in en.ts). The ids and
/// arguments:
///   missingNoQueries   (none)                  the month has no search queries at all
///   missing            rowsFetched             no match among the queries that were fetched
///   deep, near, title, performing  (none)      the advice by status, when no page is known
///   pageDeep, pageNear, pagePerforming         path, position (one decimal), queries (how many of the term's queries)
///   pageTitle                                  the same and ctrPercent (up to two decimals)
/// The path is sent as it is: "/" is data, and the client says "the home page" for it.
/// </summary>
public static class TargetAnalysis
{
    /// <summary>None of the month's search queries contains the term.</summary>
    public const string StatusMissing = "missing";

    /// <summary>Page two or deeper. The demand is there, the ranking is not.</summary>
    public const string StatusDeep = "deep";

    /// <summary>Position 11-20. One step from page one.</summary>
    public const string StatusNear = "near";

    /// <summary>A good position but few clicks. A title and meta description question.</summary>
    public const string StatusTitle = "title";

    /// <summary>Nothing to do.</summary>
    public const string StatusPerforming = "performing";

    /// <summary>Every status, most serious first. The client maps each code to its label (see <see cref="TargetRow.Status"/>).</summary>
    internal static readonly string[] Statuses = [StatusMissing, StatusDeep, StatusNear, StatusTitle, StatusPerforming];

    // The advice ids (TargetRow.Advice.Key); AdviceTexts lists them for ReportKeys.
    internal const string AdviceMissingNoQueries = "missingNoQueries";
    internal const string AdviceMissing = "missing";
    internal const string AdviceDeep = "deep";
    internal const string AdviceNear = "near";
    internal const string AdviceTitle = "title";
    internal const string AdvicePerforming = "performing";
    internal const string AdvicePageDeep = "pageDeep";
    internal const string AdvicePageNear = "pageNear";
    internal const string AdvicePageTitle = "pageTitle";
    internal const string AdvicePagePerforming = "pagePerforming";

    internal static readonly string[] AdviceTexts =
    [
        AdviceMissingNoQueries, AdviceMissing, AdviceDeep, AdviceNear, AdviceTitle, AdvicePerforming,
        AdvicePageDeep, AdvicePageNear, AdvicePageTitle, AdvicePagePerforming,
    ];

    /// <param name="terms">The target queries, in the editor's order.</param>
    /// <param name="queries">The month's search queries from the report. Null for a month archived before search queries were fetched.</param>
    /// <param name="queryPages">The month's query-x-page pairs. Null for a month archived before they were fetched.</param>
    /// <param name="rowsFetched">How many search queries were fetched. It goes into the advice: "not visible" means "not among
    /// the N most shown", never "zero impressions".</param>
    public static IReadOnlyList<TargetRow> Build(
        IReadOnlyList<string> terms,
        IReadOnlyList<SearchQuery>? queries,
        IReadOnlyList<SearchQueryPage>? queryPages,
        int rowsFetched)
    {
        var all = queries ?? [];
        var pairs = queryPages ?? [];

        return [.. (terms ?? []).Select(term => RowFor(term, all, pairs, rowsFetched))];
    }

    /// <summary>
    /// The term's position in a single month, or null when it has no match.
    ///
    /// PUBLIC SO THE POSITION GRID USES THE SAME FORMULA. Had the grid worked out its own weighting, a row and the grid could
    /// show different positions for the same month, and the reader would not know which one holds.
    /// </summary>
    public static double? PositionOf(string term, IReadOnlyList<SearchQuery>? queries)
    {
        var matches = Matching(term, queries);

        return matches.Count == 0 ? null : WeightedPosition(matches.Select(x => (x.Position, x.Impressions)));
    }

    private static TargetRow RowFor(
        string term, IReadOnlyList<SearchQuery> queries, IReadOnlyList<SearchQueryPage> pairs, int rowsFetched)
    {
        var matches = Matching(term, queries);

        if (matches.Count == 0)
        {
            return new TargetRow(
                term, StatusMissing, 0, 0, 0, null, 0, null,
                GeneralAdvice(StatusMissing, rowsFetched));
        }

        var impressions = matches.Sum(x => x.Impressions);
        var clicks = matches.Sum(x => x.Clicks);

        // THE CTR IS WORKED OUT AGAIN FROM THE FIGURES, never as an average of the queries' CTRs: an average of ratios is not the
        // ratio. 10/100 and 2/900 are 1.2 % together, not 5.1 %.
        var ctr = CtrPercentOf(clicks, impressions);

        var position = WeightedPosition(matches.Select(x => (x.Position, x.Impressions)));

        // The status is judged on the ROUNDED figures, that is, exactly the ones shown. Otherwise a row showing "20.0" could be
        // marked as deeper than twenty.
        var status = StatusOf(position, ctr);

        var page = BestPage(term, pairs);

        return new TargetRow(
            term,
            status,
            impressions,
            clicks,
            ctr,
            position,
            matches.Count,
            matches.OrderByDescending(x => x.Impressions).First().Query,
            page is null ? GeneralAdvice(status, rowsFetched) : PageAdvice(status, page),
            page);
    }

    private static bool Contains(string query, string term) =>
        !string.IsNullOrEmpty(query) && query.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static List<SearchQuery> Matching(string term, IReadOnlyList<SearchQuery>? queries) =>
        [.. (queries ?? []).Where(x => Contains(x.Query, term))];

    /// <summary>Clicks as a percentage of impressions, two decimals. Zero without impressions: no division by zero.</summary>
    private static double CtrPercentOf(long clicks, long impressions) =>
        impressions == 0 ? 0 : Math.Round(clicks / (double)impressions * 100.0, 2);

    /// <summary>
    /// THE IMPRESSION-WEIGHTED MEAN, the same way Search Console itself aggregates position over a set of queries. A plain mean
    /// would let an obscure phrase at position 5 hide that nearly all the impressions are at 30. Without impressions there is
    /// nothing to weight with, and the plain mean is all there is; otherwise the figure would be NaN. Rounded to one decimal.
    /// </summary>
    private static double WeightedPosition(IEnumerable<(double Position, long Impressions)> items)
    {
        var list = items.ToList();
        var impressions = list.Sum(x => x.Impressions);

        return Math.Round(
            impressions == 0
                ? list.Average(x => x.Position)
                : list.Sum(x => x.Position * x.Impressions) / impressions,
            1);
    }

    /// <summary>
    /// The page that ranks for the term: the one with the MOST IMPRESSIONS among the pairs the term matches.
    ///
    /// IMPRESSIONS AND NOT CLICKS decide. Half the point of the analysis is searches where the clicks do not come; sorted on
    /// clicks, exactly those cases would have pointed at the wrong page, or at none.
    ///
    /// Null when there are no pairs: the month may have been archived before query-x-page was fetched, and the row should then
    /// fall back to the general advice instead of claiming something about a page.
    /// </summary>
    private static TargetPage? BestPage(string term, IReadOnlyList<SearchQueryPage> pairs)
    {
        var matching = pairs.Where(x => Contains(x.Query, term)).ToList();

        if (matching.Count == 0) return null;

        var best = matching
            .GroupBy(x => x.Path ?? x.Url)
            .Select(g => new
            {
                Path = g.Key,
                Impressions = g.Sum(x => x.Impressions),
                Clicks = g.Sum(x => x.Clicks),
                Queries = g.Select(x => x.Query).Distinct(StringComparer.OrdinalIgnoreCase).Count(),

                // The same weighting as for the term itself, so the page's position and the term's position are worked out the
                // same way and can be compared.
                Position = WeightedPosition(g.Select(x => (x.Position, x.Impressions))),
            })
            .OrderByDescending(x => x.Impressions)
            .ThenBy(x => x.Path, StringComparer.Ordinal)
            .First();

        return new TargetPage(
            best.Path,
            best.Position,
            CtrPercentOf(best.Clicks, best.Impressions),
            best.Impressions,
            best.Clicks,
            best.Queries);
    }

    private static string StatusOf(double position, double ctrPercent) => position switch
    {
        > ReportMapper.FindingDeepPosition => StatusDeep,
        > ReportMapper.FindingGoodPosition => StatusNear,

        // FindingLowCtr is a fraction (0.01) and ctrPercent is a percentage. The comparison is strict: exactly 1 % does not
        // count as low.
        _ => ctrPercent < ReportMapper.FindingLowCtr * 100.0 ? StatusTitle : StatusPerforming,
    };

    /// <summary>
    /// The advice when the page is known. The difference from <see cref="GeneralAdvice"/> is the whole point: a sentence that
    /// names the page, its position and how many of the term's queries it covers is an instruction. A sentence without a page
    /// is a general truth about SEO.
    /// </summary>
    private static TextRef PageAdvice(string status, TargetPage page)
    {
        (string Name, object? Value)[] figures = [("path", page.Path), ("position", page.Position), ("queries", page.Queries)];

        return status switch
        {
            StatusDeep => TextRef.Of(AdvicePageDeep, figures),
            StatusNear => TextRef.Of(AdvicePageNear, figures),
            StatusTitle => TextRef.Of(AdvicePageTitle, [.. figures, ("ctrPercent", page.CtrPercent)]),
            _ => TextRef.Of(AdvicePagePerforming, figures),
        };
    }

    private static TextRef GeneralAdvice(string status, int rowsFetched) => status switch
    {
        // TWO DIFFERENT CLAIMS, and they must not be mixed. With a basis: "we looked in N queries and did not see you". Without
        // one: "we have nothing to look in". A month archived before search queries were fetched knows nothing about any
        // term, and "not among the 0 most shown" would read as a result instead of an empty measuring tape.
        StatusMissing when rowsFetched <= 0 => TextRef.Of(AdviceMissingNoQueries),
        StatusMissing => TextRef.Of(AdviceMissing, ("rowsFetched", rowsFetched)),
        StatusDeep => TextRef.Of(AdviceDeep),
        StatusNear => TextRef.Of(AdviceNear),
        StatusTitle => TextRef.Of(AdviceTitle),
        _ => TextRef.Of(AdvicePerforming),
    };
}
