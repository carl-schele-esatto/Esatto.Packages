namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

/// <summary>A column of the grid. The period is "YYYY-MM"; the client formats it.</summary>
public record HistoryMonth(string Period);

/// <summary>A row: one target query and its position per month. Null means no match that month.</summary>
public record HistoryRow(string Term, double?[] Positions);

/// <summary>The grid: columns in chronological order, rows in the editor's order.</summary>
public record TargetGrid(HistoryMonth[] Months, HistoryRow[] Rows);

/// <summary>
/// The target queries' positions month by month: the counterpart of the position table in a hand-written monthly report.
///
/// THE FIXED BASKET IS THE WHOLE POINT. A visibility table built on each month's most shown queries means different words in
/// different months, and so mixes "our words moved" with "the basket changed". Here the basket is chosen by a person and
/// changes only when that person changes it.
///
/// A RUNNING SERIES AND NOT A YEAR-ON-YEAR COMPARISON, on purpose. Traffic is seasonal and has to be compared month against the
/// same month a year earlier; a POSITION is not seasonal in the same way. It is a verdict from Google and reads as a curve.
/// </summary>
public static class TargetHistory
{
    /// <summary>
    /// Null when there is nothing to show: no terms, or no month with search queries. An empty table is worse than no table.
    /// </summary>
    public static TargetGrid? Grid(IReadOnlyList<string> terms, IReadOnlyList<Report> reports)
    {
        if (terms is not { Count: > 0 }) return null;

        // Months without search queries are left out ENTIRELY: a column where every cell is empty says nothing about the
        // terms, only about the archive.
        var months = (reports ?? [])
            .Where(x => x.Traffic?.Search?.Queries is { Count: > 0 })
            .OrderBy(x => x.ReportPeriod, StringComparer.Ordinal)
            .ToList();

        if (months.Count == 0) return null;

        return new TargetGrid(
            Months: [.. months.Select(x => new HistoryMonth(x.ReportPeriod))],
            Rows: [.. terms.Select(term => new HistoryRow(
                Term: term,
                Positions: [.. months.Select(m => TargetAnalysis.PositionOf(term, m.Traffic?.Search?.Queries))]))]);
    }
}
