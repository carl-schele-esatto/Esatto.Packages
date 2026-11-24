using System.Globalization;
using Esatto.Umbraco.Backoffice.SeoReport.Google;

namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

/// <summary>
/// Turns the daily curve into a <see cref="ChartBlock"/>: the same shape as the report's other charts, so the client draws it
/// with the same chart component and it looks like everything else in the dashboard.
///
/// A CHARTBLOCK, SO THE CLIENT NEEDS NO SECOND CHART SHAPE. The block is only reused as the shape the chart component reads
/// (id, type, palette, height, labels, series{key,values,emphasis,style}); it is not part of any report. The daily curve is
/// rolling data that is revised afterwards, while a report describes an archived month that never changes, and the two must
/// not be mixed up.
///
/// THE SERVER SENDS NO PROSE. The labels are raw dates (<c>YYYY-MM-DD</c>, one per day, which the client formats), the values
/// raw numbers, and every text is looked up by where it sits (see <see cref="ReportKeys"/>):
///   ChartBlock.Id (<see cref="ChartId"/>)   area chart, parts Title, Subtitle, Description, written with the arguments of
///                                            <see cref="TextArgs"/> (the endpoint sends them next to the chart)
///   Series.Key (daily, baseline)            area series, part Label, no arguments
///
/// THE COMPARISON AS A FLAT SERIES. GA4's own card draws the preceding period as a second wobbling curve, and two lines
/// crossing each other force the reader to the legend every time. A DASHED, FLAT line at the preceding period's daily average
/// answers the same question (is the day above or below the normal one?) without anything needing to be interpreted.
///
/// NO DATA IS NOT ZERO. A day without sessions is a zero (GA4 leaves such a day out and <see cref="Ga4DailyAgent.Split"/> fills
/// it in: a day without visits IS zero visits, unlike a month that was never archived), so the curve has no gaps and every
/// value is a real figure. A series without any day gives a chart without series, which the report contract defines as "no
/// data": the client says so instead of drawing it, and it is told apart from a chart of zeros, which has series.
/// </summary>
public static class DailyChart
{
    /// <summary>The chart's id; the client keys its lookup of the daily chart on it.</summary>
    public const string ChartId = "daily_traffic";

    /// <summary>The curve: sessions per day.</summary>
    public const string SeriesDaily = "daily";

    /// <summary>The dashed comparison: the preceding period's average sessions per day.</summary>
    public const string SeriesBaseline = "baseline";

    /// <summary>
    /// The picker's value for the rolling window. Empty means "no month", the one way the default is written all the way down
    /// to <see cref="Ga4DailyAgent.WindowFor"/>.
    /// </summary>
    public const string Rolling = "";

    /// <summary>How many calendar months back the picker offers.</summary>
    private const int SelectableMonths = 12;

    private const int ChartHeight = 240;

    /// <summary>
    /// The chart for a curve. A missing series is the same as one without days: a chart without series.
    /// </summary>
    public static ChartBlock Build(Ga4DailySeries? series)
    {
        var points = series?.Points ?? [];

        // No days, no series: "no data" in the report contract's own words, and not a chart of zeros.
        if (points.Count == 0) return Block([], []);

        // The same level for every day, and as many values as the curve, or the line would stop in the middle. A baseline that
        // is not a number is no baseline: a gap, never NaN and never a zero that claims "no traffic before".
        var level = series is { Baseline: var baseline } && double.IsFinite(baseline) ? Math.Round(baseline, 1) : (double?)null;

        return Block(
            labels: [.. points.Select(x => IsoDate(x.Date))],
            series:
            [
                new Series(SeriesDaily, [.. points.Select(x => (double?)x.Sessions)], Emphasis: true, Style: "solid"),
                new Series(SeriesBaseline, [.. points.Select(_ => level)], Emphasis: false, Style: "dashed"),
            ]);
    }

