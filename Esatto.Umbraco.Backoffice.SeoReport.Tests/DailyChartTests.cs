using System.Globalization;
using System.Text.Json;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// Tests for the SHAPE of the daily chart.
///
/// The shape is a contract with the client's chart component: it reads id, type, palette, labels and series{key,values,emphasis,
/// style}. Rename one and the chart stops drawing, and no compiler sees it, so the SERIALIZED JSON is tested and not just the
/// object. The server sends no prose: the labels are raw dates, the series are keys, and the chart's texts are written in en.ts.
/// </summary>
public class DailyChartTests
{
    private static readonly DateOnly Today = new(2026, 8, 28);
    private static readonly Ga4DailyWindow Window = Ga4DailyAgent.WindowFor(Today, null);

    private static Ga4DailySeries SeriesWith(Func<DateOnly, long> sessions)
    {
        var points = new List<Ga4DailyPoint>();
        for (var d = Window.PreviousStart; d <= Window.End; d = d.AddDays(1)) points.Add(new Ga4DailyPoint(d, sessions(d)));
        return Ga4DailyAgent.Split(points, Window);
    }

    private static JsonElement Chart(Ga4DailySeries? series) => JsonSerializer.SerializeToElement(DailyChart.Build(series));

    private static List<JsonElement> SeriesOf(JsonElement chart) => [.. chart.GetProperty("series").EnumerateArray()];

    private static List<double?> Values(JsonElement series) =>
        [.. series.GetProperty("values").EnumerateArray().Select(x => x.ValueKind == JsonValueKind.Null ? (double?)null : x.GetDouble())];

    // ---- The block the client reads -------------------------------------------------------

    [Fact]
    public void The_block_has_the_fields_the_chart_component_reads()
    {
        var chart = Chart(SeriesWith(_ => 10));

        foreach (var field in new[] { "id", "type", "palette", "height", "labels", "series", "annotations", "insights" })
        {
            Assert.True(chart.TryGetProperty(field, out _), $"the chart component reads '{field}'");
        }

        Assert.Equal(DailyChart.ChartId, chart.GetProperty("id").GetString());
        Assert.Equal("line", chart.GetProperty("type").GetString());

        // Every series is the site's own, so no series should carry the accent colour.
        Assert.Equal("segment", chart.GetProperty("palette").GetString());
    }

    [Fact]
    public void The_block_carries_no_prose()
    {
        // A title, a subtitle and a description are texts: they are looked up from the id (chart<Id>Title and so on), and a
        // series label from its key. The block has no property to hold prose in.
        var chart = Chart(SeriesWith(_ => 10));

        foreach (var field in new[] { "title", "subtitle", "description", "label" })
        {
            Assert.False(chart.TryGetProperty(field, out _), $"the block must not carry '{field}'");
        }

        Assert.All(SeriesOf(chart), series => Assert.False(series.TryGetProperty("label", out _)));
    }

    [Fact]
    public void Two_series_the_curve_and_the_dashed_baseline()
    {
        var series = SeriesOf(Chart(SeriesWith(_ => 10)));

        // The "segment" palette takes at most two series.
        Assert.Equal(2, series.Count);

        Assert.Equal(DailyChart.SeriesDaily, series[0].GetProperty("key").GetString());
        Assert.True(series[0].GetProperty("emphasis").GetBoolean());
        Assert.Equal("solid", series[0].GetProperty("style").GetString());

        // A dashed flat line reads without a legend; two wobbling curves do not.
        Assert.Equal(DailyChart.SeriesBaseline, series[1].GetProperty("key").GetString());
        Assert.False(series[1].GetProperty("emphasis").GetBoolean());
        Assert.Equal("dashed", series[1].GetProperty("style").GetString());
    }

