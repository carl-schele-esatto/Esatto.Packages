using Esatto.Umbraco.Backoffice.Seo.Checks;
using Esatto.Umbraco.Backoffice.Seo.Evaluation;
using Esatto.Umbraco.Backoffice.Seo.Facts;
using Esatto.Umbraco.Backoffice.Seo.Persistence;
using Esatto.Umbraco.Backoffice.Seo.Reporting;

namespace Esatto.Umbraco.Backoffice.Seo.Scanning;

/// <summary>
/// Re-fetches one page of the latest finished scan and updates that scan in place (facts, its links,
/// score), so an editor sees a fix without waiting for the next full scan.
/// </summary>
/// <remarks>
/// All network requests happen first and may be cancelled (the editor navigates away); the writes come
/// after, uncancellable and under <see cref="ScanRowLock"/>, so a re-check is either fully applied or not at all.
/// </remarks>
public sealed class RecheckService(ISeoStore store, PageFetcher fetcher, LinkChecker linkChecker, SnapshotLoader loader, ScanCoordinator coordinator,
    ReportCache cache, ScanRowLock rowLock)
{
    /// <exception cref="InvalidOperationException">A scan is running, or the site has no finished scan.</exception>
    /// <exception cref="KeyNotFoundException">The page isn't in the latest scan.</exception>
    public async Task RecheckAsync(Guid siteKey, Guid contentKey, string culture, CancellationToken ct)
    {
        if (coordinator.IsRunning(siteKey)) throw new InvalidOperationException("A scan is running. Re-check when it has finished.");

        var latest = (await store.GetScansAsync(siteKey, 20)).FirstOrDefault(s => s.Status == ScanStatus.Complete)
            ?? throw new InvalidOperationException("There is no finished scan to update. Run a scan first.");
        var pages = await store.GetPagesAsync(latest.Id);
        var row = Find(pages, contentKey, culture) ?? throw new KeyNotFoundException("The page isn't part of the latest scan.");
        var trusted = SnapshotLoader.Split(latest.TrustedHosts).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Network: this page, then its link targets (all asked again; everything else keeps the scan's answers).
        var hosts = ScanHosts.From(pages.Select(p => p.Url));
        var facts = PageParser.Parse(await fetcher.FetchAsync(row.Url, row.Culture, ct, trustedHosts: trusted), hosts);
        var self = new Dictionary<string, LinkStatus> { [UrlNormalizer.Normalize(row.Url)] = LinkChecker.FromFacts(facts) };
        var targets = facts.InternalLinks.Concat(facts.Hreflangs.Select(h => h.Href)).Select(UrlNormalizer.Normalize).ToList();
        var fresh = await linkChecker.CheckAsync(targets, self, 2, ct, trusted);

        // Writes: the row as it is now (an ignore may have re-scored it meanwhile), merged with what was found.
        using var _ = await rowLock.AcquireAsync();
        var scan = await store.GetScanAsync(latest.Id) ?? throw new InvalidOperationException("The scan disappeared.");
        row = Find(await store.GetPagesAsync(scan.Id), contentKey, culture) ?? throw new KeyNotFoundException("The page isn't part of the latest scan.");
        var ok = facts is { StatusCode: 200, IsHtml: true };
        row.Status = ok ? PageStatus.Ok : PageStatus.Failed;
        row.Reason = ok ? null : StatusError.Describe(facts);
        row.FactsJson = SnapshotLoader.Write(facts);
        await store.UpdatePageAsync(row);

        var links = SnapshotLoader.Read<Dictionary<string, LinkStatus>>(scan.LinksJson) ?? [];
        foreach (var (url, status) in fresh) links[url] = status;
        scan.LinksJson = SnapshotLoader.Write(links);

        var snapshot = await loader.LoadAsync(scan.Id) ?? throw new InvalidOperationException("The scan disappeared.");
        // LoadAsync reads the stored links; use the fresh ones.
        snapshot = ScanSnapshot.Build(snapshot.SiteKey, snapshot.SiteCultures, snapshot.Pages.Select(p => (p.Page, p.Facts)),
            snapshot.Probe, links, snapshot.IntentionallyHidden);
        scan.PagesFailed = (await store.GetPagesAsync(scan.Id)).Count(p => p.Status == PageStatus.Failed);
        ScanResults.Apply(scan, snapshot, await store.GetIgnoresAsync(siteKey));
        await store.UpdateScanAsync(scan);
        cache.Invalidate(); // same scan id, new results
    }

    private static PageEntity? Find(IEnumerable<PageEntity> pages, Guid contentKey, string? culture)
        => pages.FirstOrDefault(p => p.ContentKey == contentKey && string.Equals(p.Culture, culture ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            && p.Status != PageStatus.Skipped);
}
