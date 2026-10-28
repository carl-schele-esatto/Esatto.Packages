using Esatto.Umbraco.Backoffice.Accessibility.Knowledge;
using Esatto.Umbraco.Backoffice.Accessibility.Persistence;
using Esatto.Umbraco.Backoffice.Accessibility.Reporting;
using Esatto.Umbraco.Backoffice.Accessibility.Settings;

namespace Esatto.Umbraco.Backoffice.Accessibility.Scanning;

/// <summary>
/// Records scans as the dashboard runs them: start, one page at a time, finish (or resume).
/// Scanning itself happens in the editor's browser; this only stores what it posts.
/// </summary>
public sealed class ScanService(IAccessibilityStore store, SettingsService settings, MediaResolver media, ReportService report, TimeProvider time)
{
    public const int MaxHtmlLength = 1024;

    /// <summary>axe rules whose element is the image itself, so its <c>src</c> identifies the media item.</summary>
    private static readonly HashSet<string> ImageRules = ["image-alt", "role-img-alt", "input-image-alt"];

    public async Task<int> StartAsync(Guid siteKey, bool mobile, int pagesTotal, string userName)
    {
        // A scan left running (tab closed, browser crashed) is over once a new one starts: keep what it
        // scanned as incomplete, so it shows as such and can still be reported on.
        foreach (var abandoned in (await store.GetScansAsync(siteKey, Retention.Keep)).Where(s => s.Status == ScanStatus.Running))
        {
            await FinishAsync(abandoned.Id, complete: false);
        }

        var target = (await settings.GetAsync()).Target;
        return await store.CreateScanAsync(new ScanEntity
        {
            SiteKey = siteKey,
            StartedUtc = time.GetUtcNow().UtcDateTime,
            Version = target.Version.ToString(),
            Level = target.Level.ToString(),
            Mobile = mobile,
            UserName = userName,
            Status = ScanStatus.Running,
            PagesTotal = pagesTotal,
        });
    }

    /// <summary>Records one page. Posting the same page again (a client retry) replaces it rather than adding a second row.</summary>
    public Task AddPageAsync(int scanId, PageResult result)
    {
        var (page, findings) = ToEntities(scanId, result);
        return store.SavePageAsync(page, findings);
    }

