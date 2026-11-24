using System.Globalization;
using Esatto.Umbraco.Backoffice.SeoReport.Google;

namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

/// <summary>
/// The processing layer: deterministic and free of IO. It takes the raw GA4 and Search Console rows and builds the frozen
/// <see cref="Report"/>. All arithmetic lives here, never in the fetch layer and never in the client.
///
/// A port of an existing reference mapper with two deliberate changes. The report carries keys and arguments instead of prose:
/// every text is an id or a <see cref="TextRef"/> whose area and parts follow from where it sits (Report.cs, ReportKeys). And
/// periods and chart labels are raw values the client formats: <c>YYYY-MM</c>, or the month numbers <c>01</c>..<c>12</c> on a
/// chart pivoted by year. Every number is the one the reference computes for the same input; ReportMapperTests compares them
/// with a golden file made by the reference mapper. The partial files: this one (the build and GA4), YearOnYear (the trend and
/// its baseline check), Search (Search Console), Texts (the ids).
/// </summary>
public static partial class ReportMapper
{
    /// <summary>
    /// The contract version written into each report. It moves when the CONTENT of an existing member changes too (for example
    /// a new kind of note), not only when members change, so an archived month can be told apart from what the code makes now.
    /// </summary>
    public const string SchemaVersion = "1.0.0";

    private const string Live = "live";
    internal const string NoDataStatus = "noData";
    private const string Warning = "warning";
    private const string Info = "info";
    private const string Up = "up";
    private const string Down = "down";
    private const string Note = "note";
    private const string Solid = "solid";
    private const string Dashed = "dashed";
    private const string Line = "line";
    private const string Ramp = "ramp";
    private const string Integer = "integer";

    // The x-axis of a chart pivoted by year: calendar months, formatted by the client.
    private static readonly string[] MonthNumbers = [.. Enumerable.Range(1, 12).Select(m => m.ToString("D2", CultureInfo.InvariantCulture))];

