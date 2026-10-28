namespace Esatto.Umbraco.Backoffice.Accessibility.Persistence;

/// <summary>Everything the package reads and writes in the database, behind one seam so services stay testable.</summary>
public interface IAccessibilityStore
{
    /// <summary>The stored settings JSON, or null when nothing has been saved yet.</summary>
    Task<string?> GetSettingsJsonAsync();

    Task SaveSettingsJsonAsync(string json);

    /// <summary>Inserts the scan and returns its id.</summary>
    Task<int> CreateScanAsync(ScanEntity scan);

    Task<ScanEntity?> GetScanAsync(int id);

    /// <summary>The site's scans, newest first.</summary>
    Task<IReadOnlyList<ScanEntity>> GetScansAsync(Guid siteKey, int take);

    Task UpdateScanAsync(ScanEntity scan);

    /// <summary>
    /// Stores a scanned page and its findings in one transaction, replacing any earlier result for the same
    /// page (content, culture and viewport) in the scan: a retried post and "Re-check this page" both land here.
    /// </summary>
    Task SavePageAsync(ScanPageEntity page, IReadOnlyList<FindingEntity> findings);

    Task<IReadOnlyList<ScanPageEntity>> GetPagesAsync(int scanId);

    Task<IReadOnlyList<FindingEntity>> GetFindingsAsync(int scanId);

    /// <summary>The scan's findings without their HTML snippets: enough for scores, counts and filters.</summary>
    Task<IReadOnlyList<FindingEntity>> GetFindingSummariesAsync(int scanId);

    /// <summary>Deletes the pages and findings of these scans; their summary rows stay.</summary>
    Task PruneScansAsync(IEnumerable<int> scanIds);

    Task<IReadOnlyList<FalsePositiveEntity>> GetFalsePositivesAsync(Guid siteKey);

    /// <summary>Inserts the false positive and returns its id.</summary>
    Task<int> AddFalsePositiveAsync(FalsePositiveEntity falsePositive);

    Task DeleteFalsePositiveAsync(int id);

    Task<IReadOnlyList<ManualResultEntity>> GetManualResultsAsync(Guid siteKey);

    /// <summary>Inserts or updates the result for (site, criterion).</summary>
    Task UpsertManualResultAsync(ManualResultEntity result);

    Task DeleteManualResultAsync(Guid siteKey, string criterionId);
}
