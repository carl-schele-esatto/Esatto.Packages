using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The report mapper, ported from Nova's TrafficMapper. This half builds whole reports from synthetic rows with numbers simple
/// enough to work out by hand and checks the behaviour the dashboard leans on; the golden half proves every number equals what
/// Nova's own mapper produces for the same input, and the text half checks that the report carries keys, never prose.
/// </summary>
public partial class ReportMapperTests
{
    private const string BrandRegex = "(?i)(acme)";

    private static readonly Period October2026 = new(2026, 10);

    /// <summary>
    /// Fourteen months, 2025-09 to 2026-10 (i = 0..13), of the three GA4 segments:
    /// organic 1000 + 10i sessions (engagement 0.40 + 0.01i, 60 + 2i seconds), total 3000 + 20i, organic from Google 900 + 10i.
    /// </summary>
    private static List<Ga4Row> History(Func<int, long>? keyEvents = null)
    {
        var rows = new List<Ga4Row>();
        for (var i = 0; i < 14; i++)
        {
            var period = new Period(2025, 9).AddMonths(i).ToString();
            rows.Add(new Ga4Row(period, Ga4Agent.SegmentOrganic, 1000 + 10 * i,
                EngagementRate: 0.40 + 0.01 * i, AvgSessionSeconds: 60 + 2 * i, KeyEvents: keyEvents?.Invoke(i) ?? 1 + i % 3));
            rows.Add(new Ga4Row(period, Ga4Agent.SegmentTotal, 3000 + 20 * i));
            rows.Add(new Ga4Row(period, Ga4Agent.SegmentOrganicGoogle, 900 + 10 * i));
        }

        return rows;
    }

    /// <summary>
    /// Search Console for 2025-10 to 2026-10 (j = 0..12): total 2000 + 100j clicks, brand 500 + 10j, non-brand the total minus
    /// brand minus 100 (anonymized) on 400 000 impressions (seen but hardly clicked), plus one page that is seen but not clicked.
    /// </summary>
    private static GscFetch Search()
    {
        var rows = new List<GscRow>();
        for (var j = 0; j < 13; j++)
        {
            var period = new Period(2025, 10).AddMonths(j).ToString();
            long total = 2000 + 100 * j;
            long brand = 500 + 10 * j;
            rows.Add(Gsc(period, GscAgent.SegmentTotal, total, 80_000 + 1_000 * j, 12.0));
            rows.Add(Gsc(period, GscAgent.SegmentBrand, brand, 4_000, 2.0));
            rows.Add(Gsc(period, GscAgent.SegmentNonBrand, total - brand - 100, 400_000, 14.0));
        }

        return new GscFetch(rows, "2026-11-15", "2026-10", "2025-08", [],
            Pages: [new GscPageRow("https://example.com/gadgets/blue", 4, 2_000, 0.002, 6.5)]);
    }

    private static GscRow Gsc(string period, string segment, long clicks, long impressions, double position)
        => new(period, segment, clicks, impressions, (double)clicks / impressions, position);

    private static Kpi? Kpi(Report report, string key) => report.Traffic.Kpis.FirstOrDefault(k => k.Key == key);

    private static Series SeriesOf(ChartBlock chart, string key) => Assert.Single(chart.Series, s => s.Key == key);

    private static double?[] Months(params (int Month, double Value)[] values)
    {
        var months = new double?[12];
        foreach (var (month, value) in values) months[month - 1] = value;
        return months;
    }

