using System.Globalization;
using System.Text.RegularExpressions;
using Esatto.Umbraco.Backoffice.SeoReport.Api;
using Esatto.Umbraco.Backoffice.SeoReport.Google;

namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

/// <summary>
/// Turns an id into a translation key, and lists every key the server can emit.
///
/// A key is <c>&lt;area&gt;&lt;PascalCase(baseId)&gt;&lt;Part&gt;</c>: area <c>kpi</c>, id <c>traffic_organic</c> and part
/// <c>Label</c> give <c>kpiTrafficOrganicLabel</c>. The client applies the same rule in TypeScript, and
/// <c>Client/src/lang/key-vectors.json</c> is the shared set of examples both sides are tested against. Change the rule
/// in neither place alone.
///
/// One addition for area <c>series</c> only, the YEAR SUFFIX: a series id that ends in an underscore and exactly four digits
/// (<c>sessions_2025</c>) is looked up without them (<c>seriesSessionsLabel</c>), and the text gets the four digits as its
/// argument <c>year</c> (see <see cref="SeriesYear"/>). Year-pivoted charts have one series per year, and this keeps one text
/// per kind of series instead of one per year.
/// </summary>
public static partial class ReportKeys
{
    private static readonly string[] Areas =
        ["kpi", "chart", "series", "annotation", "insight", "narrative", "opportunity", "target", "error", "ui"];

    private static readonly string[] Parts = ["Label", "Description", "Title", "Subtitle", "Text"];

    // Built on first use, not by the type initializer: a bad registration then fails the first use of All (and the registry
    // test) instead of making every Lookup throw TypeInitializationException.
    private static readonly Lazy<IReadOnlyCollection<string>> Registry = new(() => Build(Registrations()));

    /// <summary>
    /// Every key the server can emit; the English wording of each lives in <c>Client/src/lang/en.ts</c>. The task that
    /// starts emitting a set of ids registers them in <see cref="Registrations"/>. Throws <see cref="InvalidOperationException"/>
    /// when a key is registered twice and <see cref="ArgumentException"/> for a malformed registration.
    /// </summary>
    public static IReadOnlyCollection<string> All => Registry.Value;

    /// <summary>
    /// The key for one text. Throws <see cref="ArgumentException"/> for an unknown area or part, and for an id that is not
    /// letters and digits joined by single underscores, so a typo fails loudly instead of becoming a missing text. A series
    /// id with a year suffix is looked up without it (see the class summary).
    /// </summary>
    public static string Lookup(string area, string baseId, string part)
    {
        if (!Areas.Contains(area, StringComparer.Ordinal))
            throw new ArgumentException($"Unknown key area '{area}'.", nameof(area));
        if (!Parts.Contains(part, StringComparer.Ordinal))
            throw new ArgumentException($"Unknown key part '{part}'.", nameof(part));
        if (!IdPattern().IsMatch(baseId))
            throw new ArgumentException($"'{baseId}' is not a valid key id; use letters and digits joined by single underscores.", nameof(baseId));

        if (area == "series" && YearSuffix().Match(baseId) is { Success: true } year) baseId = year.Groups["base"].Value;

        // Capitalize each word's first letter and keep the rest, so an id that is already camelCase survives.
        var words = baseId.Split('_').Select(word => char.ToUpperInvariant(word[0]) + word[1..]);
        return area + string.Concat(words) + part;
    }

    /// <summary>
    /// The year a series id carries as its suffix (<c>clicks_2026</c> gives 2026), or null when it has none. The client passes it
    /// to the series label as the argument <c>year</c>.
    /// </summary>
    public static int? SeriesYear(string seriesKey)
        => YearSuffix().Match(seriesKey) is { Success: true } match
            ? int.Parse(match.Groups["year"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture)
            : null;

    /// <summary>Every combination of the given ids and parts in one area, ready to register.</summary>
    internal static IEnumerable<string> Keys(string area, IEnumerable<string> parts, params IEnumerable<string> baseIds)
        => baseIds.SelectMany(baseId => parts.Select(part => Lookup(area, baseId, part)));

    /// <summary>
    /// The registry from its registrations, in registration order. A key registered twice is a mistake (two texts would compete
    /// for one key), so it throws <see cref="InvalidOperationException"/> naming every duplicate instead of keeping one quietly.
    /// </summary>
    internal static IReadOnlyCollection<string> Build(IEnumerable<IEnumerable<string>> registrations)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var keys = new List<string>();
        var duplicates = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var key in registrations.SelectMany(keys => keys))
        {
            if (seen.Add(key)) keys.Add(key);
            else duplicates.Add(key);
        }

