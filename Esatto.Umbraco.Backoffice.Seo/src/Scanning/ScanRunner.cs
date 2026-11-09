using Esatto.Umbraco.Backoffice.Seo.Checks;
using Esatto.Umbraco.Backoffice.Seo.Evaluation;
using Esatto.Umbraco.Backoffice.Seo.Facts;
using Esatto.Umbraco.Backoffice.Seo.Pages;
using Esatto.Umbraco.Backoffice.Seo.Persistence;
using Esatto.Umbraco.Backoffice.Seo.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Esatto.Umbraco.Backoffice.Seo.Scanning;

/// <summary>Live progress of a running scan.</summary>
public sealed record ScanProgress(int ScanId, int Done, int Total, string? CurrentUrl);

/// <summary>
/// Runs one scan of one site: list pages, fetch and parse them, probe the site, check links, score, and
/// prune old scans. Each page is stored as soon as it's done, so a restart leaves an incomplete scan, not nothing.
/// </summary>
public sealed class ScanRunner(
    IPageSource pageSource,
    SettingsService settings,
    SeoEnvironment environment,
    IConfiguration configuration,
    ISeoStore store,
    PageFetcher fetcher,
    LinkChecker linkChecker,
    SiteProber prober,
    ILogger<ScanRunner> logger)
{
    public const int AbortAfterConsecutiveFailures = 10;
    public const int KeepScansWithPages = 10;
    private const string ApplicationUrlKey = "Umbraco:CMS:WebRouting:UmbracoApplicationUrl";

    /// <summary>Serialises database writes from the parallel workers (SQLite allows one writer).</summary>
    private readonly SemaphoreSlim _write = new(1, 1);

    /// <param name="ct">Stops the scan: a user's cancel, or <paramref name="stopping"/>.</param>
    /// <param name="stopping">The application is shutting down: the scan ends "incomplete" rather than "cancelled".</param>
    public async Task<int> RunAsync(Guid siteKey, string trigger, string userName, Uri? requestBase, IProgress<ScanProgress>? progress, CancellationToken ct,
        CancellationToken stopping = default)
    {
        // The row comes first, so whatever goes wrong afterwards is visible on the Overview.
        var scan = new ScanEntity { SiteKey = siteKey, StartedUtc = DateTime.UtcNow, Trigger = trigger, UserName = userName, Status = ScanStatus.Running };
        scan.Id = await store.CreateScanAsync(scan);
        try
        {
            return await RunScanAsync(scan, requestBase, progress, ct, stopping);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && stopping.IsCancellationRequested)
        {
            // Services are going away under the scan; that's the shutdown, not a fault.
            return await FinishAsync(scan, ScanStatus.Incomplete, Interrupted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "SEO Health scan {ScanId} failed", scan.Id);
            await FinishAsync(scan, ScanStatus.Failed, $"The scan stopped with an unexpected error: {ex.Message}");
            throw;
        }
    }

    private const string Interrupted = "The site shut down or restarted during the scan.";

    private async Task<int> RunScanAsync(ScanEntity scan, Uri? requestBase, IProgress<ScanProgress>? progress, CancellationToken ct, CancellationToken stopping)
    {
        var siteKey = scan.SiteKey;
        var s = (await settings.GetAsync()).Settings;
        var configuredBase = BaseUrlResolver.Resolve(s.BaseUrl, configuration[ApplicationUrlKey], null);
        var baseUri = configuredBase ?? requestBase;
        var candidates = await pageSource.GetCandidatesAsync(siteKey);
        var list = PageListBuilder.Build(candidates, s.ExcludePaths, baseUri);
        var toFetch = list.Where(p => p.SkipReason is null).ToList();

        // Configured headers (the basic-auth secret) only go to hosts the site itself declares: bound domains
        // and a configured base URL. A host taken from the backoffice request's Host header is not trusted.
        var trusted = candidates
            .Select(c => Uri.TryCreate(c.Url, UriKind.Absolute, out var u) ? u.Host : null)
            .Append(configuredBase?.Host)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        scan.TrustedHosts = Truncate(string.Join(",", trusted), 1024);

        scan.PagesTotal = toFetch.Count;
        scan.PagesSkipped = list.Count - toFetch.Count;
        scan.Cultures = string.Join(",", await pageSource.GetSiteCulturesAsync(siteKey));
        await store.UpdateScanAsync(scan);
        progress?.Report(new ScanProgress(scan.Id, 0, toFetch.Count, null));

        if (toFetch.Count == 0)
        {
            var noUrl = list.Any(p => p.SkipReason == SkipReasons.NoUrl);
            return await FinishAsync(scan, ScanStatus.Failed, noUrl && baseUri is null
                ? "Pages have relative URLs (no domain is bound in Culture and Hostnames) and no base URL is known. Set a base URL in Settings, or Umbraco:CMS:WebRouting:UmbracoApplicationUrl."
                : "There are no published pages with a template to scan.");
        }

        // Stored only for scans that run: a scan that can't start shouldn't write a row per page every night.
        foreach (var skipped in list.Where(p => p.SkipReason is not null))
            await store.AddPageAsync(Row(scan.Id, skipped.Ref, PageStatus.Skipped, skipped.SkipReason, null));

        var hosts = ScanHosts.From(toFetch.Select(p => p.Ref.Url));
        using var abort = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var done = 0;
        var consecutiveFailures = 0;
        var anyLoaded = 0;
        string? lastError = null;

        try
        {
            await Parallel.ForEachAsync(toFetch, new ParallelOptions { MaxDegreeOfParallelism = s.Parallelism, CancellationToken = abort.Token }, async (page, token) =>
            {
                progress?.Report(new ScanProgress(scan.Id, Volatile.Read(ref done), toFetch.Count, page.Ref.Url));
                var (pageRef, fetched) = HttpsUpgrade(page.Ref, await fetcher.FetchAsync(page.Ref.Url, page.Ref.Culture, token, trustedHosts: trusted));
                var facts = PageParser.Parse(fetched, hosts);
                var ok = facts is { StatusCode: 200, IsHtml: true };

                await WriteAsync(() => store.AddPageAsync(Row(scan.Id, pageRef, ok ? PageStatus.Ok : PageStatus.Failed,
                    ok ? null : StatusError.Describe(facts), SnapshotLoader.Write(facts))));

                if (ok)
                {
                    Interlocked.Exchange(ref anyLoaded, 1);
                }
                else
                {
                    lastError = StatusError.Describe(facts);
                    // Only while nothing has loaded: then the server can't reach the site at all. Once one page has
                    // loaded, failing pages (a template throwing 500s on 20 siblings) are findings, not a reason to stop.
                    // Cancel(), not CancelAsync(): the loop's linked token must be cancelled before the next page starts.
                    if (Interlocked.Increment(ref consecutiveFailures) >= AbortAfterConsecutiveFailures && Volatile.Read(ref anyLoaded) == 0) abort.Cancel();
                }

                // Not inside the Report call: with no progress listener, ?. would skip evaluating the increment.
                var doneNow = Interlocked.Increment(ref done);
                progress?.Report(new ScanProgress(scan.Id, doneNow, toFetch.Count, page.Ref.Url));
                if (s.DelayMs > 0) await Task.Delay(s.DelayMs, token);
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            scan.PagesDone = done;
            return await StoppedAsync(scan, stopping);
        }
        catch (OperationCanceledException)
        {
            scan.PagesDone = done;
            var host = new Uri(toFetch[0].Ref.Url).Host;
            return await FinishAsync(scan, ScanStatus.Failed,
                $"{AbortAfterConsecutiveFailures} pages in a row couldn't be loaded (last: {lastError}). Check that this server can reach {host}: "
                + "the base URL in Settings, firewalls or Cloudflare rules, or basic authentication (Umbraco:CMS:BasicAuth:SharedSecret).");
        }

        try
        {
            var pages = await store.GetPagesAsync(scan.Id);
            // The stored addresses: a page may have moved to https (HttpsUpgrade).
            var origins = pages.Where(p => p.Status != PageStatus.Skipped).Select(p => new Uri(p.Url)).ToList();
            var probe = await prober.ProbeAsync(origins, ct, trusted, s.DelayMs);

            var snapshotForLinks = SnapshotLoader.Build(scan, pages, environment.IntentionallyHidden);
            var links = await linkChecker.CheckAsync(LinkTargets(snapshotForLinks), KnownStatuses(snapshotForLinks), s.Parallelism, ct, trusted, s.DelayMs);

            scan.ProbeJson = SnapshotLoader.Write(probe);
            scan.LinksJson = SnapshotLoader.Write(links);
            scan.PagesDone = done;
            scan.PagesFailed = pages.Count(p => p.Status == PageStatus.Failed);

            ScanResults.Apply(scan, SnapshotLoader.Build(scan, pages, environment.IntentionallyHidden), await store.GetIgnoresAsync(siteKey));
            return await FinishAsync(scan, ScanStatus.Complete, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return await StoppedAsync(scan, stopping);
        }
    }

    private Task<int> StoppedAsync(ScanEntity scan, CancellationToken stopping)
        => stopping.IsCancellationRequested ? FinishAsync(scan, ScanStatus.Incomplete, Interrupted) : FinishAsync(scan, ScanStatus.Cancelled, null);

    /// <summary>
    /// Umbraco builds a domain bound without a scheme ("esatto.se") as http:// outside a request (unless
    /// Umbraco:CMS:WebRouting:UmbracoApplicationUrl says https), so a scan may ask for http://esatto.se/om/ and be sent to
    /// https://esatto.se/om/. A page whose only redirect is that upgrade is recorded at its https address, as a visitor
    /// following the site's own links would reach it.
    /// </summary>
    internal static (PageRef Ref, FetchResult Fetch) HttpsUpgrade(PageRef page, FetchResult r)
    {
        if (r.RedirectChain.Count != 1
            || !Uri.TryCreate(page.Url, UriKind.Absolute, out var from) || from.Scheme != Uri.UriSchemeHttp
            || !Uri.TryCreate(r.FinalUrl, UriKind.Absolute, out var to) || to.Scheme != Uri.UriSchemeHttps
            || !from.IsDefaultPort || !to.IsDefaultPort
            || !string.Equals(from.Host, to.Host, StringComparison.OrdinalIgnoreCase)
            || from.PathAndQuery != to.PathAndQuery)
            return (page, r);

        return (page with { Url = to.ToString() }, r with { RequestedUrl = to.ToString(), RedirectChain = [] });
    }

    /// <summary>Every link the checks will look up: internal links and hreflang targets.</summary>
    internal static IEnumerable<string> LinkTargets(ScanSnapshot s)
        => s.Pages.Where(p => p.Facts is not null)
            .SelectMany(p => p.Facts!.InternalLinks.Concat(p.Facts.Hreflangs.Select(h => h.Href)));

    /// <summary>Pages already fetched answer for themselves (by requested and final URL).</summary>
    internal static Dictionary<string, LinkStatus> KnownStatuses(ScanSnapshot s)
    {
        var known = new Dictionary<string, LinkStatus>(StringComparer.Ordinal);
        foreach (var p in s.Pages.Where(p => p.Facts is not null))
        {
            var status = LinkChecker.FromFacts(p.Facts!);
            known.TryAdd(UrlNormalizer.Normalize(p.Page.Url), status);
            if (p.Facts!.FinalUrl.Length > 0) known.TryAdd(UrlNormalizer.Normalize(p.Facts.FinalUrl), status with { Hops = 0 });
        }

        return known;
    }

    private async Task<int> FinishAsync(ScanEntity scan, string status, string? message)
    {
        scan.Status = status;
        scan.Message = message is null ? null : Truncate(message, MaxMessage);
        scan.FinishedUtc = DateTime.UtcNow;
        await store.UpdateScanAsync(scan);
        if (status != ScanStatus.Complete) logger.LogWarning("SEO Health scan {ScanId} ended {Status}: {Message}", scan.Id, status, message);

        // Every finished scan, whatever its status, so failed nightly runs don't pile up either.
        var older = (await store.GetScansAsync(scan.SiteKey, 1000)).Skip(KeepScansWithPages).Select(x => x.Id);
        await store.PruneScansAsync(older);
        return scan.Id;
    }

    /// <summary>Length of <see cref="ScanEntity.Message"/>.</summary>
    private const int MaxMessage = 1024;

    private async Task WriteAsync(Func<Task> write)
    {
        await _write.WaitAsync();
        try
        {
            await write();
        }
        finally
        {
            _write.Release();
        }
    }

    internal static PageEntity Row(int scanId, PageRef r, string status, string? reason, string? factsJson) => new()
    {
        ScanId = scanId,
        ContentKey = r.ContentKey,
        Culture = r.Culture,
        Name = Truncate(r.Name, 256),
        Url = Truncate(r.Url, 2048),
        IsRoot = r.IsRoot,
        IsVariant = r.IsVariant,
        OtherCultures = string.Join(",", r.OtherCultures),
        Status = status,
        Reason = reason is null ? null : Truncate(reason, 512),
        FactsJson = factsJson,
    };

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
