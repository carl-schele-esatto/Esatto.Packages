using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>
/// A small synthetic archive with something for every part of the analysis to say: four months (July 2026, the month before it, one
/// further back and the same month a year earlier) with pages that gained and lost, queries on page one and page two, two pages
/// competing for one query, KPIs that disagree and a share of search queries in the top three that is best in the newest month.
/// Every name, path and figure is made up.
/// </summary>
internal static class AnalysisArchive
{
    public const string Newest = "2026-07";

    /// <summary>The target queries the happy path uses: a topic with a page, one on page two and one nobody searched for.</summary>
    public static readonly string[] Terms = ["red shoes", "blue hat", "garden gnome"];

    /// <summary>The archive, oldest month first.</summary>
    public static List<Report> Reports() => [YearAgo(), May(), June(), July()];

    public static SearchLandingPage Page(string path, long impressions, double position = 6.0)
        => new($"https://example.com{path}", path, Clicks: impressions / 20, Impressions: impressions, CtrPercent: 5.0, Position: position);

    public static SearchQuery Query(string text, long impressions, double position, long clicks = 2)
        => new(text, clicks, impressions, CtrPercent: 2.0, Position: position);

    public static SearchQueryPage Pair(string query, string path, long impressions, double position)
        => new(query, $"https://example.com{path}", path, Clicks: 1, Impressions: impressions, CtrPercent: 1.0, Position: position);

    /// <summary>The same report with two warnings and an information note in its search block: the findings, and what a user outside the groups must not get.</summary>
    public static Report WithFindings(Report report)
    {
        var search = report.Traffic.Search!;
        return report with
        {
            Traffic = report.Traffic with
            {
                Search = search with
                {
                    Annotations =
                    [
                        new Annotation(report.ReportPeriod, "warning", new TextRef("brandDominated")),
                        new Annotation(report.ReportPeriod, "info", new TextRef("anonymizedQueries")),
                        new Annotation(report.ReportPeriod, "warning", new TextRef("noKeyEvents")),
                    ],
                },
            },
        };
    }

    // 25 % of the queries in the top three.
    private static Report YearAgo() => TestReports.With(
        "2025-07",
        queries: [Query("red shoes", 100, 2.0), Query("shoes", 100, 8.0), Query("hat", 100, 15.0), Query("cap", 100, 30.0)],
        pages: [Page("/", 1000), Page("/shoes", 800), Page("/old", 500)],
        queryPages: []);

    private static Report May() => TestReports.With(
        "2026-05",
        queries: [Query("red shoes", 100, 2.0), Query("shoes", 100, 8.0), Query("hat", 100, 15.0), Query("cap", 100, 30.0)],
        pages: [Page("/", 900), Page("/shoes", 700)],
        queryPages: []);

    // 50 %.
    private static Report June() => TestReports.With(
        "2026-06",
        queries: [Query("red shoes", 150, 2.0), Query("shoes", 150, 3.0), Query("hat", 150, 15.0), Query("cap", 150, 30.0)],
        pages: [Page("/", 1100), Page("/shoes", 900), Page("/hats", 100)],
        queryPages: []);

    // 75 %: the best month, so the visibility statement names no third month. The search lists give both kinds of opportunity, a
    // page for "red shoes" and a query on page two for "blue hat". The KPIs disagree, and the pages moved both ways.
    private static Report July() => TestReports.With(
        "2026-07",
        queries:
        [
            Query("red shoes", 900, 2.1, clicks: 120),
            Query("shoes", 700, 2.8, clicks: 60),
            Query("buy shoes", 300, 3.0, clicks: 30),
            Query("blue hat", 400, 14.2, clicks: 4),
        ],
        pages: [Page("/", 600), Page("/shoes", 1500), Page("/new", 400), Page("/hats", 50)],
        queryPages:
        [
            Pair("red shoes", "/shoes", 600, 4.0),
            Pair("red shoes", "/sale", 300, 9.0),
            Pair("blue hat", "/hats", 400, 14.2),
        ],
        kpis:
        [
            TestReports.Kpi(ReportMapper.GscClicks, 1200, "up", 12.0),
            TestReports.Kpi(ReportMapper.OrganicSessions, 1000, "down", -5.0),
            TestReports.Kpi(ReportMapper.OrganicKeyEvents, 12),
        ],
        segments: [TestReports.Segment(GscAgent.SegmentNonBrand, 300)]);
}
