using Esatto.Umbraco.Backoffice.SeoReport.Reporting;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>
/// Builds the smallest report the analysis classes can read: a period and, optionally, the KPIs and the search lists they look
/// at. A list left out is null, as in a report archived before that list was fetched; an empty list is a month that was fetched
/// and had nothing in it. Without any search list or segment the report has no search block at all.
/// </summary>
internal static class TestReports
{
    public static Report With(
        string period,
        List<SearchQuery>? queries = null,
        List<SearchLandingPage>? pages = null,
        List<SearchQueryPage>? queryPages = null,
        List<Kpi>? kpis = null,
        List<SearchSegment>? segments = null)
    {
        var hasSearch = queries is not null || pages is not null || queryPages is not null || segments is not null;

        return new Report(
            SchemaVersion: "1.0.0",
            ReportPeriod: period,
            GeneratedAt: "2026-11-01T06:00:00Z",
            DataStatus: null,
            DataStatusNote: null,
            Sources: [],
            Traffic: new TrafficSection(
                Kpis: kpis ?? [],
                Organic: EmptyChart("organic"),
                Total: EmptyChart("total"),
                SearchComparison: EmptyChart("search_comparison"),
                Search: hasSearch ? SearchBlockWith(queries, pages, queryPages, segments) : null));
    }

    /// <summary>A KPI with a value and, when a direction is given, a trend of that direction and size ("up" and 12.0 is +12 percent).</summary>
    public static Kpi Kpi(string key, double? value, string? direction = null, double? changePercent = null) =>
        new(key, value, "integer", Source: null, Period: null,
            Trend: direction is null ? null : new Trend(direction, changePercent, Text: null));

    /// <summary>A Search Console segment with its clicks, as the mapper builds one: the single metric is "&lt;segment&gt;_clicks".</summary>
    public static SearchSegment Segment(string key, double clicks) => new(key, [Kpi($"{key}_clicks", clicks)]);

    private static SearchBlock SearchBlockWith(
        List<SearchQuery>? queries, List<SearchLandingPage>? pages, List<SearchQueryPage>? queryPages, List<SearchSegment>? segments)
        => new(
            Id: "traffic_search",
            Segments: segments ?? [],
            Clicks: EmptyChart("traffic_search_clicks"),
            Impressions: EmptyChart("traffic_search_impressions"),
            Annotations: [],
            Insights: [],
            Queries: queries,
            Pages: pages,
            QueryPages: queryPages);

    private static ChartBlock EmptyChart(string id) => new(id, "line", "ramp", 280, [], [], [], []);
}