    // -----------------------------------------------------------------------------------------------------------------
    // GA4: series, year on year, the ring, the KPI list
    // -----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_organic_and_total_series_put_each_month_in_its_calendar_slot_one_line_per_year()
    {
        var report = ReportMapper.Build(October2026, History());

        foreach (var chart in new[] { report.Traffic.Organic, report.Traffic.Total })
        {
            Assert.Equal(["01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12"], chart.Labels);
            Assert.Equal(["year_2025", "year_2026"], chart.Series.Select(s => s.Key));
        }

        Assert.Equal(Months((9, 1000), (10, 1010), (11, 1020), (12, 1030)), SeriesOf(report.Traffic.Organic, "year_2025").Values);
        Assert.Equal(Months((1, 1040), (2, 1050), (3, 1060), (4, 1070), (5, 1080), (6, 1090), (7, 1100), (8, 1110), (9, 1120), (10, 1130)),
            SeriesOf(report.Traffic.Organic, "year_2026").Values);
        Assert.Equal(Months((9, 3000), (10, 3020), (11, 3040), (12, 3060)), SeriesOf(report.Traffic.Total, "year_2025").Values);
        Assert.Equal(Months((1, 3080), (2, 3100), (3, 3120), (4, 3140), (5, 3160), (6, 3180), (7, 3200), (8, 3220), (9, 3240), (10, 3260)),
            SeriesOf(report.Traffic.Total, "year_2026").Values);

        // The latest year is the emphasized one; only years older than last year are dashed.
        Assert.True(SeriesOf(report.Traffic.Organic, "year_2026").Emphasis);
        Assert.False(SeriesOf(report.Traffic.Organic, "year_2025").Emphasis);
        Assert.All(report.Traffic.Organic.Series, s => Assert.Equal("solid", s.Style));
    }

    [Fact]
    public void The_year_on_year_trend_is_the_change_against_the_same_month_last_year_in_percent()
    {
        var report = ReportMapper.Build(October2026, History());

        // 1130 against 1010 is +11.88 %; 3260 against 3020 is +7.95 %. One decimal.
        var organic = Kpi(report, "organic_sessions")!;
        Assert.Equal(1130, organic.Value);
        Assert.Equal(("up", 11.9, "vsSameMonthLastYear"), (organic.Trend!.Direction, organic.Trend.ChangePercent, organic.Trend.Text?.Key));
        Assert.Null(organic.Trend.Text!.Args);

        var total = Kpi(report, "total_sessions")!;
        Assert.Equal(3260, total.Value);
        Assert.Equal(("up", 7.9), (total.Trend!.Direction, total.Trend.ChangePercent));
    }

    [Fact]
    public void A_drop_against_last_year_points_down()
    {
        var rows = History();
        rows.RemoveAll(r => r is { Segment: Ga4Agent.SegmentOrganic, Period: "2026-10" });
        rows.Add(new Ga4Row("2026-10", Ga4Agent.SegmentOrganic, 909, EngagementRate: 0.5, AvgSessionSeconds: 70));

        var organic = Kpi(ReportMapper.Build(October2026, rows), "organic_sessions")!;

        // 909 against 1010 is -10 %.
        Assert.Equal(("down", -10.0), (organic.Trend!.Direction, organic.Trend.ChangePercent));
    }

    [Fact]
    public void The_engaged_visits_ring_is_a_real_percentage_between_0_and_100()
    {
        var report = ReportMapper.Build(October2026, History());

        var ring = Kpi(report, "organic_engagement_rate")!;
        Assert.Equal("percent", ring.Format);
        Assert.Equal(53.0, ring.Value);
        Assert.InRange(ring.Value!.Value, 0, 100);

        // Even a site where every visit is engaged stays at 100, never above.
        var rows = History();
        rows.RemoveAll(r => r is { Segment: Ga4Agent.SegmentOrganic, Period: "2026-10" });
        rows.Add(new Ga4Row("2026-10", Ga4Agent.SegmentOrganic, 1130, EngagementRate: 1.0, AvgSessionSeconds: 70));
        Assert.Equal(100.0, Kpi(ReportMapper.Build(October2026, rows), "organic_engagement_rate")!.Value);
    }

    [Fact]
    public void The_engagement_and_duration_KPIs_name_last_years_figure_as_an_argument_not_in_prose()
    {
        var report = ReportMapper.Build(October2026, History());

        // A year ago (2025-10) engagement was 0.41 and the average visit 62 seconds.
        var engagement = Kpi(report, "organic_engagement_rate")!.Trend!;
        Assert.Equal(("note", (double?)null, "engagementVsLastYear"), (engagement.Direction, engagement.ChangePercent, engagement.Text!.Key));
        AssertArgs(engagement.Text, ("percent", 41.0));

        var duration = Kpi(report, "organic_session_seconds")!;
        Assert.Equal(("duration", 86.0), (duration.Format, duration.Value));
        Assert.Equal("durationVsLastYear", duration.Trend!.Text!.Key);
        AssertArgs(duration.Trend.Text, ("seconds", 62L));
    }

