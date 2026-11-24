using System.Text.Json.Serialization;

namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

// =============================================================================
// THE CONTRACT (report.json)
//
// A trimmed version of an existing monthly SEO report contract: the same JSON property names for every member kept, minus
// what a package has no use for (client, agency, the keywords/positions/deliverables/actions sections).
//
// The producer (the processing layer) writes it, the archive stores it, the dashboard reads it.
//
// Rules:
//   * A property name never changes without bumping schema_version.
//   * Arithmetic belongs to the processing layer, never to the client.
//   * No presentation: no hex colours, CSS classes or formatted strings. The client owns colour and formatting.
//   * No prose. Every text is a TextRef (a base id plus arguments); labels, titles, subtitles and descriptions
//     are derived from an id with ReportKeys.Lookup, so there are no label properties at all.
//   * No positional meaning. Everything is addressed by a named property.
//   * Periods are raw "YYYY-MM" strings (see Period); the client formats them for the user's language.
//
// WHERE A TEXT SITS DECIDES ITS KEY. The client resolves every id and TextRef by its location alone, with
// ReportKeys.Lookup(area, id, part) for each part listed (ReportKeys registers exactly these):
//
//   location                                   area        parts
//   Report.DataStatusNote.Key                  ui          Text
//   SourceStatus.Id                            ui          Label
//   SourceStatus.Note.Key                      ui          Text
//   TrafficSection.Kpis[].Key                  kpi         Label, Description
//   SearchSegment.Metrics[].Key                kpi         Label
//   Trend.Text.Key (any KPI)                   kpi         Text
//   ChartBlock.Id                              chart       Title, Subtitle, Description
//   SearchBlock.Id                             chart       Title, Subtitle
//   Series.Key, SearchSegment.Key              series      Label   (a trailing "_YYYY" is stripped and passed as arg "year")
//   Annotation.Text.Key                        annotation  Title, Text   (both get the same arguments)
//   ChartBlock.Insights[], SearchBlock.Insights[]  insight Text
//
// A TextRef's Args go to every part of it unchanged (numbers raw, periods "YYYY-MM", dates "YYYY-MM-DD").
// =============================================================================

/// <summary>The root object of report.json.</summary>
public record Report(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,

    // The month the report is about, "YYYY-MM".
    [property: JsonPropertyName("report_period")] string ReportPeriod,

    [property: JsonPropertyName("generated_at")] string GeneratedAt,

    // "live" | "noData". "noData": connected, but neither GA4 nor Search Console returned anything for the report, so
    // the client shows a no-data message instead of charts. Null reads as "live".
    [property: JsonPropertyName("data_status")] string? DataStatus,

    // Explanation shown with a status other than "live": ui<Key>Text.
    [property: JsonPropertyName("data_status_note")] TextRef? DataStatusNote,

    // Data freshness per source. The report period is not the latest available data point: GA4 and Search Console
    // lag by different amounts, and without this a "June" report would quietly show May numbers in its KPI cards.
    [property: JsonPropertyName("sources")] List<SourceStatus> Sources,

    [property: JsonPropertyName("traffic")] TrafficSection Traffic,

    // WHAT was measured, so every number can be reproduced in Google's own interfaces; without the property and the
    // filter a figure is unverifiable. Nullable: older archived reports lack it.
    [property: JsonPropertyName("measured")] Measured? Measured = null
);

/// <summary>The identity of the sources and the filter that was applied, for reproducing a number in GA4 and GSC.</summary>
public record Measured(
    // For example "sc-domain:example.com"; a domain property includes subdomains.
    [property: JsonPropertyName("gsc_property")] string? GscProperty,

    // For example "properties/123456789".
    [property: JsonPropertyName("ga4_property")] string? Ga4Property,

    // The regex that splits brand from non-brand; paste it into Search Console's own query filter.
    [property: JsonPropertyName("brand_regex")] string? BrandRegex
);

/// <summary>The freshness of one data source. Written by the fetch layer, never guessed by the client.</summary>
public record SourceStatus(
    // "ga4" or "gsc"; also the text base of the source's name: ui<Id>Label.
    [property: JsonPropertyName("id")] string Id,

    // The latest period with complete data, "YYYY-MM". May be earlier than the report period.
    [property: JsonPropertyName("latest_period")] string LatestPeriod,

    // A remark about the data lag, if any: ui<Key>Text.
    [property: JsonPropertyName("note")] TextRef? Note
);

// -----------------------------------------------------------------------------
// Shared building blocks
// -----------------------------------------------------------------------------