    /// <summary>
    /// The arguments the chart's title, subtitle and description are written with. They are sent beside the chart because a
    /// chart block has nowhere to carry them, and the same names are sent whatever the mode: an argument that has nothing to say
    /// is null and is still there.
    ///
    /// period (<c>YYYY-MM</c>, or null in the rolling mode); windowDays (how many days the window really has: the days of the
    /// calendar month, the days of a running month so far, the rolling window's length: see <see cref="DaysIn"/>); and
    /// provisionalDays (how many of the last days shown GA4 may still revise, or null when none of the days shown is among them).
    /// The numbers come from here so the text cannot drift from the data it describes.
    ///
    /// THE PROVISIONAL DAYS ARE COUNTED, NOT ASSUMED. A day is provisional when it is no older than
    /// <see cref="Ga4DailyAgent.ProvisionalDays"/> days. The rolling window ends yesterday, and so does a running month, so both
    /// always show the whole margin. A finished month shows only the part of it that reaches back into the month: one that ended
    /// yesterday shows the whole margin, one that ended two days ago shows one provisional day (its last), and once its last day is
    /// older than the margin nothing in it can still change, and the text must not say that it can (null).
    /// </summary>
    /// <param name="window">The window the chart shows. Null counts as the rolling window.</param>
    /// <param name="today">Today's date, which the margin is counted back from. Passed in so the arguments can be tested.</param>
    public static Dictionary<string, object?> TextArgs(Ga4DailyWindow? window, DateOnly today) =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["period"] = window?.Month,
            ["windowDays"] = DaysIn(window),
            ["provisionalDays"] = ProvisionalDaysShown(window, today),
        };

    /// <summary>
    /// How many days the window shows: its last day minus its first, plus one. For the rolling window that is
    /// <see cref="Ga4DailyAgent.WindowDays"/>; for a month it is the month's own length (28 to 31), or the days so far for the
    /// running one, which ends yesterday (none on its first day). Null is the rolling window.
    /// </summary>
    public static int DaysIn(Ga4DailyWindow? window)
        => window is null ? Ga4DailyAgent.WindowDays : Math.Max(0, window.End.DayNumber - window.Start.DayNumber + 1);

    // How many of the days shown are inside the margin: from today - margin to the last day shown, at most the margin, none (null) for
    // a calendar month whose last shown day is older than that. The rolling window (and a missing window) always ends yesterday.
    private static int? ProvisionalDaysShown(Ga4DailyWindow? window, DateOnly today)
    {
        const int margin = Ga4DailyAgent.ProvisionalDays;

        if (window is not { Month: not null }) return margin;

        var inside = Math.Clamp(window.End.DayNumber - today.AddDays(-margin).DayNumber + 1, 0, margin);
        return inside == 0 ? null : inside;
    }

    /// <summary>
    /// The picker's contents as values: the rolling window first, then calendar months back, newest first, as <c>YYYY-MM</c>.
    /// The client words each one (the rolling window as "the last 30 days", a month in the user's language).
    ///
    /// The ROLLING window comes first and is the default: the most common question is "what happened lately", not "what did
    /// March look like". The months are there to go back to a campaign that has already run.
    ///
    /// The running month is included. It is ongoing, and <see cref="Ga4DailyAgent.WindowFor"/> clips it at yesterday.
    /// </summary>
    public static IReadOnlyList<string> MonthOptions(DateOnly today) =>
    [
        Rolling,
        .. Enumerable.Range(0, SelectableMonths).Select(i => Period.FromDate(today).AddMonths(-i).ToString()),
    ];

    /// <summary>
    /// The earliest day the picker's months reach: the first day of the OLDEST month <see cref="MonthOptions"/> offers. A day before
    /// it is in no month the person can choose, and so is not one the chart can show.
    /// </summary>
    public static DateOnly EarliestDay(DateOnly today)
    {
        var oldest = Period.Parse(MonthOptions(today)[^1]);
        return new DateOnly(oldest.Year, oldest.Month, 1);
    }

    /// <summary>A date as <c>YYYY-MM-DD</c>, whatever the culture: the wire form of a day.</summary>
    public static string IsoDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static ChartBlock Block(List<string> labels, List<Series> series) =>
        new(
            Id: ChartId,
            Type: "line",

            // "segment" = identity where ALL series are the site's own and none should carry the accent alone. Exactly this
            // case, and it takes at most two series.
            Palette: "segment",
            Height: ChartHeight,
            Labels: labels,
            Series: series,
            Annotations: [],
            Insights: []);
}
