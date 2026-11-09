using System.Net;
using Esatto.Umbraco.Backoffice.Seo.Evaluation;
using Esatto.Umbraco.Backoffice.Seo.Facts;

namespace Esatto.Umbraco.Backoffice.Seo.Scanning;

/// <summary>Site-wide facts: robots.txt per host, the sitemap(s), http→https, www vs bare host.</summary>
public sealed class SiteProber(PageFetcher fetcher)
{
    public const int MaxChildSitemaps = 50;

    /// <param name="origins">The scheme+host(+port) of every page in the scan.</param>
    /// <param name="trustedHosts">Hosts that may receive the configured request headers (see <see cref="PageFetcher"/>).</param>
    /// <param name="delayMs">Pause after each request (the scan's "delay between requests").</param>
    public Task<SiteProbe> ProbeAsync(IReadOnlyList<Uri> origins, CancellationToken ct, IReadOnlySet<string>? trustedHosts = null, int delayMs = 0)
        => new Probe(fetcher, trustedHosts, delayMs, ct).RunAsync(origins); // per call: the prober is a singleton shared by concurrent scans

    private sealed class Probe(PageFetcher fetcher, IReadOnlySet<string>? trusted, int delayMs, CancellationToken ct)
    {
        public async Task<SiteProbe> RunAsync(IReadOnlyList<Uri> origins)
        {
            var distinct = origins.Select(o => new Uri(o.GetLeftPart(UriPartial.Authority) + "/")).DistinctBy(o => o.ToString()).ToList();
            var robotsFound = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var robotsText = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var sitemapAddresses = new List<string>();

            foreach (var origin in distinct)
            {
                var r = await FetchAsync(new Uri(origin, "/robots.txt").ToString());
                var found = r.StatusCode == 200 && r.Body is not null && !LooksLikeHtml(r);
                robotsFound[origin.Host] = found;
                robotsText[origin.Host] = found ? r.Body : null;
                // "Sitemap: /sitemap.xml" is invalid by the standard but common: resolve it against the host.
                if (found) sitemapAddresses.AddRange(RobotsTxt.Parse(r.Body).Sitemaps.Select(a => Resolve(origin, a)).OfType<string>());
            }

            if (sitemapAddresses.Count == 0) sitemapAddresses.AddRange(distinct.Select(o => new Uri(o, "/sitemap.xml").ToString()));
            var (sitemapFound, urls, partial) = await ReadSitemapsAsync(sitemapAddresses.Distinct(StringComparer.OrdinalIgnoreCase));

            bool? https = null, duplicate = null;
            foreach (var origin in distinct.Where(o => IsRealHost(o.Host)))
            {
                // Null = nothing answers on http at all, which is fine (nothing is served unencrypted).
                if (await RedirectsToHttpsAsync(origin) is { } redirects) https = (https ?? true) && redirects;
                duplicate = (duplicate ?? false) | await AnswersOnOtherHostAsync(origin);
            }

            return new SiteProbe(robotsFound, robotsText, sitemapFound, urls, https, duplicate, partial);
        }

        private async Task<(bool Found, IReadOnlyList<string> Urls, bool Partial)> ReadSitemapsAsync(IEnumerable<string> addresses)
        {
            var found = false;
            var partial = false;
            var urls = new List<string>();
            foreach (var address in addresses)
            {
                var doc = await ReadAsync(address);
                if (!doc.IsValid) continue;
                found = true;
                if (!doc.IsIndex)
                {
                    urls.AddRange(doc.Locations);
                    continue;
                }

                // One level of index only; children may be relative to the index file.
                var children = doc.Locations.Select(l => Resolve(new Uri(address), l)).OfType<string>().ToList();
                if (children.Count > MaxChildSitemaps) partial = true;
                foreach (var child in children.Take(MaxChildSitemaps))
                {
                    var childDoc = await ReadAsync(child);
                    if (childDoc is { IsValid: true, IsIndex: false }) urls.AddRange(childDoc.Locations);
                    else partial = true;
                }
            }

            return (found, urls.Distinct(StringComparer.Ordinal).ToList(), partial);
        }

        private async Task<SitemapDocument> ReadAsync(string address)
        {
            var r = await FetchAsync(address, PageFetcher.MaxSitemapBytes);
            return r.StatusCode == 200 ? SitemapReader.Parse(r.Body) : SitemapDocument.Empty;
        }

        /// <summary>True/false when http answers; null when nothing answers on http at all.</summary>
        private async Task<bool?> RedirectsToHttpsAsync(Uri origin)
        {
            var r = await FetchAsync($"http://{origin.Host}/", headOnly: true);
            if (r.StatusCode == 0 && r.RedirectChain.Count == 0) return null;
            return r.FinalUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && r.StatusCode is > 0 and < 400;
        }

        /// <summary>Both this host and its www/bare twin answer 200 on their own host, so the site exists twice.</summary>
        private async Task<bool> AnswersOnOtherHostAsync(Uri origin)
        {
            var twin = origin.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? origin.Host[4..] : "www." + origin.Host;
            return await StaysOnHostAsync($"{origin.Scheme}://{twin}/", twin) && await StaysOnHostAsync(origin.ToString(), origin.Host);
        }

        private async Task<bool> StaysOnHostAsync(string url, string host)
        {
            var r = await FetchAsync(url, headOnly: true);
            return r.StatusCode == 200 && Uri.TryCreate(r.FinalUrl, UriKind.Absolute, out var final) && final.Host.Equals(host, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<FetchResult> FetchAsync(string url, int maxBodyBytes = PageFetcher.MaxBodyBytes, bool headOnly = false)
        {
            var r = await fetcher.FetchAsync(url, null, ct, headOnly, trusted, maxBodyBytes);
            if (delayMs > 0) await Task.Delay(delayMs, ct);
            return r;
        }
    }

    private static string? Resolve(Uri baseUri, string address)
        => Uri.TryCreate(baseUri, address.Trim(), out var u) && u.Scheme is "http" or "https" ? u.ToString() : null;

    /// <summary>A 200 HTML page at /robots.txt is a catch-all route, not a robots file.</summary>
    private static bool LooksLikeHtml(FetchResult r) => r.ContentType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsRealHost(string host)
        => !host.Equals("localhost", StringComparison.OrdinalIgnoreCase) && !IPAddress.TryParse(host, out _) && host.Contains('.');
}
