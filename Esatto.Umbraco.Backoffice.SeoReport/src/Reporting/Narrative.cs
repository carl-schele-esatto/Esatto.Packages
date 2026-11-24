using Esatto.Umbraco.Backoffice.SeoReport.Google;

namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

/// <summary>
/// One statement about the month.
///
/// The text is a <see cref="TextRef"/>: a key and raw figures, never prose (the English wording is in en.ts). WHERE it sits
/// decides its key: area "narrative", part Text, looked up with <c>ReportKeys.Lookup("narrative", Text.Key, "Text")</c>
/// (narrativeSourcesText and so on). Its <c>Key</c> is also the statement's identity for the tests and the view: one of the
/// <c>Narrative.Key*</c> constants. <see cref="Narrative"/> documents the arguments of each.
/// </summary>
public record Statement(TextRef Text);

/// <summary>
/// The month in words: the deterministic counterpart of the points in a monthly report written by hand.
///
/// WHY THE CLASS EXISTS. The dashboard produced tables and a mechanical sentence per row. A report written by a person does
/// something else: it JOINS sources that contradict each other, NAMES a cause and DRAWS a conclusion. Compare "Demand is there,
/// the ranking is not" with "GA4 seems to fall but Search Console is stable: the cause is measurement, not traffic". It is the
/// second kind of sentence that reads as analysis.
///
/// THREE INGREDIENTS, AND THIS CLASS OWNS TWO. Joining our own figures and drawing the conclusion from them can be done
/// deterministically, and is done here. Outside events (a cookie banner, an outage, a core update) NO data source can know
/// about; they come from the editor's month note and are shown beside these statements.
///
/// EVERY STATEMENT MUST BE SUPPORTED BY THE FIGURES. When the basis is missing, or zero, the statement is absent: a summary
/// that guesses is worse than none, because it is read with the same confidence as the figures it is built on. A figure that
/// is not a finite number counts as missing.
///
/// The server sends no wording, so each statement is a key plus raw figures. A part of a statement that has nothing to say (a
/// comparison month that does not exist) is sent as null and not left out, so a statement always carries the same arguments.
/// The arguments (percentages with one decimal, positions with one decimal, periods "YYYY-MM"):
///   sources      clicks, clicksChange, sessions, sessionsChange, agree (true when both sources move the same way)
///   visibility   top3, total, percent;  comparePeriod, compareTop3, comparePercent, yearOnYear (true: the same month a year
///                earlier, false: the nearest earlier month);  bestPeriod, bestTop3, bestPercent
///   nonBrand     clicks (non-brand), total (all clicks), percent
///   conversion   sessions, events
///   movement     path (as it is: "/" is data and the client names it);  yearAgoPeriod, yearAgoDelta;  previousPeriod,
///                previousDelta (a signed difference in impressions: the sign is the direction);  position, positionBefore
///   season       period (the one month the movement is compared with)
/// </summary>
public static class Narrative
{
    /// <summary>Search Console against GA4: the same month, two sources.</summary>
    public const string KeySources = "sources";

    /// <summary>How large a share of the clicks comes from searches without the brand.</summary>
    public const string KeyNonBrand = "nonBrand";

    /// <summary>The page that moved the most, and in which direction.</summary>
    public const string KeyMovement = "movement";

    /// <summary>The caveat when the comparison is month against month instead of year against year.</summary>
    public const string KeySeason = "season";

    /// <summary>The share of search queries in the top three, and how it has moved.</summary>
    public const string KeyVisibility = "visibility";

    /// <summary>The conversions set against the traffic that produced them.</summary>
    public const string KeyConversion = "conversion";

    /// <summary>Every statement key, which is also every text id the class emits (see ReportKeys).</summary>
    internal static readonly string[] Kinds = [KeySources, KeyNonBrand, KeyMovement, KeySeason, KeyVisibility, KeyConversion];

    /// <param name="report">The month under review. Null gives only what the other two inputs can say.</param>
    /// <param name="movement">The biggest movement for the month, or null when there is none.</param>
    /// <param name="visibility">The visibility of every archived month, in any order. Null counts as empty.</param>
    public static IReadOnlyList<Statement> Write(
        Report? report,
        MovementResult? movement,
        IReadOnlyList<VisibilityMonth>? visibility)
    {
        var said = new List<Statement>();

        AddSources(report, said);
        AddVisibility(report, visibility, said);
        AddNonBrand(report, said);
        AddConversion(report, said);
        AddMovement(movement, said);

        return said;
    }

