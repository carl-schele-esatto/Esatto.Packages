using Esatto.Umbraco.Backoffice.Accessibility.Knowledge;
using Esatto.Umbraco.Backoffice.Accessibility.Persistence;
using Esatto.Umbraco.Backoffice.Accessibility.Scanning;
using Esatto.Umbraco.Backoffice.Accessibility.Settings;

namespace Esatto.Umbraco.Backoffice.Accessibility.Reporting;

/// <summary>
/// Builds the Overview and Issues from the site's latest scan, its manual results and false positives.
/// Computed live, because manual results and false positives change after a scan; each finished scan
/// also stores a snapshot for the history list.
/// </summary>
public sealed class ReportService(IAccessibilityStore store, KnowledgeBase knowledge, SettingsService settings)
{
    private const int HistorySize = Retention.Keep;

    private static readonly string[] PrincipleNames = ["Perceivable", "Operable", "Understandable", "Robust"];
    private static readonly string[] ImpactOrder = ["critical", "serious", "moderate", "minor"];

    public async Task<Overview> GetOverviewAsync(Guid siteKey)
    {
        var target = (await settings.GetAsync()).Target;
        var scans = await store.GetScansAsync(siteKey, HistorySize);
        var data = await LoadAsync(siteKey, ScanForReport(scans));
        var inputs = Inputs(target, data);

        var principles = Enumerable.Range(1, 4).Select(p =>
        {
            var statuses = inputs.Where(i => i.Criterion.Principle == p).Select(ScoreCalculator.StatusOf).ToList();
            return new PrincipleTile(p, PrincipleNames[p - 1],
                statuses.Count(s => s == CriterionStatus.Fail),
                statuses.Count(s => s == CriterionStatus.NotVerified));
        }).ToList();

        var summaries = new List<ScanSummary>(scans.Count);
        foreach (var scan in scans) summaries.Add(await SummaryAsync(scan));

        var manualCriteria = knowledge.For(target).Where(c => !c.FullyAutomatable).Select(c => c.Id).ToHashSet();
        var score = ScoreCalculator.Compute(inputs);
        // Nothing scanned and nothing checked by hand: "0 %, major gaps" would be a verdict on nothing.
        if (scans.Count == 0 && data.Manual.Count == 0) score = score with { Percent = null };
        return new Overview(
            target,
            score,
            principles,
            summaries.FirstOrDefault(),
            summaries,
            data.Manual.Count(m => manualCriteria.Contains(m.CriterionId)),
            manualCriteria.Count);
    }

    /// <summary>
    /// A scan's summary. Counters are stored when a scan finishes, so an unfinished one is counted from the
    /// pages recorded so far; otherwise an interrupted scan reads "0 pages" as if nothing had been kept.
    /// </summary>
    private async Task<ScanSummary> SummaryAsync(ScanEntity scan)
    {
        var summary = ScanService.Summary(scan);
        if (scan.Status != ScanStatus.Running) return summary;
        var pages = await store.GetPagesAsync(scan.Id);
        return summary with
        {
            PagesDone = pages.Count(p => p.Status == PageStatus.Ok),
            PagesSkipped = pages.Count(p => p.Status == PageStatus.Skipped),
            PagesFailed = pages.Count(p => p.Status == PageStatus.Failed),
        };
    }

    /// <summary>The score of one scan against the scan's own target (stored as its history snapshot).</summary>
    public async Task<ScoreResult> ComputeScoreAsync(int scanId)
    {
        var scan = await store.GetScanAsync(scanId) ?? throw new KeyNotFoundException($"Scan {scanId} not found.");
        var target = new WcagTarget(Enum.Parse<WcagVersion>(scan.Version), Enum.Parse<WcagLevel>(scan.Level));
        return ScoreCalculator.Compute(Inputs(target, await LoadAsync(scan.SiteKey, scan)));
    }