    [Fact]
    public void The_baseline_is_flat_and_as_long_as_the_curve()
    {
        // A shorter series would stop in the middle of the chart.
        var series = SeriesOf(Chart(SeriesWith(_ => 8)));
        var daily = Values(series[0]);
        var baseline = Values(series[1]);

        Assert.Equal(daily.Count, baseline.Count);
        Assert.Single(baseline.Distinct());
        Assert.Equal(8d, baseline[0]);
    }

    [Fact]
    public void The_baseline_is_the_preceding_periods_daily_average_to_one_decimal()
    {
        // Ten days of 1 and twenty of 0 in the preceding period: 10 / 30 = 0.333.
        var series = SeriesWith(d => d >= Window.PreviousStart && d < Window.PreviousStart.AddDays(10) ? 1 : 0);

        Assert.Equal(0.3, Values(SeriesOf(Chart(series))[1])[0]);
    }

    [Fact]
    public void One_label_and_one_value_per_day_of_the_curve()
    {
        var chart = Chart(SeriesWith(_ => 3));

        Assert.Equal(Ga4DailyAgent.WindowDays, chart.GetProperty("labels").GetArrayLength());
        Assert.Equal(Ga4DailyAgent.WindowDays, SeriesOf(chart)[0].GetProperty("values").GetArrayLength());
    }

    [Fact]
    public void The_curve_is_the_sessions_of_each_day_as_raw_numbers()
    {
        var chart = Chart(SeriesWith(d => d.Day));

        Assert.Equal(
            Enumerable.Range(0, Ga4DailyAgent.WindowDays).Select(i => (double?)Window.Start.AddDays(i).Day),
            Values(SeriesOf(chart)[0]));
    }

