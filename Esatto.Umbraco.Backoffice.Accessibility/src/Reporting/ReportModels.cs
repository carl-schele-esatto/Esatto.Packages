using Esatto.Umbraco.Backoffice.Accessibility.Knowledge;
using Esatto.Umbraco.Backoffice.Accessibility.Scanning;

namespace Esatto.Umbraco.Backoffice.Accessibility.Reporting;

/// <summary>One of the four WCAG principles on the Overview: failing criteria and criteria still to check.</summary>
public sealed record PrincipleTile(int Principle, string Name, int Fail, int ToCheck);

/// <summary>The dashboard's Overview tab.</summary>
public sealed record Overview(
    WcagTarget Target,
    ScoreResult Score,
    IReadOnlyList<PrincipleTile> Principles,
    ScanSummary? LastScan,
    IReadOnlyList<ScanSummary> History,
    int ManualChecked,
    int ManualTotal);

/// <summary>Issues tab filters; null means "any". <see cref="FalsePositives"/> true lists only false positives.</summary>
public sealed record IssueFilter(int? Principle, WcagLevel? Level, Owner? Owner, string? Impact, bool FalsePositives, string? Criterion, string? RuleId = null, string? Kind = null);

/// <summary>One axe rule's findings across the site (violations and "needs review" are separate groups).</summary>
public sealed record IssueGroup(
    string RuleId,
    string Title,
    IReadOnlyList<string> Criteria,
    WcagLevel? Level,
    Owner Owner,
    string? Impact,
    string Kind,
    bool BestPractice,
    int PageCount,
    int ElementCount,
    string Why,
    string HowToFix,
    string Caveats,
    string LearnMoreUrl,
    IReadOnlyList<IssuePage> Pages);

/// <summary>A page with the issue. <see cref="Viewports"/>: every width the scan loaded it at, so a re-check covers them all.</summary>
public sealed record IssuePage(Guid ContentKey, string Name, string Culture, string Url, IReadOnlyList<IssueNode> Nodes, IReadOnlyList<int> Viewports);

/// <summary>An affected element. <see cref="FalsePositiveId"/> is set when it has been marked as a false positive.</summary>
public sealed record IssueNode(int FindingId, string Selector, string Html, Guid? MediaKey, int? FalsePositiveId);
