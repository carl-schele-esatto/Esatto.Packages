using Esatto.Umbraco.Backoffice.SeoReport.Google;

namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

// The ids the mapper puts in a report, grouped by where they sit. Report.cs documents the area and parts each place is looked
// up with; ReportKeys registers each group from here, so what is emitted and what is registered come from one list.
public static partial class ReportMapper
{
    // Traffic KPIs (Traffic.Kpis[].Key): kpi<Key>Label and kpi<Key>Description.
    internal const string OrganicSessions = "organic_sessions";
    internal const string OrganicEngagementRate = "organic_engagement_rate";
    internal const string OrganicSessionSeconds = "organic_session_seconds";
    internal const string OrganicKeyEvents = "organic_key_events";
    internal const string TotalSessions = "total_sessions";
    internal const string GscClicks = "gsc_clicks";
    internal const string ClicksVsSessions = "clicks_vs_sessions";

    internal static readonly string[] TrafficKpis =
        [OrganicSessions, OrganicEngagementRate, OrganicSessionSeconds, OrganicKeyEvents, TotalSessions, GscClicks, ClicksVsSessions];

    // Search Console segments (SearchBlock.Segments[].Key and SearchBlock.Total.Key): series<Key>Label. The same ids key the
    // brand charts' series.
    private static readonly string[] SplitSegments = [GscAgent.SegmentBrand, GscAgent.SegmentNonBrand];

    private const string MetricClicks = "clicks";
    private const string MetricImpressions = "impressions";
    private const string MetricCtr = "ctr";
    private const string MetricPosition = "position";

    private static readonly string[] MetricNames = [MetricClicks, MetricImpressions, MetricCtr, MetricPosition];

    // A segment's measures (SearchSegment.Metrics[].Key, "<segment>_<measure>"): kpi<Key>Label.
    internal static readonly string[] SegmentMetricKeys =
        [.. from segment in SplitSegments.Append(GscAgent.SegmentTotal) from metric in MetricNames select MetricKey(segment, metric)];

    // Trend texts (Trend.Text.Key): kpi<Key>Text.
    internal const string VsSameMonthLastYear = "vsSameMonthLastYear";
    internal const string NoComparison = "noComparison";
    internal const string AnomalousBaseline = "anomalousBaseline";
    internal const string EngagementVsLastYear = "engagementVsLastYear";
    internal const string EngagementShare = "engagementShare";
    internal const string DurationVsLastYear = "durationVsLastYear";
    internal const string DurationPerVisit = "durationPerVisit";
    internal const string ClicksMinusVisits = "clicksMinusVisits";

    internal static readonly string[] TrendTexts =
    [
        VsSameMonthLastYear, NoComparison, AnomalousBaseline, EngagementVsLastYear, EngagementShare, DurationVsLastYear,
        DurationPerVisit, ClicksMinusVisits,
    ];

    // Charts (ChartBlock.Id): chart<Id>Title, chart<Id>Subtitle and chart<Id>Description.
    internal const string ChartOrganic = "traffic_organic";
    internal const string ChartTotal = "traffic_total";
    internal const string ChartSearchComparison = "traffic_search_comparison";
    internal const string ChartSearchClicks = "traffic_search_clicks";
    internal const string ChartSearchImpressions = "traffic_search_impressions";

    internal static readonly string[] Charts = [ChartOrganic, ChartTotal, ChartSearchComparison, ChartSearchClicks, ChartSearchImpressions];

    // The Search Console block (SearchBlock.Id): chart<Id>Title and chart<Id>Subtitle.
    internal const string SearchBlockId = "traffic_search";

    // Series (Series.Key and the segment keys): series<Key>Label. The year-pivoted series end in "_<YYYY>", which the lookup
    // strips and hands over as the argument "year" (see ReportKeys).
    internal const string SeriesYear = "year";
    internal const string SeriesSessions = "sessions";
    internal const string SeriesClicks = "clicks";

    internal static readonly string[] SeriesKinds = [SeriesYear, SeriesSessions, SeriesClicks, .. SplitSegments, GscAgent.SegmentTotal];

    // Annotations (Annotation.Text.Key): annotation<Key>Title and annotation<Key>Text.
    internal const string NoKeyEvents = "noKeyEvents";
    internal const string AnonymizedQueries = "anonymizedQueries";
    internal const string SegmentsExceedTotal = "segmentsExceedTotal";
    internal const string OutsideGscWindow = "outsideGscWindow";
    internal const string MissingGscMonths = "missingGscMonths";
    internal const string PagesNotClicked = "pagesNotClicked";
    internal const string PagesRankDeep = "pagesRankDeep";
    internal const string NonBrandNotClicked = "nonBrandNotClicked";

    internal static readonly string[] AnnotationTexts =
    [
        NoKeyEvents, AnomalousBaseline, AnonymizedQueries, SegmentsExceedTotal, OutsideGscWindow, MissingGscMonths,
        PagesNotClicked, PagesRankDeep, NonBrandNotClicked,
    ];

    // Sources (SourceStatus.Id): ui<Id>Label. Their notes (SourceStatus.Note.Key): ui<Key>Text.
    internal const string SourceGa4 = "ga4";
    internal const string SourceGsc = "gsc";

    internal static readonly string[] Sources = [SourceGa4, SourceGsc];

    internal const string Ga4Incomplete = "ga4Incomplete";
    internal const string GscLag = "gscLag";
    internal const string GscIncomplete = "gscIncomplete";
    internal const string GscNoData = "gscNoData";

    internal static readonly string[] SourceNotes = [Ga4Incomplete, GscLag, GscIncomplete, GscNoData];

    // The report's status note (Report.DataStatusNote.Key): ui<Key>Text.
    internal const string NoData = "noData";

    internal static readonly string[] DataStatusNotes = [NoData];

    // The unit a year-on-year note counts in (argument "unit"); en.ts turns it into a word.
    internal const string UnitVisits = "visits";
    internal const string UnitKeyEvents = "keyEvents";
    internal const string UnitClicks = "clicks";

    private static string MetricKey(string segment, string metric) => $"{segment}_{metric}";

    /// <summary>A text reference; arguments only when there are any, so the JSON omits an empty "args".</summary>
    private static TextRef Text(string key, params (string Name, object? Value)[] args) => TextRef.Of(key, args);
}