    [Fact]
    public void The_traffic_KPIs_are_the_ones_Nova_emits_in_Novas_order()
    {
        var report = ReportMapper.Build(October2026, History(), Search(), brandRegex: BrandRegex);

        Assert.Equal(
            ["organic_sessions", "organic_engagement_rate", "organic_session_seconds", "organic_key_events", "total_sessions", "gsc_clicks", "clicks_vs_sessions"],
            report.Traffic.Kpis.Select(k => k.Key));
    }

    [Fact]
    public void Zero_key_events_in_every_month_is_a_warning_that_the_measurement_is_missing_not_a_KPI_of_zero()
    {
        var report = ReportMapper.Build(October2026, History(keyEvents: _ => 0));

        Assert.Null(Kpi(report, "organic_key_events"));
        var note = Assert.Single(report.Traffic.Organic.Annotations, a => a.Text.Key == "noKeyEvents");
        Assert.Equal(("warning", "2026-10"), (note.Severity, note.Period));
    }

    [Fact]
    public void Key_events_in_any_month_give_a_KPI_with_a_year_on_year_trend_and_no_note()
    {
        // 2025-10 had 3, 2026-10 has 6: +100 %.
        var report = ReportMapper.Build(October2026, History(keyEvents: i => i switch { 1 => 3, 13 => 6, _ => 0 }));

        var kpi = Kpi(report, "organic_key_events")!;
        Assert.Equal(6, kpi.Value);
        Assert.Equal(("up", 100.0), (kpi.Trend!.Direction, kpi.Trend.ChangePercent));
        Assert.DoesNotContain(report.Traffic.Organic.Annotations, a => a.Text.Key == "noKeyEvents");
    }

    // -----------------------------------------------------------------------------------------------------------------
    // A month with no data (Review Focus 2)
    // -----------------------------------------------------------------------------------------------------------------

    public static TheoryData<string> NoDataCases() => ["no rows, no Search Console", "no rows, Search Console with zero rows", "rows of zero sessions"];

    private static Report NoDataReport(string name) => name switch
    {
        "no rows, no Search Console" => ReportMapper.Build(October2026, []),
        "no rows, Search Console with zero rows" => ReportMapper.Build(October2026, [], new GscFetch([], null, "2026-09", "2025-07", []), brandRegex: BrandRegex),
        _ => ReportMapper.Build(October2026,
            [new Ga4Row("2025-10", Ga4Agent.SegmentOrganic, 0), new Ga4Row("2026-10", Ga4Agent.SegmentOrganic, 0), new Ga4Row("2026-10", Ga4Agent.SegmentTotal, 0)]),
    };

    [Theory]
    [MemberData(nameof(NoDataCases))]
    public void A_month_with_no_data_is_a_noData_report_with_no_division_by_zero_and_no_ring(string name)
    {
        var report = NoDataReport(name);

        Assert.Equal("noData", report.DataStatus);
        Assert.Equal("noData", report.DataStatusNote?.Key);

        Assert.All(report.Traffic.Kpis, k => Assert.True(k.Value is null || double.IsFinite(k.Value.Value), $"{k.Key} = {k.Value}"));
        Assert.All(report.Traffic.Kpis, k => Assert.True(k.Trend?.ChangePercent is null || double.IsFinite(k.Trend.ChangePercent.Value), k.Key));
        Assert.All(new[] { report.Traffic.Organic, report.Traffic.Total, report.Traffic.SearchComparison }.SelectMany(c => c.Series).SelectMany(s => s.Values),
            v => Assert.True(v is null || double.IsFinite(v.Value)));

        // The ring has nothing to show.
        Assert.Null(Kpi(report, "organic_engagement_rate")?.Value);
    }

    [Fact]
    public void A_month_with_data_is_live_and_carries_no_status_note()
    {
        var report = ReportMapper.Build(October2026, History());

        Assert.Equal("live", report.DataStatus);
        Assert.Null(report.DataStatusNote);
    }

    [Fact]
    public void Search_Console_data_alone_is_enough_for_a_live_report()
        => Assert.Equal("live", ReportMapper.Build(October2026, [], Search(), brandRegex: BrandRegex).DataStatus);