/// <summary>
/// A KPI card. The value is a NUMBER, not a formatted string: the client formats it per <see cref="Format"/> and the
/// user's language. <see cref="Key"/> is the text base of its label (<c>kpi&lt;Key&gt;Label</c>) and, for a traffic KPI,
/// its description (<c>kpi&lt;Key&gt;Description</c>); a Search Console segment's measures have a label only.
/// </summary>
public record Kpi(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("value")] double? Value,

    // "integer" | "signed" | "percent" | "decimal" | "duration" (seconds, shown as minutes:seconds).
    [property: JsonPropertyName("format")] string Format,

    // Which source the figure comes from (an id in Report.Sources).
    [property: JsonPropertyName("source")] string? Source,

    // The period the figure actually covers, "YYYY-MM". Often not the report period.
    [property: JsonPropertyName("period")] string? Period,

    [property: JsonPropertyName("trend")] Trend? Trend
);

/// <summary>
/// A trend indicator: a computed percentage change or a free-standing note. The direction is semantic
/// ("up"/"down"/"note"); the client picks the colour.
/// </summary>
public record Trend(
    // "up" | "down" | "note".
    [property: JsonPropertyName("direction")] string Direction,
    [property: JsonPropertyName("change_percent")] double? ChangePercent,

    // What the change is measured against, or the note itself: kpi<Key>Text.
    [property: JsonPropertyName("text")] TextRef? Text
);

/// <summary>
/// A chart: the data only, no colour and no CSS. The client assigns colours from <see cref="Palette"/> and the series
/// order. <see cref="Id"/> is the text base of the chart's title, subtitle and description (<c>chart&lt;Id&gt;Title</c>,
/// <c>chart&lt;Id&gt;Subtitle</c>, <c>chart&lt;Id&gt;Description</c>); the description explains the measure, never the outcome, which belongs to
/// <see cref="Insights"/> and <see cref="Annotations"/>. A chart with no series has no data; the client says so instead of
/// drawing it.
/// </summary>
public record ChartBlock(
    [property: JsonPropertyName("id")] string Id,

    // "line" | "bar".
    [property: JsonPropertyName("type")] string Type,

    // "ramp": ordinal, one hue light to dark (years by recency).
    // "categorical": identity (competitor domains); slot 0 is the site's own accent. Max 6 series.
    // "segment": identity where ALL series are the site's own (brand vs non-brand), so the accent is left out rather
    // than meaning two things in one report. Max 2 series.
    [property: JsonPropertyName("palette")] string Palette,

    [property: JsonPropertyName("height")] int Height,

    // The x-axis categories, raw for the client to format. A chart pivoted by year (one series per year: organic, total,
    // search_comparison) has the calendar months "01".."12"; every other chart has "YYYY-MM" periods.
    [property: JsonPropertyName("labels")] List<string> Labels,

    [property: JsonPropertyName("series")] List<Series> Series,

    // DETERMINISTIC data notes (measurement gaps, tracking outages, bot traffic), found by the processing layer.
    [property: JsonPropertyName("annotations")] List<Annotation> Annotations,

    // INTERPRETING points about what the curve does: insight<Key>Text.
    [property: JsonPropertyName("insights")] List<TextRef> Insights
);

/// <summary>
/// A data series. <see cref="Values"/> is as long as the chart's labels; null means "no data point" (a gap in the
/// line), never 0. <see cref="Key"/> is the text base of the series' label, <c>series&lt;Key&gt;Label</c>; on a chart pivoted by
/// year it ends in the year (<c>year_2026</c>, <c>sessions_2026</c>, <c>clicks_2026</c>), which the lookup strips and hands to
/// the label as the argument <c>year</c> (<see cref="ReportKeys.SeriesYear"/>).
/// </summary>
public record Series(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("values")] List<double?> Values,

    // True for the series that is "the site's own"/current: it gets the primary colour and a thicker line.
    [property: JsonPropertyName("emphasis")] bool Emphasis,

    // "solid" | "dashed". Dashed means historical/comparison.
    [property: JsonPropertyName("style")] string Style
);

/// <summary>
/// A deterministic note tied to a period: an established fact about the data, deliberately kept apart from insights,
/// which interpret. <see cref="Text"/> carries the annotation id and its arguments; the client shows
/// <c>annotation&lt;Id&gt;Title</c> and <c>annotation&lt;Id&gt;Text</c>.
/// </summary>
public record Annotation(
    [property: JsonPropertyName("period")] string? Period,

    // "warning" | "info".
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("text")] TextRef Text
);

// -----------------------------------------------------------------------------
// Traffic
// -----------------------------------------------------------------------------

public record TrafficSection(
    [property: JsonPropertyName("kpis")] List<Kpi> Kpis,
    [property: JsonPropertyName("organic")] ChartBlock Organic,
    [property: JsonPropertyName("total")] ChartBlock Total,

    // GA4 sessions vs GSC clicks, pivoted by year. Ordinal ramp.
    [property: JsonPropertyName("search_comparison")] ChartBlock SearchComparison,

    // Search Console split into brand and non-brand. Nullable: older reports lack it and the block is skipped.
    [property: JsonPropertyName("search")] SearchBlock? Search = null
);

