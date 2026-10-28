using Esatto.Umbraco.Backoffice.Accessibility.Knowledge;
using Esatto.Umbraco.Backoffice.Accessibility.Persistence;
using Esatto.Umbraco.Backoffice.Accessibility.Reporting;
using Esatto.Umbraco.Backoffice.Accessibility.Settings;

namespace Esatto.Umbraco.Backoffice.Accessibility.Manual;

/// <summary>
/// One row of the Manual checklist. <see cref="AutomatedFindings"/> counts what the latest scan found for
/// the criterion (failures and "needs review"); <see cref="DocumentLinks"/> is the number of linked
/// documents, shown on the criteria that also apply to documents. <see cref="ScannerChecked"/>: the latest scan ran
/// rules for the criterion and found no violations, so a person only has to confirm it.
/// </summary>
public sealed record ChecklistItem(
    Criterion Criterion,
    string? Status,
    string? Note,
    string? UserName,
    DateTime? UpdatedUtc,
    bool NeedsRecheck,
    int AutomatedFindings,
    int DocumentLinks,
    bool ScannerChecked);

/// <summary>The Manual checklist: every criterion of the target a machine can't fully check, with saved results.</summary>
public sealed class ManualService(IAccessibilityStore store, KnowledgeBase knowledge, SettingsService settings, TimeProvider time)
{
    /// <summary>The note column's length.</summary>
    public const int MaxNoteLength = 2048;

    /// <summary>Criteria where linked documents (PDF, Word…) must be checked too.</summary>
    private static readonly HashSet<string> DocumentCriteria = ["1.1.1", "1.3.1"];

    public async Task<IReadOnlyList<ChecklistItem>> GetChecklistAsync(Guid siteKey)
    {
        var effective = await settings.GetAsync();
        var results = (await store.GetManualResultsAsync(siteKey)).ToDictionary(r => r.CriterionId);
        var staleBefore = time.GetUtcNow().AddMonths(-effective.Settings.Scan.RecheckMonths).UtcDateTime;

        var scan = ReportService.ScanForReport(await store.GetScansAsync(siteKey, Scanning.Retention.Keep));
        var findings = scan is null ? [] : await store.GetFindingSummariesAsync(scan.Id);
        var findingsPerCriterion = findings
            .SelectMany(f => f.Criteria.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());
        var documentLinks = scan?.DocumentLinks?.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
        var violated = findings
            .Where(f => f.Kind == FindingKind.Violation)
            .SelectMany(f => f.Criteria.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet();
        var passed = scan is null ? [] : (await store.GetPagesAsync(scan.Id))
            .SelectMany(p => (p.PassedCriteria ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet();

        return knowledge.For(effective.Target)
            .Where(c => !c.FullyAutomatable)
            .Select(c =>
            {
                results.TryGetValue(c.Id, out var r);
                return new ChecklistItem(
                    c,
                    r?.Status,
                    r?.Note,
                    r?.UserName,
                    r?.UpdatedUtc,
                    r is not null && r.UpdatedUtc < staleBefore,
                    findingsPerCriterion.GetValueOrDefault(c.Id),
                    DocumentCriteria.Contains(c.Id) ? documentLinks : 0,
                    passed.Contains(c.Id) && !violated.Contains(c.Id));
            })
            .ToList();
    }

    public Task SetAsync(Guid siteKey, string criterionId, string status, string? note, string userName)
    {
        if (knowledge.Find(criterionId) is null) throw new ArgumentException($"Unknown WCAG criterion '{criterionId}'.");
        if (!ManualStatus.IsValid(status)) throw new ArgumentException("Status must be pass, fail or na.");
        if (note?.Trim().Length > MaxNoteLength) throw new ArgumentException($"The note can be at most {MaxNoteLength} characters.");

        return store.UpsertManualResultAsync(new ManualResultEntity
        {
            SiteKey = siteKey,
            CriterionId = criterionId,
            Status = status,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            UserName = userName,
            UpdatedUtc = time.GetUtcNow().UtcDateTime,
        });
    }

    /// <summary>Removes the result, so the criterion is "not checked" again (for a result recorded by mistake).</summary>
    public Task ClearAsync(Guid siteKey, string criterionId) => store.DeleteManualResultAsync(siteKey, criterionId);
}
