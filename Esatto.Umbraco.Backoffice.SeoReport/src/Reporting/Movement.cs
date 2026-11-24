using System.Globalization;

namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

/// <summary>A month a page's movement is measured against. The period is "YYYY-MM"; the client formats it for the user's language.</summary>
public record Comparison(string Period);

/// <summary>One page's change against ONE comparison month.</summary>
public record MoveDelta(
    // Impressions in the comparison month. Zero when the page is new.
    long Before,

    // The difference from now. Negative is a loss.
    long Delta,

    // The change in percent. Null when the page is new: division by zero.
    double? ChangePercent,

    // The position in the comparison month.
    double? PositionBefore);

/// <summary>
/// One page's movement, against BOTH comparisons.
///
/// Null in one of them means that month is not archived with a page list, not that the page was unchanged.
/// </summary>
public record Move(
    string Path,
    string Url,
    long Impressions,
    double? Position,
    MoveDelta? VsPrevious,
    MoveDelta? VsYearAgo);

/// <summary>Which pages gained and lost the most, and against what.</summary>
public record MovementResult(
    string Period,

    // The nearest earlier archived month with a page list. Null when there is none.
    Comparison? Previous,

    // The same month a year earlier. Null when it is not archived.
    Comparison? YearAgo,

    // Which comparison the lists are SORTED on: Movement.RankYearAgo or Movement.RankPrevious. The client has to say so, or
    // the reader cannot tell why the rows are in the order they are.
    string RankedBy,

    Move[] Losses,
    Move[] Gains);

/// <summary>
/// Biggest movement: the pages that gained and lost the most impressions, against the previous month AND against the same
/// month a year earlier.
///
/// BOTH COMPARISONS, ALWAYS. The previous month answers "what happened since last time", the year month answers "are we above
/// or below last year", and they can point in different directions: a page can have recovered since last month and still sit
/// far below last year.
///
/// THE ORDER IS SET BY THE PREVIOUS MONTH, so the list means the same thing every month. The year comparison stays on the row
/// as a check.
///
/// An earlier version carried ONE comparison that changed basis depending on what the archive happened to hold: July 2026 was
/// compared with July 2025 while June 2026 was compared with May 2026. The heading therefore meant different things in
/// different months, and the figures could not be compared between months. That was worse than either choice on its own.
///
/// IMPRESSIONS, NOT CLICKS, are the measure. A drop in impressions is a drop in VISIBILITY, which comes first; the clicks
/// follow. Sorted on clicks, pages that are shown a lot and clicked little would never appear, and they are both the most
/// common and the easiest to fix.
/// </summary>
public static class Movement
{
    /// <summary>The list is sorted on the previous month. The normal case.</summary>
    public const string RankPrevious = "previous";

    /// <summary>
    /// The list is sorted on the year comparison. Used ONLY when the previous month is missing from the archive, which in
    /// practice holds only for the archive's oldest month.
    /// </summary>
    public const string RankYearAgo = "yearAgo";