    /// <summary>
    /// The conversions set against the traffic that produced them.
    ///
    /// ONE STATEMENT AND NOT A CHART. The volume is a handful a month, and GA4 only counts key events from the day they are
    /// marked as key events. A series over the archive would therefore be a row of zeros and a few events: a curve that claims
    /// a fall that never happened. Same reason the mapper adds a note instead of a zero: a zero reads as an outcome and not as
    /// a missing measurement.
    ///
    /// ABSOLUTE FIGURES, NO RATE. One event in 30 visits is 3.3 percent, but one event more or less makes it 0 or 6.7. A
    /// conversion rate on that basis is noise with decimals, and it would be read with the same confidence as the figures it
    /// is built on.
    /// </summary>
    private static void AddConversion(Report? report, List<Statement> said)
    {
        // Only when there ARE conversions. The mapper does not even add the KPI when everything is zero, so the lookup fails on
        // its own in the usual month, but the condition is here too so the statement can never start claiming "0 key events".
        if (Figure(KpiOf(report, ReportMapper.OrganicKeyEvents)) is not { } events || events <= 0) return;

        // Without the traffic to set the number against, the statement is just the KPI again: it is the JOIN of the two
        // figures that is the analysis.
        if (Figure(KpiOf(report, ReportMapper.OrganicSessions)) is not { } sessions || sessions <= 0) return;

        said.Add(Say(KeyConversion, ("sessions", sessions), ("events", events)));
    }

    /// <summary>
    /// The share of search queries in the top three, set against the same month a year earlier and against the archive's best.
    ///
    /// A SHARE AND NOT A COUNT. The basis is the N most shown search queries, a basket of FIXED size: the number in the top
    /// three can be read as a share but never as "how many words we rank for". The table this statement replaced said the
    /// basket size on every row, which was the row limit and not a measurement, and left the reader to draw the conclusion from
    /// thirteen rows.
    ///
    /// THE CAVEAT THAT COMES ALONG: the basket holds different words in different months, because Google sorts it on clicks.
    /// The wording therefore says "of your most shown search queries" and never "of your search queries".
    /// </summary>
    private static void AddVisibility(Report? report, IReadOnlyList<VisibilityMonth>? visibility, List<Statement> said)
    {
        // A month with no examined queries has no share: it cannot be the month shown, a comparison or the best month, and it
        // keeps every figure below finite. Sorted here, so the nearest earlier month does not depend on the caller's order.
        var months = (visibility ?? [])
            .Where(x => x is { Total: > 0 })
            .OrderBy(x => x.Period, StringComparer.Ordinal)
            .ToList();

        var now = months.FirstOrDefault(x => x.Period == report?.ReportPeriod);
        if (now is null) return;

        // The comparison: the same month a year earlier if it exists, otherwise the nearest earlier month.
        var yearAgo = Movement.YearBefore(now.Period);
        var yearOnYear = months.FirstOrDefault(x => x.Period == yearAgo);
        var earlier = yearOnYear
            ?? months.LastOrDefault(x => string.CompareOrdinal(x.Period, now.Period) < 0);

        // THE FALLBACK MUST BE SAID. Traffic is seasonal, and a share compared with an earlier month can move because of the
        // season and not the work. The client words the caveat from yearOnYear = false (the same rule as Movement follows).
        bool? isYearOnYear = earlier is null ? null : yearOnYear is not null;

        // The best month is named only when it is a THIRD month: otherwise the statement repeats itself. It is ranked by the EXACT
        // share (Total is above zero for every month here), never by the rounded one shown: two months that print the same
        // percent would tie, the earlier would win, and the statement could name another month as the best right after giving
        // the shown month's identical figure. MaxBy keeps the first of equal shares, and the months are in chronological order,
        // so an exact tie goes to the earliest month.
        var best = months.MaxBy(x => (double)x.Top3 / x.Total)!;
        var bestIsThird = best.Period != now.Period && best.Period != earlier?.Period;

        said.Add(Say(
            KeyVisibility,
            ("top3", now.Top3),
            ("total", now.Total),
            ("percent", Share(now)),
            ("comparePeriod", earlier?.Period),
            ("compareTop3", earlier?.Top3),
            ("comparePercent", earlier is null ? null : Share(earlier)),
            ("yearOnYear", isYearOnYear),
            ("bestPeriod", bestIsThird ? best.Period : null),
            ("bestTop3", bestIsThird ? best.Top3 : null),
            ("bestPercent", bestIsThird ? Share(best) : null)));
    }

    /// <summary>
    /// The month's share in the top three in percent, one decimal: the figure that is SHOWN. Months are never ranked on it (see
    /// <see cref="AddVisibility"/>). Only called for a month with a total above zero.
    /// </summary>
    private static double Share(VisibilityMonth month) => Percent(month.Top3, month.Total) ?? 0;

    /// <summary>
    /// The two sources set against each other.
    ///
    /// THE CONCLUSION IS THE POINT OF THE STATEMENT. When the sources point in DIFFERENT directions it is almost always the
    /// measurement that separates them: Search Console counts clicks at Google, GA4 counts visits in the browser, where
    /// blocked scripts and declined cookie consent are not measured. When they point the same way the statement must NOT hint
    /// at a measurement error: then they are consistent, and that is information too.
    /// </summary>
    private static void AddSources(Report? report, List<Statement> said)
    {
        var clicks = KpiOf(report, ReportMapper.GscClicks);
        var sessions = KpiOf(report, ReportMapper.OrganicSessions);

        if (Figure(clicks) is not { } c || Figure(sessions) is not { } s) return;

        // Without trends there is no direction to speak of.
        if (Change(clicks) is not { } clicksChange || Change(sessions) is not { } sessionsChange) return;

        // Math.Sign, so a change of exactly zero is a direction of its own.
        var agree = Math.Sign(clicksChange) == Math.Sign(sessionsChange);

        said.Add(Say(
            KeySources,
            ("clicks", c),
            ("clicksChange", clicksChange),
            ("sessions", s),
            ("sessionsChange", sessionsChange),
            ("agree", agree)));
    }