    // -----------------------------------------------------------------------------------------------------------------
    // Without Search Console, and without brand terms
    // -----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Without_Search_Console_the_report_has_no_search_sections_and_lists_GA4_as_its_only_source()
    {
        var report = ReportMapper.Build(October2026, History(), gsc: null, ga4Property: "properties/123456789");

        Assert.Null(report.Traffic.Search);
        Assert.Empty(report.Traffic.SearchComparison.Series);
        Assert.Equal("traffic_search_comparison", report.Traffic.SearchComparison.Id);
        Assert.DoesNotContain(report.Traffic.Kpis, k => k.Source == "gsc");

        var source = Assert.Single(report.Sources);
        Assert.Equal(("ga4", "2026-10", (TextRef?)null), (source.Id, source.LatestPeriod, source.Note));
        Assert.Equal(new Measured(null, "properties/123456789", null), report.Measured);
    }

    [Fact]
    public void Without_any_property_or_brand_pattern_there_is_nothing_to_report_as_measured()
        => Assert.Null(ReportMapper.Build(October2026, History()).Measured);

    [Fact]
    public void Without_brand_terms_nothing_that_needs_a_brand_split_is_reported()
    {
        // The same data WITH a brand pattern gives the split and its notes, so the absence below means something.
        var split = ReportMapper.Build(October2026, History(), Search(), "sc-domain:example.com", brandRegex: BrandRegex).Traffic.Search!;
        Assert.Equal(["brand", "non_brand"], split.Segments.Select(s => s.Key));
        Assert.Contains(split.Annotations, a => a.Text.Key == "anonymizedQueries");
        Assert.Contains(split.Annotations, a => a.Text.Key == "nonBrandNotClicked");

        var report = ReportMapper.Build(October2026, History(), Search(), "sc-domain:example.com", brandRegex: null);
        var search = report.Traffic.Search!;

        Assert.Empty(search.Segments);
        Assert.Empty(search.Clicks.Series);
        Assert.Empty(search.Clicks.Labels);
        Assert.Empty(search.Impressions.Series);
        Assert.Empty(search.Impressions.Labels);
        Assert.DoesNotContain(search.Annotations, a => a.Text.Key is "anonymizedQueries" or "segmentsExceedTotal" or "nonBrandNotClicked");
        Assert.Empty(search.Insights);

        // Everything that does not depend on the split is still there.
        Assert.Equal("total", search.Total?.Key);
        Assert.Contains(search.Annotations, a => a.Text.Key == "pagesNotClicked");
        Assert.Equal(3200, Kpi(report, "gsc_clicks")?.Value);
        Assert.NotEmpty(report.Traffic.SearchComparison.Series);
        Assert.Null(report.Measured!.BrandRegex);
    }

    // -----------------------------------------------------------------------------------------------------------------
    // Search Console: ported from Nova's own checks (Manadsrapport.Checks), with made-up numbers
    // -----------------------------------------------------------------------------------------------------------------

    private static GscFetch OneMonth(params GscRow[] rows) => new(rows, "2026-06-12", "2026-05", "2025-03", []);

    private static Report June(GscFetch gsc, string organicPeriod = "2026-05")
        => ReportMapper.Build(new Period(2026, 6), [new Ga4Row(organicPeriod, Ga4Agent.SegmentOrganic, 5_000)], gsc, brandRegex: BrandRegex);

    private static double? Metric(Report report, string segment, string metric) => Metric(report.Traffic.Search, segment, metric);

    private static double? Metric(SearchBlock? search, string segment, string metric)
        => search?.Segments.FirstOrDefault(s => s.Key == segment)?.Metrics.FirstOrDefault(m => m.Key == $"{segment}_{metric}")?.Value;

    [Fact]
    public void CTR_becomes_a_percentage_and_position_one_decimal_while_clicks_and_impressions_stay_whole()
    {
        var report = June(OneMonth(
            new GscRow("2026-05", GscAgent.SegmentTotal, 9_000, 300_000, 0.03, 11.7),
            new GscRow("2026-05", GscAgent.SegmentBrand, 2_000, 16_000, 0.1234567, 1.66),
            new GscRow("2026-05", GscAgent.SegmentNonBrand, 6_600, 240_000, 0.0275, 12.94)));

        Assert.Equal(12.35, Metric(report, "brand", "ctr"));
        Assert.Equal(2.75, Metric(report, "non_brand", "ctr"));
        Assert.Equal(1.7, Metric(report, "brand", "position"));
        Assert.Equal(12.9, Metric(report, "non_brand", "position"));
        Assert.Equal((2_000.0, 16_000.0), (Metric(report, "brand", "clicks"), Metric(report, "brand", "impressions")));
    }