    /// <param name="reports">The archive's reports, in any order.</param>
    /// <param name="period">The month under review, "YYYY-MM".</param>
    /// <param name="take">How many rows per list. A list of 200 does not get read.</param>
    public static MovementResult? Compare(IReadOnlyList<Report> reports, string period, int take = 5)
    {
        var all = reports ?? [];

        var current = all.FirstOrDefault(x => x.ReportPeriod == period);
        if (PagesOf(current) is not { Count: > 0 } now) return null;

        var previous = all
            .Where(x => string.CompareOrdinal(x.ReportPeriod, period) < 0 && HasPages(x))
            .OrderByDescending(x => x.ReportPeriod, StringComparer.Ordinal)
            .FirstOrDefault();

        var yearAgo = YearBefore(period) is { } wanted
            ? all.FirstOrDefault(x => x.ReportPeriod == wanted && HasPages(x))
            : null;

        // The year month can BE the nearest earlier one in an archive with gaps. That is one comparison, and it must not be
        // reported as two.
        if (previous is not null && previous.ReportPeriod == yearAgo?.ReportPeriod) previous = null;

        if (previous is null && yearAgo is null) return null;

        // THE PREVIOUS MONTH RANKS, whenever it exists.
        //
        // The reason is CONSISTENCY, not statistics. Ranked on the year comparison when there was one and on the previous
        // month otherwise, the order would mean different things in different months: July 2026 sorted against July 2025,
        // June 2026 against May 2026. The figures can be compared between months, an order that changes its basis cannot.
        //
        // The seasonal objection (July against June is holiday time) is answered another way: BOTH figures are on the row, so
        // the year comparison remains as a check without setting the order. The previous month also exists for every month
        // but the archive's oldest, while the year month is missing for everything before the backfill began.
        var rankBy = previous is not null ? RankPrevious : RankYearAgo;

        long? RankOf(MoveDelta? vsPrevious, MoveDelta? vsYearAgo) => rankBy == RankYearAgo ? vsYearAgo?.Delta : vsPrevious?.Delta;
        long Rank(Move move) => RankOf(move.VsPrevious, move.VsYearAgo) ?? 0;

        var nowByUrl = ByUrl(now);
        var previousByUrl = ByUrl(PagesOf(previous));
        var yearAgoByUrl = ByUrl(PagesOf(yearAgo));

        var urls = nowByUrl.Keys
            .Union(previousByUrl.Keys, StringComparer.OrdinalIgnoreCase)
            .Union(yearAgoByUrl.Keys, StringComparer.OrdinalIgnoreCase);

        var moves = new List<Move>();

        foreach (var url in urls)
        {
            nowByUrl.TryGetValue(url, out var a);
            previousByUrl.TryGetValue(url, out var p);
            yearAgoByUrl.TryGetValue(url, out var y);

            var impressions = a?.Impressions ?? 0;

            var vsPrevious = previous is null ? null : Delta(impressions, p);
            var vsYearAgo = yearAgo is null ? null : Delta(impressions, y);

            // A row with no movement in the comparison that ranks is not a movement.
            if (RankOf(vsPrevious, vsYearAgo) is null or 0) continue;

            moves.Add(new Move(
                Path: a?.Path ?? p?.Path ?? y?.Path ?? url,
                Url: url,
                Impressions: impressions,
                Position: a?.Position,
                VsPrevious: vsPrevious,
                VsYearAgo: vsYearAgo));
        }

        return new MovementResult(
            Period: period,
            Previous: previous is null ? null : new Comparison(previous.ReportPeriod),
            YearAgo: yearAgo is null ? null : new Comparison(yearAgo.ReportPeriod),
            RankedBy: rankBy,
            Losses: [.. moves.Where(x => Rank(x) < 0).OrderBy(Rank).Take(take)],
            Gains: [.. moves.Where(x => Rank(x) > 0).OrderByDescending(Rank).Take(take)]);
    }

    private static MoveDelta Delta(long impressions, SearchLandingPage? before)
    {
        var was = before?.Impressions ?? 0;
        var delta = impressions - was;

        return new MoveDelta(
            Before: was,
            Delta: delta,

            // A new page has no percentage change: "new" is the information, and "+infinity" is not a figure that can be shown.
            ChangePercent: was == 0 ? null : Math.Round(delta / (double)was * 100.0, 1),

            PositionBefore: before?.Position);
    }

    /// <summary>
    /// The key is the URL, not the shortened path: the path is presentation and can change with a code change, while the URL
    /// is the page's identity.
    /// </summary>
    private static Dictionary<string, SearchLandingPage> ByUrl(List<SearchLandingPage>? pages) =>
        (pages ?? [])
            .GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    /// <summary>The same month a year earlier, or null for a period that is not "YYYY-MM". Shared with <see cref="Narrative"/>.</summary>
    internal static string? YearBefore(string period) =>
        DateOnly.TryParseExact(
            $"{period}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
            out var month)
            ? month.AddMonths(-12).ToString("yyyy-MM", CultureInfo.InvariantCulture)
            : null;

    private static List<SearchLandingPage>? PagesOf(Report? report) =>
        report?.Traffic?.Search?.Pages;

    /// <summary>A month counts as a comparison only when it holds a page list: without one everything would look like a gain.</summary>
    private static bool HasPages(Report report) => PagesOf(report) is { Count: > 0 };
}
