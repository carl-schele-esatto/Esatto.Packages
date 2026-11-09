namespace Esatto.Umbraco.Backoffice.Seo.Persistence;

/// <summary>The package's database access. One implementation over Umbraco's database; an in-memory one in tests.</summary>
public interface ISeoStore
{
    Task<string?> GetSettingsJsonAsync();

    Task SaveSettingsJsonAsync(string json);

    /// <summary>Inserts the scan and returns its id.</summary>
    Task<int> CreateScanAsync(ScanEntity scan);

    Task UpdateScanAsync(ScanEntity scan);

    Task<ScanEntity?> GetScanAsync(int id);

    /// <summary>The site's scans, newest first — summaries only: <see cref="ScanEntity.ProbeJson"/> and
    /// <see cref="ScanEntity.LinksJson"/> are not loaded (use <see cref="GetScanAsync"/> for one full row).</summary>
    Task<IReadOnlyList<ScanEntity>> GetScansAsync(Guid siteKey, int take);

    /// <summary>Inserts the page row and returns its id.</summary>
    Task<int> AddPageAsync(PageEntity page);

    Task UpdatePageAsync(PageEntity page);

    Task<IReadOnlyList<PageEntity>> GetPagesAsync(int scanId);

    /// <summary>Reduces these scans to their summary row (for the trend): page rows deleted, JSON columns cleared.</summary>
    Task PruneScansAsync(IEnumerable<int> scanIds);

    Task<IReadOnlyList<IgnoreEntity>> GetIgnoresAsync(Guid siteKey);

    Task<int> AddIgnoreAsync(IgnoreEntity ignore);

    Task<IgnoreEntity?> GetIgnoreAsync(int id);

    Task RemoveIgnoreAsync(int id);

    /// <summary>At startup: scans left running by a restart become incomplete.</summary>
    Task MarkRunningAsIncompleteAsync();
}