    [Fact]
    public void A_segment_without_a_row_has_null_measures_not_zeros()
    {
        var report = June(OneMonth(new GscRow("2026-05", GscAgent.SegmentTotal, 9_000, 300_000, 0.03, 11.7)));

        Assert.Null(Metric(report, "brand", "clicks"));
        Assert.Null(Metric(report, "brand", "position"));
    }

    [Theory]
    [InlineData(10_500, "warning")] // 10 % over the total: the pattern or the property is wrong
    [InlineData(9_010, "info")]     // a handful over: Search Console rounds the three aggregates separately
    public void Brand_plus_non_brand_above_the_total_is_noted_and_only_a_large_overlap_is_a_warning(long nonBrand, string severity)
    {
        var report = June(OneMonth(
            new GscRow("2026-05", GscAgent.SegmentTotal, 12_000, 300_000, 0.04, 11.7),
            new GscRow("2026-05", GscAgent.SegmentBrand, 3_000, 16_000, 0.19, 1.7),
            new GscRow("2026-05", GscAgent.SegmentNonBrand, nonBrand, 240_000, 0.04, 12.9)));

        var note = Assert.Single(report.Traffic.Search!.Annotations, a => a.Text.Key == "segmentsExceedTotal");
        Assert.Equal(severity, note.Severity);
        Assert.Equal("2026-05", note.Text.Args!["period"]);
    }

    [Fact]
    public void Anonymized_queries_are_noted_with_the_share_brand_and_non_brand_cover()
    {
        // 2 000 + 6 600 of 9 000 clicks: 95.6 %, so 4.4 % fell out with the anonymized queries.
        var report = June(OneMonth(
            new GscRow("2026-05", GscAgent.SegmentTotal, 9_000, 300_000, 0.03, 11.7),
            new GscRow("2026-05", GscAgent.SegmentBrand, 2_000, 16_000, 0.125, 1.7),
            new GscRow("2026-05", GscAgent.SegmentNonBrand, 6_600, 240_000, 0.0275, 12.9)));

        var note = Assert.Single(report.Traffic.Search!.Annotations, a => a.Text.Key == "anonymizedQueries");
        Assert.Equal(("info", "2026-05"), (note.Severity, note.Period));
        Assert.Equal(95.6, note.Text.Args!["percent"]);
        Assert.Equal(9_000, Kpi(report, "gsc_clicks")?.Value);
    }

    [Fact]
    public void Clicks_minus_visits_is_only_reported_when_both_sources_cover_the_same_month()
    {
        var gsc = OneMonth(new GscRow("2026-05", GscAgent.SegmentTotal, 9_000, 300_000, 0.03, 11.7));

        Assert.Equal(4_000, Kpi(June(gsc), "clicks_vs_sessions")?.Value);
        Assert.Null(Kpi(June(gsc, organicPeriod: "2026-04"), "clicks_vs_sessions"));
    }

    [Fact]
    public void A_Search_Console_month_behind_the_report_is_its_own_source_with_a_note_naming_the_month()
    {
        var report = June(OneMonth(new GscRow("2026-05", GscAgent.SegmentTotal, 9_000, 300_000, 0.03, 11.7)));

        var source = Assert.Single(report.Sources, s => s.Id == "gsc");
        Assert.Equal("2026-05", source.LatestPeriod);
        Assert.Equal("gscIncomplete", source.Note?.Key);
        Assert.Equal("2026-05", source.Note!.Args!["period"]);
        Assert.Equal("2026-06-12", source.Note.Args["dataThrough"]);
    }

    [Fact]
    public void A_month_with_visits_but_no_Search_Console_rows_says_Search_Console_has_no_data_for_it_and_not_that_it_lags()
    {
        var report = June(OneMonth());

        var source = Assert.Single(report.Sources, s => s.Id == "gsc");
        Assert.Equal("gscNoData", source.Note?.Key);
        Assert.Equal("2026-06", source.Note!.Args!["period"]);
        Assert.Equal("2026-06-12", source.Note.Args["dataThrough"]);
    }

