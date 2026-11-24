using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The year-on-year rule and its baseline check, ported from Nova's own checks (Manadsrapport.Checks) with made-up numbers.
/// Working out a percentage is trivial; what is not is a comparison month that is broken (a tracking outage), where a straight
/// comparison renders a triumphant green arrow that only measures the outage.
/// </summary>
public partial class ReportMapperTests
{
    private static Dictionary<string, long> Points(params (string Period, long Value)[] points)
        => points.ToDictionary(p => p.Period, p => p.Value, StringComparer.Ordinal);

    /// <summary>Twelve level months of 2025, so the median rule has something to compare with.</summary>
    private static Dictionary<string, long> LevelYear(long level)
        => Enumerable.Range(1, 12).ToDictionary(m => $"2025-{m:D2}", _ => level, StringComparer.Ordinal);

    [Fact]
    public void A_normal_baseline_gives_up_or_down_with_one_decimal_and_no_note()
    {
        var series = LevelYear(2_000);
        series["2026-05"] = 2_400;

        var (up, annotation) = ReportMapper.YoyTrend(series, "2026-05", ReportMapper.UnitVisits);
        Assert.Equal(("up", 20.0, "vsSameMonthLastYear"), (up!.Direction, up.ChangePercent, up.Text!.Key));
        Assert.Null(annotation);

        series["2026-05"] = 1_600;
        var down = ReportMapper.YoyTrend(series, "2026-05", ReportMapper.UnitVisits).Trend;
        Assert.Equal(("down", -20.0), (down!.Direction, down.ChangePercent));
    }

    [Fact]
    public void A_missing_baseline_is_a_note_naming_the_month_never_a_blank_trend()
    {
        var (trend, annotation) = ReportMapper.YoyTrend(Points(("2026-05", 1_234)), "2026-05", ReportMapper.UnitVisits);

        Assert.Equal(("note", (double?)null, "noComparison"), (trend!.Direction, trend.ChangePercent, trend.Text!.Key));
        AssertArgs(trend.Text, ("period", "2025-05"));
        Assert.Null(annotation);
    }

    [Fact]
    public void A_baseline_of_zero_is_a_note_not_a_division_by_zero()
    {
        var trend = ReportMapper.YoyTrend(Points(("2025-05", 0), ("2026-05", 1_234)), "2026-05", ReportMapper.UnitVisits).Trend;

        Assert.Equal(("note", (double?)null, "noComparison"), (trend!.Direction, trend.ChangePercent, trend.Text!.Key));
    }

    [Fact]
    public void A_missing_current_month_gives_no_trend_at_all()
        => Assert.Equal(((Trend?)null, (Annotation?)null), ReportMapper.YoyTrend(Points(("2025-05", 500)), "2026-05", ReportMapper.UnitVisits));

    [Fact]
    public void An_anomalous_baseline_keeps_the_number_but_is_a_note_with_a_warning_on_the_baseline_month()
    {
        // 2025-05 has 100 against about 4 000 in the months around it: an outage, not a season.
        var series = Points(
            ("2024-11", 4_000), ("2024-12", 3_900), ("2025-01", 4_100), ("2025-02", 4_050), ("2025-03", 3_950), ("2025-04", 4_020),
            ("2025-05", 100), ("2025-06", 3_980), ("2025-07", 4_010), ("2025-08", 3_990), ("2025-09", 4_030), ("2025-10", 3_970),
            ("2025-11", 4_060), ("2026-05", 4_200));

        var (trend, annotation) = ReportMapper.YoyTrend(series, "2026-05", ReportMapper.UnitVisits);

        // (4 200 - 100) / 100 = +4 100 %: shown, but not as an arrow.
        Assert.Equal(("note", 4100.0, "anomalousBaseline"), (trend!.Direction, trend.ChangePercent, trend.Text!.Key));
        AssertArgs(trend.Text, ("period", "2025-05"), ("value", 100L), ("unit", "visits"));

        Assert.NotNull(annotation);
        Assert.Equal(("warning", "2025-05", "anomalousBaseline"), (annotation.Severity, annotation.Period, annotation.Text.Key));
        AssertArgs(annotation.Text, ("period", "2025-05"), ("value", 100L), ("median", 4_000L), ("unit", "visits"));
    }

    [Fact]
    public void A_baseline_that_is_merely_low_is_not_flagged()
    {
        // 40 % of the median is a season, not an outage: the rule only fires below a quarter.
        var series = LevelYear(5_000);
        series["2025-05"] = 2_000;
        series["2026-05"] = 4_500;

        var (trend, annotation) = ReportMapper.YoyTrend(series, "2026-05", ReportMapper.UnitVisits);

        Assert.Equal("up", trend!.Direction);
        Assert.Null(annotation);
    }

    [Fact]
    public void Too_little_history_says_nothing_about_what_is_normal()
    {
        var (trend, annotation) = ReportMapper.YoyTrend(Points(("2025-05", 10), ("2025-06", 3_000), ("2026-05", 3_100)), "2026-05", ReportMapper.UnitVisits);

        Assert.Equal("up", trend!.Direction);
        Assert.Null(annotation);
    }

    [Fact]
    public void An_anomalous_organic_baseline_puts_its_warning_on_the_organic_chart()
    {
        var rows = History();
        rows.RemoveAll(r => r is { Segment: Ga4Agent.SegmentOrganic, Period: "2025-10" });
        rows.Add(new Ga4Row("2025-10", Ga4Agent.SegmentOrganic, 10));

        var report = ReportMapper.Build(October2026, rows);

        var note = Assert.Single(report.Traffic.Organic.Annotations, a => a.Text.Key == "anomalousBaseline");
        Assert.Equal(("warning", "2025-10"), (note.Severity, note.Period));
        Assert.Equal("note", Kpi(report, "organic_sessions")!.Trend!.Direction);
    }
}
