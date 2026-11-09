using Esatto.Umbraco.Backoffice.Seo.Checks;
using Esatto.Umbraco.Backoffice.Seo.Persistence;
using Esatto.Umbraco.Backoffice.Seo.Scanning;

namespace Esatto.Umbraco.Backoffice.Seo.Reporting;

/// <summary>
/// "Don't report this": per page × check (optionally one culture) or a whole check. Re-applied to every
/// later scan. Changing an ignore re-scores the latest scan, so the Overview and the trend agree.
/// </summary>
public sealed class IgnoreService(ISeoStore store, SnapshotLoader loader, ReportCache cache, ScanRowLock rowLock)
{
    public const int MaxNote = 1024;

    /// <exception cref="ArgumentException">Unknown check, or no note.</exception>
    public async Task<int> AddAsync(Guid siteKey, string checkId, Guid? contentKey, string? culture, string note, string userName)
    {
        if (!CheckCatalog.Info.ContainsKey(checkId)) throw new ArgumentException($"Unknown check '{checkId}'.", nameof(checkId));
        if (string.IsNullOrWhiteSpace(note)) throw new ArgumentException("Say why this is ignored.", nameof(note));

        var id = await store.AddIgnoreAsync(new IgnoreEntity
        {
            SiteKey = siteKey,
            CheckId = checkId,
            ContentKey = contentKey,
            Culture = contentKey is null || string.IsNullOrWhiteSpace(culture) ? null : culture,
            Note = note.Trim() is var n && n.Length > MaxNote ? n[..MaxNote] : note.Trim(),
            UserName = userName,
            CreatedUtc = DateTime.UtcNow,
        });
        await RescoreLatestAsync(siteKey);
        return id;
    }

    public async Task RemoveAsync(int id)
    {
        var ignore = await store.GetIgnoreAsync(id);
        if (ignore is null) return;
        await store.RemoveIgnoreAsync(id);
        await RescoreLatestAsync(ignore.SiteKey);
    }

    public Task<IReadOnlyList<IgnoreEntity>> ListAsync(Guid siteKey) => store.GetIgnoresAsync(siteKey);

    private async Task RescoreLatestAsync(Guid siteKey)
    {
        cache.Invalidate();
        using var _ = await rowLock.AcquireAsync(); // a re-check may be updating the same row
        var latest =(await store.GetScansAsync(siteKey, 20)).FirstOrDefault(s => s.Status == ScanStatus.Complete);
        // The list holds summaries; updating needs the whole row, or its probe and links would be wiped.
        if (latest is null || await store.GetScanAsync(latest.Id) is not { } scan || await loader.LoadAsync(scan.Id) is not { } snapshot) return;
        ScanResults.Apply(scan, snapshot, await store.GetIgnoresAsync(siteKey));
        await store.UpdateScanAsync(scan);
    }
}
