using Esatto.Umbraco.Backoffice.Seo.Evaluation;
using Esatto.Umbraco.Backoffice.Seo.Facts;

namespace Esatto.Umbraco.Backoffice.Seo.Scanning;

/// <summary>Checks link targets with HEAD requests, each distinct URL once; pages already fetched aren't asked again.</summary>
public sealed class LinkChecker(PageFetcher fetcher)
{
    /// <param name="delayMs">Pause after each request (the scan's "delay between requests").</param>
    /// <returns><paramref name="known"/> plus a status for every other URL, keyed by normalised URL.</returns>
    public async Task<IReadOnlyDictionary<string, LinkStatus>> CheckAsync(
        IEnumerable<string> urls, IReadOnlyDictionary<string, LinkStatus> known, int parallelism, CancellationToken ct,
        IReadOnlySet<string>? trustedHosts = null, int delayMs = 0)
    {
        var result = new Dictionary<string, LinkStatus>(known, StringComparer.Ordinal);
        var todo = urls.Select(UrlNormalizer.Normalize).Distinct(StringComparer.Ordinal).Where(u => !result.ContainsKey(u)).ToList();

        var found = new System.Collections.Concurrent.ConcurrentDictionary<string, LinkStatus>(StringComparer.Ordinal);
        await Parallel.ForEachAsync(todo, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, parallelism), CancellationToken = ct }, async (url, token) =>
        {
            var r = await fetcher.FetchAsync(url, null, token, headOnly: true, trustedHosts: trustedHosts);
            found[url] = FromFetch(r);
            if (delayMs > 0) await Task.Delay(delayMs, token);
        });

        foreach (var (url, status) in found) result[url] = status;
        return result;
    }

    public static LinkStatus FromFetch(FetchResult r) => new(r.StatusCode, r.FinalUrl, r.RedirectChain.Count, r.Error);

    public static LinkStatus FromFacts(PageFacts f) => new(f.StatusCode, f.FinalUrl, f.RedirectChain.Count, f.Error);
}