    /// <summary>Builds a <see cref="Report"/> from GA4 and Search Console raw data.</summary>
    /// <param name="reportPeriod">The month the report is ABOUT.</param>
    /// <param name="rows">GA4 rows from <see cref="Ga4Agent"/>.</param>
    /// <param name="gsc">The Search Console fetch from <see cref="GscAgent"/>, or null when that source is missing.</param>
    /// <param name="gscProperty">The Search Console property, recorded in <see cref="Report.Measured"/>.</param>
    /// <param name="ga4Property">The GA4 property, recorded in <see cref="Report.Measured"/>.</param>
    /// <param name="brandRegex">
    /// The pattern that split brand from non-brand queries in the fetch. Null when the site has no brand terms: then there is no
    /// split, and nothing that depends on one (the segments, the brand charts, the notes about the split) is reported.
    /// </param>
    public static Report Build(
        Period reportPeriod,
        IReadOnlyList<Ga4Row> rows,
        GscFetch? gsc = null,
        string? gscProperty = null,
        string? ga4Property = null,
        string? brandRegex = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var report = reportPeriod.ToString();

        var organicByPeriod = SessionsBySegment(rows, Ga4Agent.SegmentOrganic);
        var totalByPeriod = SessionsBySegment(rows, Ga4Agent.SegmentTotal);

        // Google only, for the comparison with Search Console. Empty for raw data fetched before that segment existed; the
        // comparison then falls back to the channel group, which also holds Bing and others.
        var googleByPeriod = SessionsBySegment(rows, Ga4Agent.SegmentOrganicGoogle);

        // The latest period WITH data, not necessarily the report period. GA4 can lag, and then a KPI must say which month it
        // actually covers.
        var latestPeriod = organicByPeriod.Keys.OrderBy(p => p, StringComparer.Ordinal).LastOrDefault() ?? report;

        var organicYoy = YoyTrend(organicByPeriod, latestPeriod, UnitVisits);
        var organic = WithAnnotation(BuildYearChart(ChartOrganic, organicByPeriod), organicYoy.Annotation);
        var total = totalByPeriod.Count > 0 ? BuildYearChart(ChartTotal, totalByPeriod) : EmptyChart(ChartTotal);

        // KEY EVENTS. Zero everywhere does not mean "no leads": it means no key event is defined in GA4. A number claims an
        // outcome; a note says the measurement is missing.
        var keyEventsByPeriod = BySegment(rows, Ga4Agent.SegmentOrganic, r => r.KeyEvents);
        var anyKeyEvents = keyEventsByPeriod.Values.Any(v => v > 0);
        if (!anyKeyEvents && keyEventsByPeriod.Count > 0)
        {
            organic = WithAnnotation(organic, new Annotation(latestPeriod, Warning, Text(NoKeyEvents)));
        }

        var kpis = new List<Kpi>
        {
            new(OrganicSessions, organicByPeriod.TryGetValue(latestPeriod, out var latestValue) ? latestValue : null,
                Integer, SourceGa4, latestPeriod, organicYoy.Trend),
        };

        kpis.AddRange(QualityKpis(rows, latestPeriod));

        // Only when there ARE key events; otherwise the note above stands, because a KPI showing 0 reads as an outcome. Year on
        // year like everything else: at a handful a month, month on month is noise.
        if (anyKeyEvents)
        {
            kpis.Add(new Kpi(OrganicKeyEvents, keyEventsByPeriod.GetValueOrDefault(latestPeriod), Integer, SourceGa4, latestPeriod,
                YoyTrend(keyEventsByPeriod, latestPeriod, UnitKeyEvents).Trend));
        }

        // All traffic: a KPI and a chart of its own. It cannot be derived from the organic segment; it comes from its own call.
        if (totalByPeriod.TryGetValue(latestPeriod, out var totalLatest))
        {
            var totalYoy = YoyTrend(totalByPeriod, latestPeriod, UnitVisits);
            total = WithAnnotation(total, totalYoy.Annotation);
            kpis.Add(new Kpi(TotalSessions, totalLatest, Integer, SourceGa4, latestPeriod, totalYoy.Trend));
        }

        var sources = new List<SourceStatus> { new(SourceGa4, latestPeriod, latestPeriod == report ? null : Text(Ga4Incomplete)) };
        var searchComparison = EmptyChart(ChartSearchComparison);
        SearchBlock? search = null;

        if (gsc is not null)
        {
            var gscByKey = IndexRows(gsc);
            var gscLatest = GscLatestPeriod(gsc, report);
            sources.Add(new SourceStatus(SourceGsc, gscLatest, GscNote(gsc, gscLatest, report)));

            // Google against Google where possible: Search Console measures Google only. The KPI and the chart use the same
            // sessions, or the two would say different things about the same month.
            var googleSessions = googleByPeriod.Count > 0 ? googleByPeriod : organicByPeriod;
            kpis.AddRange(GscKpis(gscByKey, gscLatest, googleSessions, latestPeriod));
            searchComparison = BuildSearchComparison(googleSessions, gscByKey);
            search = BuildSearchBlock(gsc, gscByKey, gscLatest, reportPeriod, brandSplit: !string.IsNullOrWhiteSpace(brandRegex));
        }

        // Connected but nothing came back (a new site, a property without tracking): say so instead of charting zeros.
        var hasData = rows.Any(r => r.Sessions > 0) || (gsc?.Rows.Any(r => r.Clicks > 0 || r.Impressions > 0) ?? false);

        return new Report(
            SchemaVersion: SchemaVersion,
            ReportPeriod: report,
            GeneratedAt: DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            DataStatus: hasData ? Live : NoDataStatus,
            DataStatusNote: hasData ? null : Text(NoData),
            Sources: sources,
            Traffic: new TrafficSection(kpis, organic, total, searchComparison, search),
            Measured: gscProperty is null && ga4Property is null && brandRegex is null
                ? null
                : new Measured(gscProperty, ga4Property, brandRegex));
    }

    /// <summary>
    /// The QUALITY of the organic traffic: ratios, not counts. GA4 under-records sessions (Search Console's clicks exceed them
    /// systematically), but a ratio is computed over the sessions GA4 does see and so survives a loss that a count does not.
    /// No year-on-year change: a percentage change OF a percentage reads wrongly, so last year's figure goes in the note.
    /// </summary>
    private static IEnumerable<Kpi> QualityKpis(IReadOnlyList<Ga4Row> rows, string latestPeriod)
    {
        var current = rows.FirstOrDefault(r => r.Segment == Ga4Agent.SegmentOrganic && r.Period == latestPeriod);
        var yearAgo = rows.FirstOrDefault(r => r.Segment == Ga4Agent.SegmentOrganic && r.Period == ShiftYearBack(latestPeriod));

        if (current is { EngagementRate: > 0 })
        {
            yield return new Kpi(OrganicEngagementRate, Percent1(current.EngagementRate), "percent", SourceGa4, latestPeriod,
                new Trend(Note, null, yearAgo is { EngagementRate: > 0 }
                    ? Text(EngagementVsLastYear, ("percent", Shown1(yearAgo.EngagementRate * 100.0)))
                    : Text(EngagementShare)));
        }

        if (current is { AvgSessionSeconds: > 0 })
        {
            yield return new Kpi(OrganicSessionSeconds, Math.Round(current.AvgSessionSeconds), "duration", SourceGa4, latestPeriod,
                new Trend(Note, null, yearAgo is { AvgSessionSeconds: > 0 }
                    ? Text(DurationVsLastYear, ("seconds", (long)Math.Round(yearAgo.AvgSessionSeconds)))
                    : Text(DurationPerVisit)));
        }
    }