    /// <summary>
    /// The issue groups. The list carries counts only (<see cref="IssueGroup.Pages"/> empty), because a site
    /// can have thousands of affected elements; the elements, with their HTML, come with
    /// <paramref name="withNodes"/> or when the filter names one group (<see cref="IssueFilter.RuleId"/>).
    /// </summary>
    public async Task<IReadOnlyList<IssueGroup>> GetIssuesAsync(Guid siteKey, IssueFilter filter, bool withNodes = false)
    {
        withNodes |= filter.RuleId is not null;
        var target = (await settings.GetAsync()).Target;
        var data = await LoadAsync(siteKey, ScanForReport(await store.GetScansAsync(siteKey, HistorySize)), withHtml: withNodes);
        var inTarget = knowledge.For(target).ToDictionary(c => c.Id);
        var pages = data.Pages.ToDictionary(p => p.Id);
        var viewports = data.Pages
            .GroupBy(p => (p.ContentKey, p.Culture))
            .ToDictionary(g => g.Key, IReadOnlyList<int> (g) => g.Select(p => p.Viewport).Distinct().OrderDescending().ToList());

        var groups = new List<IssueGroup>();
        foreach (var byRule in data.Findings.GroupBy(f => (f.RuleId, f.Kind)))
        {
            if (filter.RuleId is { } wantedRule && (byRule.Key.RuleId != wantedRule || (filter.Kind is { } wantedKind && byRule.Key.Kind != wantedKind))) continue;
            var criteriaIds = byRule.First().Criteria.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var bestPractice = criteriaIds.Length == 0;
            var criteria = criteriaIds.Where(inTarget.ContainsKey).Select(id => inTarget[id]).ToList();
            if (!bestPractice && criteria.Count == 0) continue; // only criteria outside the current target

            var guide = knowledge.Rule(byRule.Key.RuleId);
            var owner = guide?.Owner ?? criteria.FirstOrDefault()?.Owner ?? Owner.Developer;
            if (filter.Owner is { } wantedOwner && owner != wantedOwner) continue;
            if (filter.Principle is { } principle && !criteria.Any(c => c.Principle == principle)) continue;
            if (filter.Level is { } level && !criteria.Any(c => c.Level == level)) continue;
            if (filter.Criterion is { } criterion && !criteria.Any(c => c.Id == criterion)) continue;
            var impact = Worst(byRule.Select(f => f.Impact));
            if (filter.Impact is { } wantedImpact && !string.Equals(impact, wantedImpact, StringComparison.OrdinalIgnoreCase)) continue;

            var issuePages = byRule
                .Where(f => pages.ContainsKey(f.ScanPageId))
                .GroupBy(f => (pages[f.ScanPageId].ContentKey, pages[f.ScanPageId].Culture))
                .Select(byPage =>
                {
                    var page = pages[byPage.First().ScanPageId];
                    var nodes = byPage
                        .Select(f => new IssueNode(f.Id, f.Selector, f.Html, f.MediaKey, FalsePositiveFor(f, page, data.FalsePositives)?.Id))
                        .Where(n => filter.FalsePositives ? n.FalsePositiveId is not null : n.FalsePositiveId is null)
                        .DistinctBy(n => n.Selector) // the same element found at two viewports
                        .ToList();
                    return new IssuePage(page.ContentKey, page.Name, page.Culture, page.Url, nodes, viewports[(page.ContentKey, page.Culture)]);
                })
                .Where(p => p.Nodes.Count > 0)
                .ToList();
            if (issuePages.Count == 0) continue;

            var first = criteria.FirstOrDefault();
            groups.Add(new IssueGroup(
                byRule.Key.RuleId,
                byRule.First().Help,
                criteria.Select(c => c.Id).ToList(),
                criteria.Count > 0 ? criteria.Min(c => c.Level) : null,
                owner,
                impact,
                byRule.Key.Kind,
                bestPractice,
                issuePages.Count,
                issuePages.Sum(p => p.Nodes.Count),
                first?.Why ?? "Not a WCAG requirement; a recommended practice.",
                guide?.HowToFix ?? first?.HowToFix ?? byRule.First().Help,
                guide?.Caveats ?? first?.Caveats ?? string.Empty,
                first?.UnderstandingUrl ?? byRule.First().HelpUrl,
                withNodes ? issuePages : []));
        }

        return groups
            .OrderBy(g => g.Kind == FindingKind.Violation ? 0 : 1)
            .ThenBy(g => ImpactRank(g.Impact))
            .ThenByDescending(g => g.PageCount)
            .ThenBy(g => g.RuleId, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The scan the report is built from: the newest complete one, else the newest unfinished one (incomplete,
    /// or still running), so a site whose only scan was interrupted still gets a report.
    /// </summary>
    public static ScanEntity? ScanForReport(IReadOnlyList<ScanEntity> newestFirst)
        => newestFirst.FirstOrDefault(s => s.Status == ScanStatus.Complete) ?? newestFirst.FirstOrDefault();

    private async Task<ReportData> LoadAsync(Guid siteKey, ScanEntity? scan, bool withHtml = false)
    {
        var manual = await store.GetManualResultsAsync(siteKey);
        var falsePositives = await store.GetFalsePositivesAsync(siteKey);
        if (scan is null) return new ReportData([], [], manual, falsePositives);
        var findings = withHtml ? await store.GetFindingsAsync(scan.Id) : await store.GetFindingSummariesAsync(scan.Id);
        return new ReportData(await store.GetPagesAsync(scan.Id), findings, manual, falsePositives);
    }

    private List<CriterionInput> Inputs(WcagTarget target, ReportData data)
    {
        var pages = data.Pages.ToDictionary(p => p.Id);
        var openViolations = data.Findings
            .Where(f => f.Kind == FindingKind.Violation)
            .Where(f => !pages.TryGetValue(f.ScanPageId, out var page) || FalsePositiveFor(f, page, data.FalsePositives) is null)
            .SelectMany(f => f.Criteria.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());
        var passed = data.Pages
            .SelectMany(p => (p.PassedCriteria ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet();
        var manual = data.Manual.ToDictionary(m => m.CriterionId, m => m.Status);

        return knowledge.For(target)
            .Select(c => new CriterionInput(c, openViolations.GetValueOrDefault(c.Id), passed.Contains(c.Id), manual.GetValueOrDefault(c.Id)))
            .ToList();
    }

    private static FalsePositiveEntity? FalsePositiveFor(FindingEntity f, ScanPageEntity page, IReadOnlyList<FalsePositiveEntity> falsePositives)
        => falsePositives.FirstOrDefault(fp =>
            fp.RuleId == f.RuleId
            && fp.ContentKey == page.ContentKey
            && (fp.Selector is null || fp.Selector == f.Selector));

    private static string? Worst(IEnumerable<string?> impacts)
        => impacts.Where(i => i is not null).OrderBy(ImpactRank).FirstOrDefault();

    private static int ImpactRank(string? impact)
    {
        var index = Array.IndexOf(ImpactOrder, impact);
        return index < 0 ? ImpactOrder.Length : index;
    }

    private sealed record ReportData(
        IReadOnlyList<ScanPageEntity> Pages,
        IReadOnlyList<FindingEntity> Findings,
        IReadOnlyList<ManualResultEntity> Manual,
        IReadOnlyList<FalsePositiveEntity> FalsePositives);
}
