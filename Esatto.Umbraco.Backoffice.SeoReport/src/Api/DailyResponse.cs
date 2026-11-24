using Esatto.Umbraco.Backoffice.SeoReport.Reporting;

namespace Esatto.Umbraco.Backoffice.SeoReport.Api;

// The answers of GET daily and GET daily/sources. camelCase, like every response here. No labels: a day is "YYYY-MM-DD", a month
// "YYYY-MM", a number is a number, and the client words and formats all of them.

/// <summary>
/// The day-fresh curve for the rolling window or a calendar month, in the shape the client's chart component reads, with what the
/// client needs around it.
/// </summary>
/// <param name="Chart">The curve as a chart block (<see cref="DailyChart.Build"/>): the dates as labels, the sessions and the dashed comparison as series. Without series when the window has no days.</param>
/// <param name="TextArgs">The arguments the chart's texts are written with (<see cref="DailyChart.TextArgs"/>); a chart block has no place for them. <c>period</c>, <c>windowDays</c>, <c>provisionalDays</c>, the last one null when none of the days shown can still be revised.</param>
/// <param name="Months">What the picker offers: the empty string (the rolling window, the default) and then the last twelve months, <c>YYYY-MM</c>, newest first.</param>
/// <param name="Window">The days the curve shows.</param>
/// <param name="Total">Sessions in the window.</param>
/// <param name="ChangePercent">The change against the preceding period of the same length in percent, one decimal; null when that period had none.</param>
/// <param name="Peak">The day with the most sessions; null when the window has none.</param>
/// <param name="Days">One entry per day of the window, for the day picker under the chart; the same days and numbers as the chart's.</param>
/// <param name="WindowDays">How many days the window has (0 for a running month on its first day); also in <paramref name="TextArgs"/>.</param>
public sealed record DailyResponse(
    ChartBlock Chart,
    Dictionary<string, object?> TextArgs,
    string[] Months,
    DailyWindowResponse Window,
    long Total,
    double? ChangePercent,
    DailyPeakResponse? Peak,
    DailyDayResponse[] Days,
    int WindowDays);

/// <summary>The first and last day of the curve, <c>YYYY-MM-DD</c> (the last is yesterday at the latest), and the month, <c>YYYY-MM</c>, or null for the rolling window.</summary>
public sealed record DailyWindowResponse(string Start, string End, string? Month);

/// <summary>The busiest day of the window.</summary>
public sealed record DailyPeakResponse(string Date, long Sessions);

/// <summary>One day of the window.</summary>
public sealed record DailyDayResponse(string Date, long Sessions);

/// <summary>Where one day's sessions came from, as GA4 names them, most sessions first.</summary>
/// <param name="Date">The day, <c>YYYY-MM-DD</c>, written from the parsed value and never as it was typed.</param>
/// <param name="Sources">The rows; empty when the day had no sessions.</param>
public sealed record DailySourcesResponse(string Date, DailySourceRow[] Sources);

/// <summary>One source of a day: GA4's channel group, the source within it, and the sessions it brought. Names are data from Google, shown as text.</summary>
public sealed record DailySourceRow(string Channel, string Source, long Sessions);
