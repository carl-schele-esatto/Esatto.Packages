using Esatto.Umbraco.Backoffice.SeoReport.Google;

namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

// -------------------------------------------------------------------------------------------------------------------------
// SEARCH CONSOLE: brand / non-brand
//
// ALL arithmetic on Search Console data lives here, never in GscAgent. The agent delivers Search Console's own figures per
// (period, segment); what is worked out below is the year-on-year change, CTR as a percentage instead of a fraction, and the
// difference between the unfiltered total and brand + non-brand.
// -------------------------------------------------------------------------------------------------------------------------
public static partial class ReportMapper
{
    /// <summary>
    /// The share of the total that may drop out with anonymized queries before it is noted. Below this the difference is noise;
    /// above it, it starts to change how brand/non-brand should be read.
    /// </summary>
    private const double AnonymizedNoticeThreshold = 0.02;

    // THRESHOLDS OF THE FINDINGS. Judgements about the domain, not technical constants, and deliberately blunt: a note that fires
    // on noise gets ignored, and a note nobody reads is no note. The rules never guess WHY; they point at WHAT and WHERE, from
    // figures already in the report. The three below are public because the target-query analysis asks the same questions
    // about one query that these rules ask about a page, and the two are shown side by side: separate thresholds there could
    // call a query fine while a page with the same figures is a finding.

    /// <summary>The fewest impressions a page needs before its click-through rate says anything.</summary>
    private const long FindingMinImpressions = 300;

    /// <summary>A CTR below this counts as "the impressions are there, the clicks are not". 1 %.</summary>
    public const double FindingLowCtr = 0.01;

    /// <summary>A position better than this means the page is seen; then the CTR is a question of title and meta description.</summary>
    public const double FindingGoodPosition = 10.0;

    /// <summary>A position worse than this is page two or deeper. Then it is a question of ranking.</summary>
    public const double FindingDeepPosition = 20.0;

    /// <summary>How many pages a finding names. A list of 25 does not get read.</summary>
    private const int FindingMaxExamples = 3;

    /// <summary>The rows by (period, segment). Should the source ever answer twice for one key, the last answer wins instead of failing the run.</summary>
    private static Dictionary<(string Period, string Segment), GscRow> IndexRows(GscFetch gsc)
        => gsc.Rows.GroupBy(r => (r.Period, r.Segment)).ToDictionary(g => g.Key, g => g.Last());

    /// <summary>The latest month with an unfiltered row, up to the report period; without one, the latest complete month.</summary>
    private static string GscLatestPeriod(GscFetch gsc, string reportPeriod)
        => gsc.Rows
               .Where(r => r.Segment == GscAgent.SegmentTotal && string.CompareOrdinal(r.Period, reportPeriod) <= 0)
               .Select(r => r.Period)
               .OrderBy(p => p, StringComparer.Ordinal)
               .LastOrDefault()
           ?? Earlier(gsc.LatestCompletePeriod, reportPeriod);

