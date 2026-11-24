namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

// -------------------------------------------------------------------------------------------------------------------------
// YEAR ON YEAR, AND A SANITY CHECK ON THE BASELINE
//
// Working out the change is trivial. What is not, and only showed once the processing layer actually ran, is what happens
// when the comparison month is broken: a cookie banner that knocked out tracking for a month leaves a baseline a fraction of
// its neighbours, and a straight comparison then renders a four-digit rise as a green arrow, a triumphant figure that only
// measures the outage.
// -------------------------------------------------------------------------------------------------------------------------
public static partial class ReportMapper
{
    /// <summary>
    /// How far below its own surroundings the comparison month may lie before it no longer counts as a baseline: under a quarter
    /// of the median. Deliberately blunt and conservative: it is meant to catch "tracking was down", not seasonal variation. A
    /// judgement about the domain, not a technical constant.
    /// </summary>
    private const double AnomalousBaselineRatio = 0.25;

    /// <summary>The fewest months around the baseline needed to say anything about what is normal.</summary>
    private const int BaselineWindowMinimum = 6;

    /// <summary>
    /// The year-on-year trend for a period, plus a deterministic note when the baseline cannot be trusted. Three outcomes, all
    /// explicit:
    ///   * no baseline           -> "note" without a figure. Not null: an empty trend line cannot be told from "no change".
    ///   * an anomalous baseline -> "note" WITH the figure, plus a warning on the baseline month. The figure is never hidden,
    ///                              but it must not look like a win.
    ///   * a sound baseline      -> "up"/"down" as usual.
    /// </summary>
    /// <param name="byPeriod">The values per <c>YYYY-MM</c>.</param>
    /// <param name="period">The month to compare.</param>
    /// <param name="unit">What the values count (<see cref="UnitVisits"/>, <see cref="UnitKeyEvents"/>, <see cref="UnitClicks"/>), for the note.</param>
    internal static (Trend? Trend, Annotation? Annotation) YoyTrend(
        IReadOnlyDictionary<string, long> byPeriod,
        string period,
        string unit)
    {
        if (!byPeriod.TryGetValue(period, out var current)) return (null, null);

        var baseline = ShiftYearBack(period);

        if (!byPeriod.TryGetValue(baseline, out var prior) || prior == 0)
        {
            return (new Trend(Note, null, Text(NoComparison, ("period", baseline))), null);
        }

        var change = ChangePercent(current, prior);

        if (!IsAnomalousBaseline(byPeriod, baseline))
        {
            return (new Trend(change >= 0 ? Up : Down, change, Text(VsSameMonthLastYear)), null);
        }

        return (
            new Trend(Note, change, Text(AnomalousBaseline, ("period", baseline), ("value", prior), ("unit", unit))),
            new Annotation(baseline, Warning, Text(AnomalousBaseline,
                ("period", baseline), ("value", prior), ("median", MedianAround(byPeriod, baseline)), ("unit", unit))));
    }

    /// <summary>The change from <paramref name="prior"/> to <paramref name="current"/> in percent, one decimal. The prior is never 0 here.</summary>
    private static double ChangePercent(long current, long prior) => Math.Round(((double)current - prior) / prior * 100.0, 1);

    /// <summary>
    /// Is the comparison month an obvious measurement failure? Compared with the median of the 13 months centred on it. The
    /// median, not the mean: a mean is dragged down by the very outage this looks for.
    /// </summary>
    private static bool IsAnomalousBaseline(IReadOnlyDictionary<string, long> byPeriod, string baseline)
    {
        if (!byPeriod.TryGetValue(baseline, out var value)) return false;

        var median = MedianAround(byPeriod, baseline);
        return median > 0 && value < median * AnomalousBaselineRatio;
    }

    /// <summary>
    /// The median of the months with data among the 13 centred on <paramref name="period"/>; 0 when fewer than
    /// <see cref="BaselineWindowMinimum"/> have data, which is too little history to say what is normal.
    /// </summary>
    private static long MedianAround(IReadOnlyDictionary<string, long> byPeriod, string period)
    {
        var window = Enumerable.Range(-6, 13)
            .Select(offset => ShiftMonths(period, offset))
            .Where(byPeriod.ContainsKey)
            .Select(p => byPeriod[p])
            .Order()
            .ToList();

        return window.Count < BaselineWindowMinimum ? 0 : window[window.Count / 2];
    }

    private static string ShiftYearBack(string period) => ShiftMonths(period, -12);

    /// <summary>A <c>YYYY-MM</c> moved by whole months; anything that is not a period comes back unchanged.</summary>
    private static string ShiftMonths(string period, int offset)
        => Period.TryParse(period, out var parsed) ? parsed.AddMonths(offset).ToString() : period;
}