    [Fact]
    public void Search_Console_rows_only_for_later_months_are_no_data_for_the_month_either()
    {
        var report = June(OneMonth(new GscRow("2026-07", GscAgent.SegmentTotal, 9_000, 300_000, 0.03, 11.7)));

        Assert.Equal("gscNoData", Assert.Single(report.Sources, s => s.Id == "gsc").Note?.Key);
    }

    [Fact]
    public void The_visits_and_clicks_chart_only_has_years_in_which_Search_Console_has_clicks()
    {
        var rows = Enumerable.Range(2022, 5).Select(year => new Ga4Row($"{year}-05", Ga4Agent.SegmentOrganic, 7_000)).ToList();
        var gsc = OneMonth(
            new GscRow("2025-11", GscAgent.SegmentTotal, 3_000, 90_000, 0.033, 12.9),
            new GscRow("2026-05", GscAgent.SegmentTotal, 4_000, 120_000, 0.033, 11.7));

        var chart = ReportMapper.Build(new Period(2026, 6), rows, gsc, brandRegex: BrandRegex).Traffic.SearchComparison;

        Assert.Equal(["sessions_2025", "clicks_2025", "sessions_2026", "clicks_2026"], chart.Series.Select(s => s.Key));
        Assert.Equal(["01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12"], chart.Labels);
        Assert.Equal(Months((5, 7_000)), SeriesOf(chart, "sessions_2025").Values);
        Assert.Equal(Months((11, 3_000)), SeriesOf(chart, "clicks_2025").Values);
    }

    [Fact]
    public void The_brand_charts_run_on_a_rolling_axis_of_raw_periods()
    {
        var search = ReportMapper.Build(October2026, History(), Search(), brandRegex: BrandRegex).Traffic.Search!;

        var expected = Enumerable.Range(0, 13).Select(i => new Period(2025, 10).AddMonths(i).ToString()).ToList();
        Assert.Equal(expected, search.Clicks.Labels);
        Assert.Equal(expected, search.Impressions.Labels);
        Assert.Equal(["brand", "non_brand"], search.Clicks.Series.Select(s => s.Key));
        Assert.Equal(500, SeriesOf(search.Clicks, "brand").Values[0]);
        Assert.Equal(2_480, SeriesOf(search.Clicks, "non_brand").Values[12]);
    }

    [Fact]
    public void Page_findings_name_at_most_three_shortened_paths_and_carry_the_thresholds_as_arguments()
    {
        var gsc = OneMonth(
            new GscRow("2026-05", GscAgent.SegmentTotal, 9_000, 300_000, 0.03, 11.7)) with
        {
            Pages =
            [
                new GscPageRow("https://www.example.com/", 2, 5_000, 0.0004, 3.0),
                new GscPageRow("https://example.com/b", 2, 4_000, 0.0005, 4.0),
                new GscPageRow("https://shop.example.com/c", 2, 3_000, 0.0006, 5.0),
                new GscPageRow("http://example.com/d", 2, 2_000, 0.001, 6.0),
                new GscPageRow("https://example.com/deep", 2, 900, 0.002, 25.0),
                new GscPageRow("https://example.com/quiet", 0, 299, 0, 2.0),
            ],
        };

        var notes = June(gsc).Traffic.Search!.Annotations;

        var unclicked = Assert.Single(notes, a => a.Text.Key == "pagesNotClicked");
        Assert.Equal(("warning", "2026-05"), (unclicked.Severity, unclicked.Period));
        AssertArgs(unclicked.Text, ("count", 4), ("pages", "/, /b, shop.example.com/c"), ("position", 10.0), ("impressions", 300L), ("ctrPercent", 1.0));

        var deep = Assert.Single(notes, a => a.Text.Key == "pagesRankDeep");
        AssertArgs(deep.Text, ("count", 1), ("pages", "/deep"), ("impressions", 300L), ("position", 20.0));
    }