    /// <summary>
    /// Non-brand against the total. Growth has to come from there: brand follows the brand's awareness, and long-term SEO work
    /// does not move it.
    /// </summary>
    private static void AddNonBrand(Report? report, List<Statement> said)
    {
        var nonBrand = (report?.Traffic?.Search?.Segments ?? [])
            .FirstOrDefault(x => x?.Key == GscAgent.SegmentNonBrand)?.Metrics
            ?.FirstOrDefault(x => x?.Key?.EndsWith("_clicks", StringComparison.Ordinal) == true);

        if (Figure(nonBrand) is not { } clicks
            || Figure(KpiOf(report, ReportMapper.GscClicks)) is not { } total
            || Percent(clicks, total) is not { } percent)
        {
            return;
        }

        said.Add(Say(KeyNonBrand, ("clicks", clicks), ("total", total), ("percent", percent)));
    }

    /// <summary>
    /// The biggest movement, plus the caveat when the comparison is not year against year.
    ///
    /// THE POSITION BEFORE AND AFTER is what decides what to DO: the same drop means different things if the page stays at
    /// position 12 (the demand fell) or has fallen to 35 (we lost the ranking).
    /// </summary>
    private static void AddMovement(MovementResult? movement, List<Statement> said)
    {
        if (movement is null) return;

        if ((movement.Losses ?? []).FirstOrDefault() is { } worst && Drop(movement, worst) is { } drop)
        {
            said.Add(drop);
        }

        if (movement.YearAgo is null && movement.Previous is { } only)
        {
            said.Add(Say(KeySeason, ("period", only.Period)));
        }
    }

    /// <summary>
    /// The statement about the page that lost the most, or null when there is no figure to say how much.
    ///
    /// BOTH COMPARISONS IN THE STATEMENT when both exist. "6,000 fewer impressions than last July and 400 fewer than last month"
    /// says two different things: how far below last year the page is, and whether the drop is new. Either figure alone can be
    /// misread in both directions. Each keeps its own sign: a page can be below last year and above last month.
    /// </summary>
    private static Statement? Drop(MovementResult movement, Move worst)
    {
        var vsYearAgo = movement.YearAgo is null ? null : worst.VsYearAgo;
        var vsPrevious = movement.Previous is null ? null : worst.VsPrevious;

        // A drop that says nothing about how large it was is a sentence that only names the page: vague.
        if (vsYearAgo is null && vsPrevious is null) return null;

        // The position is compared with the month the list is sorted on, so the figure belongs with the drop that gave the row
        // its place.
        var against = movement.RankedBy == Movement.RankYearAgo ? worst.VsYearAgo : worst.VsPrevious;

        // The position before and the position now, both or neither: one of them is half a sentence.
        var position = Finite(worst.Position);
        var before = Finite(against?.PositionBefore);
        if (position is null || before is null) position = before = null;

        return Say(
            KeyMovement,
            ("path", worst.Path),
            ("yearAgoPeriod", vsYearAgo is null ? null : movement.YearAgo?.Period),
            ("yearAgoDelta", vsYearAgo?.Delta),
            ("previousPeriod", vsPrevious is null ? null : movement.Previous?.Period),
            ("previousDelta", vsPrevious?.Delta),
            ("position", Shown1(position)),
            ("positionBefore", Shown1(before)));
    }

    private static Statement Say(string key, params (string Name, object? Value)[] args) => new(TextRef.Of(key, args));

    private static Kpi? KpiOf(Report? report, string key) =>
        (report?.Traffic?.Kpis ?? []).FirstOrDefault(x => x?.Key == key);

    /// <summary>The KPI's value, or null when it has none or it is not a finite number.</summary>
    private static double? Figure(Kpi? kpi) => Finite(kpi?.Value);

    /// <summary>The KPI's change against the comparison period in percent, or null when there is no trend or it is not finite.</summary>
    private static double? Change(Kpi? kpi) => Finite(kpi?.Trend?.ChangePercent);

    private static double? Finite(double? value) => value is { } v && double.IsFinite(v) ? v : null;

    private static double? Shown1(double? value) => value is { } v ? ReportMapper.Shown1(v) : null;

    /// <summary>A share in percent with one decimal, or null when there is nothing to take a share of.</summary>
    private static double? Percent(double part, double whole)
    {
        if (whole <= 0) return null;

        var percent = Math.Round(part / whole * 100.0, 1);
        return double.IsFinite(percent) ? percent : null;
    }
}