        return duplicates.Count == 0
            ? keys.AsReadOnly()
            : throw new InvalidOperationException($"Translation keys registered more than once: {string.Join(", ", duplicates)}.");
    }

    /// <summary>
    /// The registry. The task that starts emitting a set of ids adds its lines below; Report.cs (and, for the analysis, the
    /// Opportunity and TargetRow records) documents, for every place a text can sit, the area and parts the client looks it up
    /// with, and each line here is one such place.
    /// </summary>
    private static IEnumerable<IEnumerable<string>> Registrations() =>
    [
        .. ReportMapperKeys(),
        .. AnalysisKeys(),
        .. ErrorKeys(),
    ];

    /// <summary>What <see cref="ReportMapper"/> emits, one line per place a text sits in the report.</summary>
    internal static IEnumerable<IEnumerable<string>> ReportMapperKeys() =>
    [
        Keys("kpi", ["Label", "Description"], ReportMapper.TrafficKpis),      // Traffic.Kpis[].Key
        Keys("kpi", ["Label"], ReportMapper.SegmentMetricKeys),               // SearchSegment.Metrics[].Key
        Keys("kpi", ["Text"], ReportMapper.TrendTexts),                       // Trend.Text.Key
        Keys("chart", ["Title", "Subtitle", "Description"], ReportMapper.Charts), // ChartBlock.Id
        Keys("chart", ["Title", "Subtitle"], ReportMapper.SearchBlockId),     // SearchBlock.Id
        Keys("series", ["Label"], ReportMapper.SeriesKinds),                  // Series.Key, SearchSegment.Key
        Keys("annotation", ["Title", "Text"], ReportMapper.AnnotationTexts),  // Annotation.Text.Key
        Keys("ui", ["Label"], ReportMapper.Sources),                          // SourceStatus.Id
        Keys("ui", ["Text"], ReportMapper.SourceNotes),                       // SourceStatus.Note.Key
        Keys("ui", ["Text"], ReportMapper.DataStatusNotes),                   // Report.DataStatusNote.Key
    ];

    /// <summary>
    /// What the analysis classes emit; the records and classes document where each text sits (Opportunity.Text, TargetRow,
    /// Statement.Text, DailyChart).
    /// </summary>
    internal static IEnumerable<IEnumerable<string>> AnalysisKeys() =>
    [
        Keys("opportunity", ["Title", "Text"], Opportunities.Kinds),          // Opportunity.Text.Key
        Keys("target", ["Label"], TargetAnalysis.Statuses),                   // TargetRow.Status
        Keys("target", ["Text"], TargetAnalysis.AdviceTexts),                 // TargetRow.Advice.Key
        Keys("narrative", ["Text"], Narrative.Kinds),                         // Statement.Text.Key
        Keys("chart", ["Title", "Subtitle", "Description"], DailyChart.ChartId), // the daily chart's ChartBlock.Id
        Keys("series", ["Label"], DailyChart.SeriesDaily, DailyChart.SeriesBaseline), // the daily chart's Series.Key
    ];

    /// <summary>
    /// What names a problem: a <see cref="ProblemInfo"/> (and so a fetch or a Test connection result) sends its code, and the
    /// client looks the title and the text up in area <c>error</c> under that code in snake_case (<see cref="ErrorCodes.BaseId"/>).
    /// The API's own refusals (<see cref="ApiCodes"/>) are looked up the same way.
    /// </summary>
    internal static IEnumerable<IEnumerable<string>> ErrorKeys() =>
    [
        Keys("error", ["Title", "Text"], ErrorCodes.All.Select(ErrorCodes.BaseId)), // ProblemInfo.Code, SourceTest.ErrorCode
        Keys("error", ["Title", "Text"], ApiCodes.All.Select(ErrorCodes.BaseId)),   // the problem's code when the API refuses the request
    ];

    [GeneratedRegex(@"^[A-Za-z0-9]+(_[A-Za-z0-9]+)*\z")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"^(?<base>[A-Za-z0-9]+(?:_[A-Za-z0-9]+)*)_(?<year>[0-9]{4})\z")]
    private static partial Regex YearSuffix();
}