    [Fact]
    public void The_labels_are_raw_dates_for_the_client_to_format()
    {
        // Not "24 Aug" or "24 aug.": which language and which short form is the client's business.
        var labels = Chart(SeriesWith(_ => 3)).GetProperty("labels").EnumerateArray().Select(x => x.GetString()!).ToList();

        Assert.Equal(DailyChart.IsoDate(Window.Start), labels[0]);
        Assert.Equal(DailyChart.IsoDate(Window.End), labels[^1]);
        Assert.All(labels, label => Assert.True(
            DateOnly.TryParseExact(label, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            $"'{label}' is not YYYY-MM-DD"));
        Assert.Equal(labels.Count, labels.Distinct().Count());
    }

    [Fact]
    public void A_day_without_traffic_is_a_zero_not_a_gap()
    {
        // GA4 leaves a day with no visits out altogether, and the series fills it with zero: a day without visits IS zero visits,
        // unlike a month that was never archived. A curve that skipped the day would squeeze the time axis.
        var chart = Chart(SeriesWith(d => d == Window.End ? 5 : 0));
        var daily = Values(SeriesOf(chart)[0]);

        Assert.Equal(Ga4DailyAgent.WindowDays, daily.Count);
        Assert.Equal(0d, daily[0]);
        Assert.Equal(5d, daily[^1]);
        Assert.DoesNotContain(null, daily);
    }

    // ---- No data ------------------------------------------------------------------------

    [Fact]
    public void An_empty_series_is_a_chart_without_series_which_the_client_reads_as_no_data()
    {
        // The report contract says a chart with no series has no data. That is not the same as a chart of zeros, which has series.
        var empty = new Ga4DailySeries([], Baseline: 0, Total: 0, PreviousTotal: 0, ChangePercent: null, Peak: null);

        var chart = Chart(empty);

        Assert.Equal(0, chart.GetProperty("labels").GetArrayLength());
        Assert.Equal(0, chart.GetProperty("series").GetArrayLength());
    }

    [Fact]
    public void A_missing_series_is_no_data_too_and_never_throws()
    {
        var chart = Chart(null);

        Assert.Equal(0, chart.GetProperty("labels").GetArrayLength());
        Assert.Equal(0, chart.GetProperty("series").GetArrayLength());
    }

    [Fact]
    public void A_window_of_real_zeros_is_still_a_chart_with_series()
    {
        // Thirty days with no visits is a measurement: a flat line at zero. Distinguishable from "no data" by having series.
        var chart = Chart(SeriesWith(_ => 0));
        var series = SeriesOf(chart);

        Assert.Equal(2, series.Count);
        Assert.All(Values(series[0]), v => Assert.Equal(0d, v));
        Assert.All(Values(series[1]), v => Assert.Equal(0d, v));
    }

    [Fact]
    public void A_baseline_that_is_not_a_number_is_drawn_as_a_gap_never_as_NaN_or_zero()
    {
        // No usable comparison is a gap in the dashed line, like a month without data anywhere else in the report.
        foreach (var baseline in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var series = new Ga4DailySeries(
                [new Ga4DailyPoint(Window.Start, 4), new Ga4DailyPoint(Window.Start.AddDays(1), 6)],
                baseline, Total: 10, PreviousTotal: 0, ChangePercent: null, Peak: null);

            var chart = Chart(series);
            var values = SeriesOf(chart);

            Assert.Equal([4d, 6d], Values(values[0]));
            Assert.Equal([null, null], Values(values[1]));
            Assert.DoesNotContain("NaN", chart.GetRawText());
            Assert.DoesNotContain("Infinity", chart.GetRawText());
        }
    }

    // ---- The arguments of the chart's texts --------------------------------------------------

    private static IReadOnlyDictionary<string, object?> ArgsFor(DateOnly today, string? month)
        => DailyChart.TextArgs(Ga4DailyAgent.WindowFor(today, month), today);

    [Fact]
    public void The_texts_in_the_rolling_mode_get_the_window_length_and_no_month()
    {
        var args = DailyChart.TextArgs(Window, Today);

        Assert.Equal(["period", "provisionalDays", "windowDays"], args.Keys.Order(StringComparer.Ordinal));
        Assert.Null(args["period"]);
        Assert.Equal(Ga4DailyAgent.WindowDays, Assert.IsType<int>(args["windowDays"]));
        Assert.Equal(Ga4DailyAgent.ProvisionalDays, Assert.IsType<int>(args["provisionalDays"]));
    }

    [Fact]
    public void The_texts_in_the_month_mode_get_the_month()
    {
        var args = ArgsFor(Today, "2026-06");

        Assert.Equal("2026-06", Assert.IsType<string>(args["period"]));
        Assert.Equal(30, args["windowDays"]); // June has thirty days, which is also the rolling window's length: the number is the month's own
    }

    [Fact]
    public void A_missing_window_gives_the_rolling_arguments()
    {
        var args = DailyChart.TextArgs(null, Today);

        Assert.Null(args["period"]);
        Assert.Equal(Ga4DailyAgent.WindowDays, args["windowDays"]);
        Assert.Equal(Ga4DailyAgent.ProvisionalDays, args["provisionalDays"]);
    }

    [Theory]
    [InlineData("2026-07", 31)]
    [InlineData("2026-06", 30)]
    [InlineData("2026-02", 28)]
    [InlineData("2024-02", 29)]
    [InlineData("2025-12", 31)]
    public void A_calendar_month_says_how_many_days_it_really_has(string month, int days)
    {
        var args = ArgsFor(Today, month);

        Assert.Equal(days, args["windowDays"]);
        Assert.Equal(days, DailyChart.DaysIn(Ga4DailyAgent.WindowFor(Today, month)));
    }

    [Fact]
    public void A_month_that_is_still_running_counts_the_days_shown_so_far()
    {
        // The window ends yesterday: on the 28th the running month shows 27 days.
        var args = ArgsFor(Today, "2026-08");

        Assert.Equal(27, args["windowDays"]);
        Assert.Equal(Ga4DailyAgent.ProvisionalDays, args["provisionalDays"]);
    }

    [Fact]
    public void On_the_first_of_the_month_the_running_month_has_no_days_yet()
    {
        var args = ArgsFor(new DateOnly(2026, 9, 1), "2026-09");

        Assert.Equal(0, args["windowDays"]); // nothing to draw, and the number does not pretend otherwise
        Assert.Equal(Ga4DailyAgent.ProvisionalDays, args["provisionalDays"]); // a running month carries the margin, as the rolling window does
    }

    [Fact]
    public void The_rolling_window_keeps_its_length_and_its_margin_whatever_the_day()
    {
        foreach (var today in new[] { new DateOnly(2026, 8, 28), new DateOnly(2026, 3, 1), new DateOnly(2024, 3, 1) })
        {
            var args = ArgsFor(today, null);

            Assert.Equal(Ga4DailyAgent.WindowDays, args["windowDays"]);
            Assert.Equal(Ga4DailyAgent.ProvisionalDays, args["provisionalDays"]);
        }
    }

    [Theory]
    [InlineData("2026-08-01", 2)]    // the month ended yesterday: its last two days are inside the margin
    [InlineData("2026-08-02", 1)]    // two days ago: only its last day is, and the text must say one day and not two
    [InlineData("2026-08-03", null)] // three days ago: older than the margin, the month is closed for good
    [InlineData("2026-08-28", null)]
    [InlineData("2027-01-15", null)]
    public void A_month_counts_the_days_it_shows_that_are_inside_the_margin_and_none_once_it_is_closed(string todayText, int? provisional)
    {
        var args = ArgsFor(DateOnly.ParseExact(todayText, "yyyy-MM-dd"), "2026-07");

        Assert.Equal(provisional, args["provisionalDays"]);
        Assert.True(args.ContainsKey("provisionalDays")); // null is "none", and it is still an argument: the key is there
    }

    [Fact]
    public void The_count_is_never_more_than_the_margin_and_never_below_none()
    {
        // Whatever the day, a finished month gives 2, 1 or nothing: never a negative number and never more than the margin.
        for (var today = new DateOnly(2026, 8, 1); today < new DateOnly(2026, 8, 12); today = today.AddDays(1))
        {
            var count = (int?)ArgsFor(today, "2026-07")["provisionalDays"];

            Assert.True(count is null or 1 or 2, $"{today}: {count}");
        }
    }

    // ---- The picker ----------------------------------------------------------------------

    [Fact]
    public void The_picker_starts_with_the_rolling_window_then_twelve_months_newest_first()
    {
        // An empty value means rolling: it is how the default is written all the way down to Ga4DailyAgent.WindowFor.
        var options = DailyChart.MonthOptions(new DateOnly(2026, 8, 28));

        Assert.Equal(13, options.Count);
        Assert.Equal("", options[0]);
        Assert.Equal("2026-08", options[1]);
        Assert.Equal("2025-09", options[^1]);
    }

    [Fact]
    public void The_running_month_is_an_option_and_the_year_boundary_is_crossed()
    {
        var options = DailyChart.MonthOptions(new DateOnly(2026, 1, 3));

        Assert.Equal("2026-01", options[1]);
        Assert.Equal("2025-12", options[2]);
        Assert.Equal("2025-02", options[^1]);
        Assert.All(options.Skip(1), o => Assert.True(Period.TryParse(o, out _), $"'{o}' is not YYYY-MM"));
    }

    [Fact]
    public void Every_picker_value_gives_the_window_it_names()
    {
        // The picker and the window agree: the empty value is the rolling window, a month is that month.
        foreach (var option in DailyChart.MonthOptions(Today))
        {
            var window = Ga4DailyAgent.WindowFor(Today, option);

            Assert.Equal(option == "" ? null : option, window.Month);
        }
    }

    // ---- Dates ---------------------------------------------------------------------------

    [Fact]
    public void A_date_is_written_year_month_day_whatever_the_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            Assert.Equal("2026-05-03", DailyChart.IsoDate(new DateOnly(2026, 5, 3)));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
