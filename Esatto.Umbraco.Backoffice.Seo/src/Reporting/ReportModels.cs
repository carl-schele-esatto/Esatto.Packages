using Esatto.Umbraco.Backoffice.Seo.Checks;
using Esatto.Umbraco.Backoffice.Seo.Evaluation;
using Esatto.Umbraco.Backoffice.Seo.Persistence;
using Esatto.Umbraco.Backoffice.Seo.Scanning;

namespace Esatto.Umbraco.Backoffice.Seo.Reporting;

public sealed record ScanInfo(
    int Id, DateTime StartedUtc, DateTime? FinishedUtc, string Trigger, string UserName, string Status,
    int PagesTotal, int PagesDone, int PagesFailed, int PagesSkipped, int? Score, string? Message)
{
    public static ScanInfo From(ScanEntity s) => new(s.Id, s.StartedUtc, s.FinishedUtc, s.Trigger, s.UserName, s.Status,
        s.PagesTotal, s.PagesDone, s.PagesFailed, s.PagesSkipped, s.Score, s.Message);
}

public sealed record TrendPoint(int ScanId, DateTime StartedUtc, int Score, int Pages);

public sealed record SeverityCounts(int Critical, int Warning, int Notice);

/// <summary>A whole-site check on the Overview. <see cref="Passed"/> null = not applicable.</summary>
public sealed record SiteCheckRow(string CheckId, Severity Severity, bool? Passed, string? Value);

public sealed record PageRow(Guid ContentKey, string Name, string Culture, string Url, string Reason);

/// <summary>
/// The Overview tab. <see cref="LastScan"/> is the latest complete scan (what the numbers are from);
/// <see cref="LatestAttempt"/> is the newest scan of any status, shown when it failed or was cancelled.
/// </summary>
public sealed record Overview(
    ScanInfo? LastScan,
    ScanInfo? LatestAttempt,
    ScanProgress? Running,
    int? Score,
    IReadOnlyList<TrendPoint> Trend,
    IReadOnlyList<IssueSummary> Top,
    SeverityCounts Counts,
    IReadOnlyList<SiteCheckRow> SiteChecks,
    IReadOnlyList<PageRow> Hidden,
    IReadOnlyList<PageRow> Skipped,
    bool SiteHidden,
    bool IntentionallyHidden);

/// <summary>Issues tab filter. <see cref="Ignored"/> true = show the ignored findings instead of the open ones.</summary>
public sealed record IssueFilter(
    Severity? Severity = null, Owner? Owner = null, string? Culture = null, string? CheckId = null, bool Ignored = false, string? Search = null);

public sealed record IssueOccurrence(Guid? ContentKey, string Name, string Culture, string? Url, string? Value, bool Ignored, int? IgnoreId);

public sealed record IssueGroup(IssueSummary Summary, IReadOnlyList<IssueOccurrence> Occurrences);