    [Fact]
    public void Non_brand_seen_widely_but_hardly_clicked_is_a_finding_with_its_figures_as_arguments()
    {
        var search = ReportMapper.Build(October2026, History(), Search(), brandRegex: BrandRegex).Traffic.Search!;

        // 2 480 clicks on 400 000 impressions is 0.62 % CTR at position 14.
        var note = Assert.Single(search.Annotations, a => a.Text.Key == "nonBrandNotClicked");
        Assert.Equal(("warning", "2026-10"), (note.Severity, note.Period));
        AssertArgs(note.Text, ("impressions", 400_000L), ("clicks", 2_480L), ("ctrPercent", 0.62), ("position", 14.0));
    }

    [Fact]
    public void A_figure_in_a_note_is_rounded_as_Nova_printed_it_half_away_from_zero()
    {
        // 1 100 + 2 910 of 4 000 clicks is exactly 100.25 %: Nova's note said 100.3 (a table rounding to even says 100.2).
        var report = June(OneMonth(
            new GscRow("2026-05", GscAgent.SegmentTotal, 4_000, 120_000, 0.033, 9.9),
            new GscRow("2026-05", GscAgent.SegmentBrand, 1_100, 9_000, 0.12, 1.9),
            new GscRow("2026-05", GscAgent.SegmentNonBrand, 2_910, 100_000, 0.029, 12.4)));

        var note = Assert.Single(report.Traffic.Search!.Annotations, a => a.Text.Key == "segmentsExceedTotal");
        Assert.Equal(100.3, note.Text.Args!["percent"]);
    }

    /// <summary>The arguments of a text, compared by name and by value including the boxed type (300L is not 300).</summary>
    private static void AssertArgs(TextRef text, params (string Name, object? Value)[] expected)
        => Assert.Equal(
            expected.OrderBy(a => a.Name, StringComparer.Ordinal),
            (text.Args ?? new Dictionary<string, object?>()).Select(a => (Name: a.Key, a.Value)).OrderBy(a => a.Name, StringComparer.Ordinal));

    [Fact]
    public void Queries_pages_and_query_page_pairs_are_mapped_in_percent_and_pairs_keep_only_archived_queries()
    {
        var gsc = OneMonth(new GscRow("2026-05", GscAgent.SegmentTotal, 9_000, 300_000, 0.03, 11.7)) with
        {
            Queries = [new GscQueryRow("acme widgets", 40, 500, 0.08, 1.44)],
            Pages = [new GscPageRow("https://shop.example.com/cart?step=two", 10, 200, 0.05, 3.06)],
            QueryPages =
            [
                new GscQueryPageRow("ACME widgets", "https://www.example.com/widgets", 30, 400, 0.075, 1.25),
                new GscQueryPageRow("not archived", "https://www.example.com/", 1, 10, 0.1, 9.0),
            ],
        };

        var search = June(gsc).Traffic.Search!;

        Assert.Equal([new SearchQuery("acme widgets", 40, 500, 8.0, 1.4)], search.Queries);
        Assert.Equal([new SearchLandingPage("https://shop.example.com/cart?step=two", "shop.example.com/cart?step=two", 10, 200, 5.0, 3.1)], search.Pages);
        Assert.Equal([new SearchQueryPage("ACME widgets", "https://www.example.com/widgets", "/widgets", 30, 400, 7.5, 1.2)], search.QueryPages);
    }

    [Fact]
    public void Empty_query_and_page_lists_are_null_so_missing_reads_differently_from_empty()
    {
        var search = June(OneMonth(new GscRow("2026-05", GscAgent.SegmentTotal, 9_000, 300_000, 0.03, 11.7)) with { Queries = [], Pages = [] })
            .Traffic.Search!;

        Assert.Null(search.Queries);
        Assert.Null(search.Pages);
        Assert.Null(search.QueryPages);
    }

    [Theory]
    [InlineData("https://www.example.com/", "/")]
    [InlineData("https://example.com", "/")]
    [InlineData("http://example.com/a/b", "/a/b")]
    [InlineData("https://shop.example.com/cart", "shop.example.com/cart")]
    [InlineData("https://shop.example.com", "shop.example.com")]
    [InlineData("https://www.example.co.uk/x", "example.co.uk/x")]
    [InlineData("ftp://example.com/x", "ftp://example.com/x")]
    public void A_page_url_is_shortened_to_its_path_on_the_main_domain_and_keeps_the_host_of_a_subdomain(string url, string expected)
        => Assert.Equal(expected, ReportMapper.Trim(url));
}