/// <summary>
/// The Search Console block: the same measures (clicks, impressions, CTR, average position) per SEGMENT instead of in
/// one lump. The split is made by the fetch layer with a query-dimension filter (includingRegex = the brand regex for
/// brand, excludingRegex for non-brand).
///
/// The block carries no grand total in <see cref="Segments"/>: the unfiltered total lives in the traffic KPIs as
/// "gsc_clicks" and can exceed brand + non-brand, because anonymized queries drop out as soon as a query filter is
/// applied. The difference is an <see cref="Annotation"/>, not a hidden rounding.
/// </summary>
public record SearchBlock(
    // The text base of the block's heading: chart<Id>Title and chart<Id>Subtitle.
    [property: JsonPropertyName("id")] string Id,

    // At most two segments (brand, non_brand): the "segment" palette is validated for two series.
    [property: JsonPropertyName("segments")] List<SearchSegment> Segments,

    // Clicks per month, one series per segment. Palette "segment".
    [property: JsonPropertyName("clicks")] ChartBlock Clicks,

    // Impressions per month, one series per segment. Palette "segment".
    [property: JsonPropertyName("impressions")] ChartBlock Impressions,

    // Deterministic notes about the whole block: anonymized queries, the 16-month window, data gaps, and the findings
    // (severity "warning", the "To fix" list).
    [property: JsonPropertyName("annotations")] List<Annotation> Annotations,

    // insight<Key>Text.
    [property: JsonPropertyName("insights")] List<TextRef> Insights,

    // The report month's most-clicked queries. Nullable: older reports lack it, and an archived report must not need
    // migrating to be read. The queries always sum to less than the segment totals (Search Console anonymizes rare
    // queries once the query dimension is requested), so never present this as a breakdown of the total.
    [property: JsonPropertyName("queries")] List<SearchQuery>? Queries = null,

    // The report month's most-clicked LANDING PAGES, with the same anonymization caveat as Queries.
    // Queries say WHAT people searched, pages say WHERE they landed; without both a drop can be seen but not located.
    [property: JsonPropertyName("pages")] List<SearchLandingPage>? Pages = null,

    // The UNFILTERED total for the period. Kept outside Segments on purpose: that list drives the chart
    // series and is at most two long. The total belongs in the table, never in the curve, and it must stay visible
    // because brand + non-brand never add up to it.
    [property: JsonPropertyName("total")] SearchSegment? Total = null,

    // Query x page, from one call with both dimensions: the basis for naming WHICH page ranks for a target query.
    // Nullable: older reports lack it.
    [property: JsonPropertyName("query_pages")] List<SearchQueryPage>? QueryPages = null
);

/// <summary>
/// A landing page with the report month's measures. <see cref="Url"/> is GSC's own full URL.
/// Not called SearchPage: a site document type of that name would clash with ModelsBuilder's namespace.
/// </summary>
public record SearchLandingPage(
    [property: JsonPropertyName("url")] string Url,

    // The URL shortened for display: the site's own origin removed, subdomain hosts kept. Computed once in the
    // processing layer so every view shows the same thing without knowing the site's domain.
    [property: JsonPropertyName("path")] string? Path,
    [property: JsonPropertyName("clicks")] long Clicks,
    [property: JsonPropertyName("impressions")] long Impressions,
    [property: JsonPropertyName("ctr_percent")] double CtrPercent,
    [property: JsonPropertyName("position")] double Position
);

/// <summary>
/// A query AND the page it was shown for (GSC with both dimensions). It exists to answer "which page should I work
/// on": the query list and the page list are separate aggregates and nothing in them links a query to a page.
/// </summary>
public record SearchQueryPage(
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("url")] string Url,

    // The URL shortened for display; computed like SearchLandingPage.Path.
    [property: JsonPropertyName("path")] string? Path,
    [property: JsonPropertyName("clicks")] long Clicks,
    [property: JsonPropertyName("impressions")] long Impressions,
    [property: JsonPropertyName("ctr_percent")] double CtrPercent,
    [property: JsonPropertyName("position")] double Position
);

/// <summary>
/// A query with the report month's measures. CTR is in PERCENT and position a decimal: the same units as
/// <see cref="SearchSegment.Metrics"/>, not the source's own fraction.
/// </summary>
public record SearchQuery(
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("clicks")] long Clicks,
    [property: JsonPropertyName("impressions")] long Impressions,
    [property: JsonPropertyName("ctr_percent")] double CtrPercent,
    [property: JsonPropertyName("position")] double Position
);

/// <summary>
/// A GSC segment. <see cref="Key"/> is "brand" or "non_brand" and drives both the series colour and the order (and is
/// the text base of its name, <c>series&lt;Key&gt;Label</c>, as on the brand charts' series). <see cref="Metrics"/> are
/// ordinary <see cref="Kpi"/>s, so formatting and period marking work as everywhere else ("integer" for
/// clicks/impressions, "percent" for CTR, "decimal" for average position).
/// </summary>
public record SearchSegment(
    // "brand" | "non_brand", or "total" for SearchBlock.Total.
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("metrics")] List<Kpi> Metrics
);