    /// <summary>
    /// Monthly data pivoted to one series per year on a January-December axis, the comparison the dashboard shows. The latest
    /// year is emphasized; years before last year are dashed.
    /// </summary>
    private static ChartBlock BuildYearChart(string id, IReadOnlyDictionary<string, long> byPeriod)
    {
        var years = YearsOf(byPeriod.Keys);
        var latestYear = years.Count > 0 ? years[^1] : 0;

        var series = years.Select(year => new Series(
            Key: YearKey(SeriesYear, year),
            Values: MonthValues(byPeriod, year),
            Emphasis: year == latestYear,
            Style: year < latestYear - 1 ? Dashed : Solid)).ToList();

        return new ChartBlock(id, Line, Ramp, 290, [.. MonthNumbers], series, [], []);
    }

    /// <summary>A chart with nothing in it yet. The client shows a no-data message for it, never an empty plot.</summary>
    private static ChartBlock EmptyChart(string id) => new(id, Line, Ramp, 240, [], [], [], []);

    private static ChartBlock WithAnnotation(ChartBlock chart, Annotation? annotation)
        => annotation is null ? chart : chart with { Annotations = [.. chart.Annotations, annotation] };

    /// <summary>
    /// One GA4 segment as period to value. The segment is a NAMED field, not a position in the list; a duplicated period keeps
    /// the last row instead of failing the run.
    /// </summary>
    private static Dictionary<string, long> BySegment(IReadOnlyList<Ga4Row> rows, string segment, Func<Ga4Row, long> value)
        => rows.Where(r => string.Equals(r.Segment, segment, StringComparison.Ordinal))
            .GroupBy(r => r.Period, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => value(g.Last()), StringComparer.Ordinal);

    private static Dictionary<string, long> SessionsBySegment(IReadOnlyList<Ga4Row> rows, string segment)
        => BySegment(rows, segment, r => r.Sessions);

    /// <summary>The twelve months of one year; a month without data is null (a gap in the line), never 0.</summary>
    private static List<double?> MonthValues(IReadOnlyDictionary<string, long> byPeriod, int year)
        => [.. Enumerable.Range(1, 12).Select(m => byPeriod.TryGetValue($"{year:D4}-{m:D2}", out var v) ? (double?)v : null)];

    /// <summary>The distinct years of some periods, oldest first; a period without a readable year is skipped.</summary>
    private static List<int> YearsOf(IEnumerable<string> periods)
        => [.. periods.Select(YearOf).Where(y => y > 0).Distinct().Order()];

    private static int YearOf(string period)
        => period.Length >= 4 && int.TryParse(period.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year) ? year : 0;

    /// <summary>A series id with its year as the suffix ReportKeys strips for the label: <c>sessions_2026</c>.</summary>
    private static string YearKey(string kind, int year) => $"{kind}_{year:D4}";

    /// <summary>A fraction as a percentage with one decimal (0.6123 is 61.2).</summary>
    private static double Percent1(double fraction) => Math.Round(fraction * 100.0, 1);

    /// <summary>
    /// A figure for a TEXT argument, rounded to one decimal exactly as the reference printed it in its notes (format "0.0": half
    /// away from zero on the printed digits), so a note shows the very number the reference showed. KPI and table values keep
    /// Math.Round, as in the reference; at an exact half the two can differ in the last decimal (100.25 is 100.3 in a note, 100.2
    /// in a table), as in the reference.
    /// </summary>
    internal static double Shown1(double value) => Shown(value, "0.0");

    /// <summary>As <see cref="Shown1"/>, with up to two decimals (format "0.##"): how the reference printed a click-through rate in a note.</summary>
    private static double Shown2(double value) => Shown(value, "0.##");

    private static double Shown(double value, string format)
        => double.Parse(value.ToString(format, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    /// <summary>A fraction as a percentage with two decimals, the precision click-through rates are shown with.</summary>
    private static double Percent2(double fraction) => Math.Round(fraction * 100.0, 2);
}
