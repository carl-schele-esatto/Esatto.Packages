namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

/// <summary>
/// One month's search queries in the top three, and how many were examined.
///
/// ONLY THE TOP THREE AND THE TOTAL. The record once carried four groups (top 3, 4-10, 11-20, 21+) for a table that showed the
/// whole distribution. The table was removed (it presented the row limit as if it were a measurement), which left the other
/// three groups computed and never read. Should the distribution be needed again, it is three Count calls to put back.
/// </summary>
public record VisibilityMonth(
    // "YYYY-MM"; the client formats it.
    string Period,
    int Top3,
    int Total);

/// <summary>
/// Visibility at large: how many of the month's most shown search queries rank in the top three.
///
/// THE QUESTION IS IN NO OTHER FIGURE WE SHOW. Clicks and impressions can stand still while rankings shift: ten queries going
/// from page two to page one are an improvement that no total reveals.
///
/// ONLY MONTHS WITH SEARCH QUERIES COUNT. A month archived before search queries were fetched knows nothing about rankings,
/// and a zero would read as "no search terms at all", a claim we cannot make. It is left out instead.
///
/// THE BASIS IS THE TOP-N QUERIES, not all of them. The figure is therefore relative: it shows how our MOST SHOWN terms move,
/// not the whole tail. That is Search Console's own limitation, and it is both the ceiling and the floor of what can be said.
/// So it is reported as a SHARE in the narrative and never as a count on its own.
/// </summary>
public static class Visibility
{
    public static IReadOnlyList<VisibilityMonth> PerMonth(IReadOnlyList<Report> reports)
    {
        var months = new List<VisibilityMonth>();

        foreach (var report in (reports ?? []).OrderBy(x => x.ReportPeriod, StringComparer.Ordinal))
        {
            if (report.Traffic?.Search?.Queries is not { Count: > 0 } queries) continue;

            // Position 0 means "no value" in the Search Console mapping, not the best possible position.
            var ranked = queries.Where(x => x.Position > 0).ToList();
            if (ranked.Count == 0) continue;

            months.Add(new VisibilityMonth(
                Period: report.ReportPeriod,

                // The limit is inclusive upwards: position 3.0 is top three.
                Top3: ranked.Count(x => x.Position <= 3),
                Total: ranked.Count));
        }

        return months;
    }
}