    private static string Earlier(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? a : b;

    /// <summary>
    /// The note on the Search Console source. It says plainly when the report period is not complete; otherwise a cut-short month
    /// reads as a decline. With no unfiltered row up to the report period at all, Search Console has nothing for the month (a new
    /// property, or one that answered empty), and the note says that instead of a lag or an earlier month it has no figures for.
    /// </summary>
    private static TextRef GscNote(GscFetch gsc, string gscLatest, string reportPeriod)
    {
        (string, object?)[] through = string.IsNullOrWhiteSpace(gsc.DataThrough) ? [] : [("dataThrough", gsc.DataThrough)];

        if (!gsc.Rows.Any(r => r.Segment == GscAgent.SegmentTotal && string.CompareOrdinal(r.Period, reportPeriod) <= 0))
        {
            return Text(GscNoData, [("period", reportPeriod), .. through]);
        }

        return gscLatest == reportPeriod
            ? Text(GscLag, through)
            : Text(GscIncomplete, [("period", gscLatest), .. through]);
    }

    private static Dictionary<string, long> ClicksByPeriod(IReadOnlyDictionary<(string Period, string Segment), GscRow> gsc)
        => gsc.Where(kv => kv.Key.Segment == GscAgent.SegmentTotal)
            .ToDictionary(kv => kv.Key.Period, kv => kv.Value.Clicks, StringComparer.Ordinal);

    /// <summary>
    /// The Search Console KPIs of the traffic row. "gsc_clicks" comes from the UNFILTERED call, not from brand + non-brand (see
    /// GscAgent for why they differ), with the same year-on-year rule and baseline check as GA4.
    /// </summary>
    private static List<Kpi> GscKpis(
        IReadOnlyDictionary<(string Period, string Segment), GscRow> gsc,
        string gscLatest,
        IReadOnlyDictionary<string, long> sessions,
        string ga4Latest)
    {
        if (!gsc.TryGetValue((gscLatest, GscAgent.SegmentTotal), out var total)) return [];

        var kpis = new List<Kpi>
        {
            new(GscClicks, total.Clicks, Integer, SourceGsc, gscLatest, YoyTrend(ClicksByPeriod(gsc), gscLatest, UnitClicks).Trend),
        };

        // Clicks against visits ONLY when both sources cover the same month: a difference across two months is meaningless and
        // still looks authoritative. Clicks are normally MORE than visits (a visit can hold several clicks, and GA4 misses those
        // who decline consent); the sign of the value carries the direction.
        if (gscLatest == ga4Latest && sessions.TryGetValue(ga4Latest, out var ga4Sessions))
        {
            kpis.Add(new Kpi(ClicksVsSessions, total.Clicks - ga4Sessions, "signed", SourceGsc, gscLatest,
                new Trend(Note, null, Text(ClicksMinusVisits))));
        }

        return kpis;
    }

    /// <summary>
    /// GA4 visits against Search Console clicks, pivoted by year like the other traffic charts; older years lighter and dashed.
    /// ONLY the years in which Search Console has clicks: GA4 keeps years of history but Search Console 16 months, and a "visits
    /// against clicks" line for a year without clicks compares nothing.
    /// </summary>
    private static ChartBlock BuildSearchComparison(
        IReadOnlyDictionary<string, long> sessions,
        IReadOnlyDictionary<(string Period, string Segment), GscRow> gsc)
    {
        var clicks = ClicksByPeriod(gsc);
        var years = YearsOf(clicks.Keys);
        var latestYear = years.Count > 0 ? years[^1] : 0;

        var series = new List<Series>();
        foreach (var year in years)
        {
            var style = year < latestYear ? Dashed : Solid;
            series.Add(new Series(YearKey(SeriesSessions, year), MonthValues(sessions, year), false, style));
            series.Add(new Series(YearKey(SeriesClicks, year), MonthValues(clicks, year), year == latestYear, style));
        }

        return new ChartBlock(ChartSearchComparison, Line, Ramp, 240, [.. MonthNumbers], series, [], []);
    }

    /// <summary>
    /// The brand/non-brand block. A rolling month axis (13 points) instead of a year pivot: the split is a comparison WITHIN each
    /// month, and a January-December axis with four series would be unreadable. Months without data are null, NEVER 0: they
    /// become gaps in the line, while a zero reads as a crash.
    /// </summary>
    private static SearchBlock BuildSearchBlock(
        GscFetch gsc,
        IReadOnlyDictionary<(string Period, string Segment), GscRow> rows,
        string gscLatest,
        Period reportPeriod,
        bool brandSplit)
    {
        var periods = RollingPeriods(reportPeriod, 12);
        string[] segments = brandSplit ? SplitSegments : [];

        // Without a brand split there is nothing to plot: no labels and no series, which the client shows as "no data".
        ChartBlock MetricChart(string id, Func<GscRow, double> pick) => new(
            Id: id, Type: Line, Palette: "segment", Height: 240,
            Labels: segments.Length > 0 ? [.. periods] : [],
            Series: [.. segments.Select(segment => new Series(
                Key: segment,
                Values: [.. periods.Select(p => rows.TryGetValue((p, segment), out var row) ? pick(row) : (double?)null)],
                Emphasis: false,
                Style: Solid))],
            Annotations: [], Insights: []);

        return new SearchBlock(
            Id: SearchBlockId,
            Segments: [.. segments.Select(segment => new SearchSegment(segment, SegmentMetrics(rows, segment, gscLatest)))],
            Clicks: MetricChart(ChartSearchClicks, row => row.Clicks),
            Impressions: MetricChart(ChartSearchImpressions, row => row.Impressions),
            Annotations: SearchAnnotations(gsc, rows, gscLatest, periods, brandSplit),
            Insights: [],
            // CTR from fraction to percent HERE, as SegmentMetrics does; the agent leaves it in the source's unit. An empty
            // list becomes null, so "the source had none" reads differently from "the member is missing" everywhere alike.
            Queries: gsc.Queries is { Count: > 0 } queries
                ? [.. queries.Select(x => new SearchQuery(x.Query, x.Clicks, x.Impressions, Percent2(x.CtrFraction), Math.Round(x.Position, 1)))]
                : null,
            Pages: gsc.Pages is { Count: > 0 } pages
                ? [.. pages.Select(x => new SearchLandingPage(x.Url, Trim(x.Url), x.Clicks, x.Impressions, Percent2(x.CtrFraction), Math.Round(x.Position, 1)))]
                : null,
            Total: rows.ContainsKey((gscLatest, GscAgent.SegmentTotal))
                ? new SearchSegment(GscAgent.SegmentTotal, SegmentMetrics(rows, GscAgent.SegmentTotal, gscLatest))
                : null,
            QueryPages: QueryPagesOf(gsc));
    }

    /// <summary>
    /// The query-by-page pairs, cut down to the queries that are also archived. Two reasons: the analysis only reads queries that
    /// are in Queries, so a pair whose query is not there can never be used; and without the cut the archive would hold a
    /// thousand rows a month instead of a few hundred. Null when there are no pairs, so "no pairs" reads apart from "empty".
    /// </summary>
    private static List<SearchQueryPage>? QueryPagesOf(GscFetch gsc)
    {
        if (gsc.QueryPages is not { Count: > 0 } pairs) return null;

        var archived = new HashSet<string>((gsc.Queries ?? []).Select(x => x.Query), StringComparer.OrdinalIgnoreCase);

        var kept = pairs
            .Where(x => archived.Count == 0 || archived.Contains(x.Query))
            .Select(x => new SearchQueryPage(x.Query, x.Url, Trim(x.Url), x.Clicks, x.Impressions, Percent2(x.CtrFraction), Math.Round(x.Position, 1)))
            .ToList();

        return kept.Count > 0 ? kept : null;
    }

    /// <summary>
    /// The four measures of a segment in the latest complete month. CTR goes from Search Console's fraction (0.1228) to a
    /// percentage (12.28) HERE, where it can be seen. CTR and average position deliberately get no trend: their change is
    /// absolute (percentage points, places), and a Trend carries a percentage change only; "position -12 %" would mislead.
    /// </summary>
    private static List<Kpi> SegmentMetrics(
        IReadOnlyDictionary<(string Period, string Segment), GscRow> rows,
        string segment,
        string period)
    {
        rows.TryGetValue((period, segment), out var row);
        rows.TryGetValue((ShiftYearBack(period), segment), out var prior);

        Kpi Metric(string metric, double? value, string format, Trend? trend = null)
            => new(MetricKey(segment, metric), value, format, SourceGsc, period, trend);

        return
        [
            Metric(MetricClicks, row?.Clicks, Integer, ChangeTrend(row?.Clicks, prior?.Clicks)),
            Metric(MetricImpressions, row?.Impressions, Integer, ChangeTrend(row?.Impressions, prior?.Impressions)),
            Metric(MetricCtr, row is null ? null : Percent2(row.CtrFraction), "percent"),
            Metric(MetricPosition, row is null ? null : Math.Round(row.Position, 1), "decimal"),
        ];
    }

    /// <summary>A plain year-on-year trend, or null when either month is missing or the prior one is not above zero.</summary>
    private static Trend? ChangeTrend(long? current, long? prior)
    {
        if (current is not { } now || prior is not { } then || then <= 0) return null;

        var change = ChangePercent(now, then);
        return new Trend(change >= 0 ? Up : Down, change, Text(VsSameMonthLastYear));
    }

    /// <summary>
    /// Deterministic notes about the Search Console data. They make a limitation VISIBLE instead of letting it look like an
    /// outcome, and they come from here, never from an interpreting layer.
    /// </summary>
    private static List<Annotation> SearchAnnotations(
        GscFetch gsc,
        IReadOnlyDictionary<(string Period, string Segment), GscRow> rows,
        string gscLatest,
        IReadOnlyList<string> periods,
        bool brandSplit)
    {
        var annotations = new List<Annotation>();

        // 1. Anonymized queries. As soon as one filters on query, Search Console leaves out rare searches, so brand + non-brand
        //    falls short of the total. The difference cannot be assigned, and it must not silently vanish either.
        if (brandSplit
            && rows.TryGetValue((gscLatest, GscAgent.SegmentTotal), out var total)
            && rows.TryGetValue((gscLatest, GscAgent.SegmentBrand), out var brand)
            && rows.TryGetValue((gscLatest, GscAgent.SegmentNonBrand), out var nonBrand))
        {
            var split = brand.Clicks + nonBrand.Clicks;
            var residual = total.Clicks - split;
            var share = total.Clicks > 0 ? (double)split / total.Clicks * 100.0 : 0;
            (string, object?)[] args = [("percent", Shown1(share)), ("period", gscLatest)];

            if (total.Clicks > 0 && residual > 0 && (double)residual / total.Clicks >= AnonymizedNoticeThreshold)
            {
                annotations.Add(new Annotation(gscLatest, Info, Text(AnonymizedQueries, args)));
            }
            else if (residual < 0)
            {
                // Brand + non-brand ABOVE the total. It happens: the three figures are three separate aggregates that Search
                // Console rounds one by one. A small difference is noise, but it must not be invisible, because a LARGE one
                // means the brand pattern or the property is wrong.
                var severity = total.Clicks > 0 && -residual > total.Clicks * AnonymizedNoticeThreshold ? Warning : Info;
                annotations.Add(new Annotation(gscLatest, severity, Text(SegmentsExceedTotal, args)));
            }
        }

        // 2. The 16-month window. Older periods cannot be fetched, now or later, and are drawn as gaps, not zeros.
        if (gsc.OutOfWindowPeriods.Count > 0)
        {
            annotations.Add(new Annotation(null, Info,
                Text(OutsideGscWindow, ("count", gsc.OutOfWindowPeriods.Count), ("windowStart", gsc.WindowStart))));
        }

        // 3. Gaps INSIDE the window, from months before the source was connected.
        var missing = periods
            .Where(p => string.CompareOrdinal(p, gsc.WindowStart) >= 0
                        && string.CompareOrdinal(p, gscLatest) <= 0
                        && !rows.ContainsKey((p, GscAgent.SegmentTotal)))
            .ToList();
        if (missing.Count > 0)
        {
            annotations.Add(new Annotation(null, Info, Text(MissingGscMonths, ("from", missing[0]), ("to", missing[^1]))));
        }

        // 4. Findings from the page and segment data. Deterministic: the same figures give the same note, and none of them
        //    claims a CAUSE.
        annotations.AddRange(Findings(gsc, rows, gscLatest, brandSplit));

        return annotations;
    }

    /// <summary>
    /// Actionable findings, derived from figures already in the report (no extra API call). The other notes are about whether
    /// the DATA can be trusted; these are about what the data shows. They are the warnings the "To fix" list is made of.
    /// </summary>
    private static List<Annotation> Findings(
        GscFetch gsc,
        IReadOnlyDictionary<(string Period, string Segment), GscRow> rows,
        string gscLatest,
        bool brandSplit)
    {
        var findings = new List<Annotation>();
        var pages = gsc.Pages ?? [];

        static string Examples(IEnumerable<GscPageRow> source) => string.Join(", ", source.Take(FindingMaxExamples).Select(x => Trim(x.Url)));

        // A. The page is SEEN but not clicked. The position is already good, so it is the title and meta description, not the
        //    ranking: the cheapest fix there is. The threshold is named, not the first page's position: the rule only guarantees
        //    that ALL are better than it.
        var titleWork = pages
            .Where(x => x.Impressions >= FindingMinImpressions && x.Position > 0 && x.Position <= FindingGoodPosition && x.CtrFraction < FindingLowCtr)
            .OrderByDescending(x => x.Impressions)
            .ToList();

        if (titleWork.Count > 0)
        {
            findings.Add(new Annotation(gscLatest, Warning, Text(PagesNotClicked,
                ("count", titleWork.Count), ("pages", Examples(titleWork)), ("position", FindingGoodPosition),
                ("impressions", FindingMinImpressions), ("ctrPercent", FindingLowCtr * 100.0))));
        }

        // B. Impressions but a deep position. This is ranking, not the text in the search result.
        var rankWork = pages
            .Where(x => x.Impressions >= FindingMinImpressions && x.Position > FindingDeepPosition)
            .OrderByDescending(x => x.Impressions)
            .ToList();

        if (rankWork.Count > 0)
        {
            findings.Add(new Annotation(gscLatest, Warning, Text(PagesRankDeep,
                ("count", rankWork.Count), ("pages", Examples(rankWork)), ("impressions", FindingMinImpressions),
                ("position", FindingDeepPosition))));
        }

        // C. The same pattern for the segment: non-brand seen widely without clicks. Separate because it is the site's
        //    visibility to NEW visitors, not one page.
        if (brandSplit
            && rows.TryGetValue((gscLatest, GscAgent.SegmentNonBrand), out var nonBrand)
            && nonBrand.Impressions >= FindingMinImpressions * 3
            && nonBrand.CtrFraction < FindingLowCtr)
        {
            findings.Add(new Annotation(gscLatest, Warning, Text(NonBrandNotClicked,
                ("impressions", nonBrand.Impressions), ("clicks", nonBrand.Clicks),
                ("ctrPercent", Shown2(nonBrand.CtrFraction * 100.0)), ("position", Shown1(nonBrand.Position)))));
        }

        return findings;
    }

    /// <summary>
    /// Shortens a Search Console URL for display. The main site's origin goes; the host of ANY OTHER host stays, because a
    /// subdomain (shop.*) is a site of its own in practice and a note that hides that points at the wrong page.
    /// </summary>
    internal static string Trim(string url)
    {
        foreach (var scheme in new[] { "https://", "http://" })
        {
            if (!url.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)) continue;

            var rest = url[scheme.Length..];
            var slash = rest.IndexOf('/');
            var host = slash < 0 ? rest : rest[..slash];

            // Only "www." and the bare main domain are shortened away; everything else stays.
            if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) host = host[4..];

            // ONE dot is the main domain (example.com): show the path only. More is a subdomain: keep the host, or it cannot be
            // told apart from the main site in the table. A heuristic, not a rule: a domain under a two-part top-level domain
            // (example.co.uk) keeps its host. Ugly, not wrong; a list of top-level domains would never be complete.
            if (host.Count(c => c == '.') == 1)
            {
                var path = slash < 0 ? "/" : rest[slash..];
                return path.Length == 0 ? "/" : path;
            }

            return slash < 0 ? host : host + rest[slash..];
        }

        return url;
    }

    /// <summary>A rolling month axis: <paramref name="back"/> months back up to and including <paramref name="period"/>, oldest first.</summary>
    private static List<string> RollingPeriods(Period period, int back)
        => [.. Enumerable.Range(0, back + 1).Select(i => period.AddMonths(-(back - i)).ToString())];
}