    /// <summary>
    /// Closes the scan. Counters and document links are computed here from the stored pages, not
    /// incremented per page, because the dashboard posts pages in parallel.
    /// </summary>
    public async Task<ScanSummary> FinishAsync(int scanId, bool complete)
    {
        var scan = await store.GetScanAsync(scanId) ?? throw new KeyNotFoundException($"Scan {scanId} not found.");
        var pages = await store.GetPagesAsync(scanId);

        scan.Status = complete ? ScanStatus.Complete : ScanStatus.Incomplete;
        scan.FinishedUtc = time.GetUtcNow().UtcDateTime;
        scan.PagesDone = pages.Count(p => p.Status == PageStatus.Ok);
        scan.PagesSkipped = pages.Count(p => p.Status == PageStatus.Skipped);
        scan.PagesFailed = pages.Count(p => p.Status == PageStatus.Failed);
        var links = pages
            .SelectMany(p => (p.DocumentLinks ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        scan.DocumentLinks = links.Count > 0 ? string.Join('\n', links) : null;
        await store.UpdateScanAsync(scan);
        await SnapshotScoreAsync(scan);

        var siteScans = await store.GetScansAsync(scan.SiteKey, int.MaxValue);
        await store.PruneScansAsync(Retention.ScansToPrune(siteScans.Select(s => (s.Id, s.StartedUtc))));

        return Summary(scan);
    }

    /// <summary>
    /// Pages already recorded in the scan, for resuming an interrupted one. Failed pages aren't listed, so a
    /// resume tries them again (often they failed only because the editor left while they were loading).
    /// </summary>
    public async Task<IReadOnlyList<DonePage>> DoneAsync(int scanId)
        => (await store.GetPagesAsync(scanId))
            .Where(p => p.Status != PageStatus.Failed)
            .Select(p => new DonePage(p.ContentKey, p.Culture, p.Viewport))
            .ToList();

    /// <summary>Replaces one page's result in the scan the report is built from ("Re-check this page").</summary>
    public async Task<int> RecheckAsync(Guid siteKey, PageResult result)
    {
        var reported = ReportService.ScanForReport(await store.GetScansAsync(siteKey, Retention.Keep))
            ?? throw new InvalidOperationException("There is no scan to update. Run a full scan first.");
        var (page, findings) = ToEntities(reported.Id, result);
        await store.SavePageAsync(page, findings);
        await SnapshotScoreAsync(reported);
        return reported.Id;
    }

    /// <summary>Stores the scan's score at this moment, for the history list.</summary>
    private async Task SnapshotScoreAsync(ScanEntity scan)
    {
        var score = await report.ComputeScoreAsync(scan.Id);
        scan.Score = score.Percent;
        scan.PassCount = score.Pass;
        scan.FailCount = score.Fail;
        scan.UnverifiedCount = score.NotVerified;
        scan.NaCount = score.NotApplicable;
        await store.UpdateScanAsync(scan);
    }

    public static ScanSummary Summary(ScanEntity s) => new(
        s.Id,
        s.StartedUtc,
        s.FinishedUtc,
        s.Status,
        new WcagTarget(Enum.Parse<WcagVersion>(s.Version), Enum.Parse<WcagLevel>(s.Level)).ToString(),
        s.Score,
        s.PagesTotal,
        s.PagesDone,
        s.PagesSkipped,
        s.PagesFailed,
        s.UserName,
        s.Mobile);

    private (ScanPageEntity Page, List<FindingEntity> Findings) ToEntities(int scanId, PageResult r)
    {
        // These URLs end up as links in the dashboard, and anyone with Content access can post results:
        // only http(s) is stored, so a javascript: or data: link can never reach another user's click.
        if (!string.IsNullOrEmpty(r.Url) && !IsHttp(r.Url))
            throw new ArgumentException("A page result's URL must be an absolute http(s) URL.");
        if (string.IsNullOrEmpty(r.Url) && r.Status == PageStatus.Ok)
            throw new ArgumentException("A scanned page needs its URL.");

        var page = new ScanPageEntity
        {
            ScanId = scanId,
            ContentKey = r.ContentKey,
            Culture = r.Culture ?? string.Empty,
            Name = Truncate(r.Name ?? string.Empty, 256),
            Url = Truncate(r.Url ?? string.Empty, 2048),
            Viewport = r.Viewport,
            Status = r.Status is PageStatus.Ok or PageStatus.Skipped or PageStatus.Failed ? r.Status : PageStatus.Failed,
            Reason = r.Reason is null ? null : Truncate(r.Reason, 512),
            PassedCriteria = string.Join(',', AxeTags.CriteriaFromTags((r.Passes ?? []).SelectMany(p => p.Tags ?? []))) is { Length: > 0 } passed ? passed : null,
            DocumentLinks = r.DocumentLinks is { Count: > 0 } ? string.Join('\n', r.DocumentLinks.Distinct()) : null,
        };

        var findings = (r.Rules ?? [])
            .SelectMany(rule => rule.Nodes.Select(node => new FindingEntity
            {
                ScanId = scanId,
                RuleId = rule.RuleId,
                Help = Truncate(rule.Help ?? string.Empty, 256),
                HelpUrl = IsHttp(rule.HelpUrl) ? Truncate(rule.HelpUrl!, 512) : string.Empty,
                Criteria = string.Join(',', AxeTags.CriteriaFromTags(rule.Tags ?? [])),
                Impact = rule.Impact,
                Kind = rule.Kind == FindingKind.Incomplete ? FindingKind.Incomplete : FindingKind.Violation,
                Selector = Truncate(node.Selector ?? string.Empty, 1024),
                Html = Truncate(node.Html ?? string.Empty, MaxHtmlLength),
                MediaKey = ImageRules.Contains(rule.RuleId) ? media.Resolve(node.Html ?? string.Empty) : null,
            }))
            .ToList();

        return (page, findings);
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private static bool IsHttp(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme is "http" or "https";
}
