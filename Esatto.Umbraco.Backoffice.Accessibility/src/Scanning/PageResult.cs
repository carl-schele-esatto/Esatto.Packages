namespace Esatto.Umbraco.Backoffice.Accessibility.Scanning;

/// <summary>
/// One scanned page as posted by the dashboard: the axe results reduced to what the report needs.
/// <see cref="Status"/> is <c>ok</c>, <c>skipped</c> or <c>failed</c> (then <see cref="Reason"/> says why).
/// </summary>
public sealed record PageResult(
    Guid ContentKey,
    string Name,
    string Culture,
    string Url,
    int Viewport,
    string Status,
    string? Reason,
    IReadOnlyList<PassedRule> Passes,
    IReadOnlyList<RuleResult> Rules,
    IReadOnlyList<string> DocumentLinks);

/// <summary>An axe rule that failed (<c>violation</c>) or needs review (<c>incomplete</c>) on the page.</summary>
public sealed record RuleResult(
    string RuleId,
    string Kind,
    string? Impact,
    string Help,
    string HelpUrl,
    IReadOnlyList<string> Tags,
    IReadOnlyList<NodeResult> Nodes);

/// <summary>An axe rule that passed on the page, with its tags (so the server can tell which criteria it covers).</summary>
public sealed record PassedRule(string RuleId, IReadOnlyList<string> Tags);

/// <summary>One affected element: its CSS selector and an HTML snippet.</summary>
public sealed record NodeResult(string Selector, string Html);

/// <summary>A page already recorded in a scan, so a resumed scan can skip it.</summary>
public sealed record DonePage(Guid ContentKey, string Culture, int Viewport);

/// <summary>A scan as listed on the Overview (last scan + history). <see cref="Mobile"/>: it also scanned at 320 px.</summary>
public sealed record ScanSummary(
    int Id,
    DateTime StartedUtc,
    DateTime? FinishedUtc,
    string Status,
    string Target,
    int? Score,
    int PagesTotal,
    int PagesDone,
    int PagesSkipped,
    int PagesFailed,
    string UserName,
    bool Mobile);
