using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using Esatto.Umbraco.Backoffice.Seo.Facts;
using Esatto.Umbraco.Backoffice.Seo.Settings;

namespace Esatto.Umbraco.Backoffice.Seo.Scanning;

/// <summary>
/// Fetches one URL the way the scan needs it: redirects followed by hand (so the chain is known), a
/// timeout per request, textual bodies read up to <see cref="MaxBodyBytes"/>, gzip files unpacked.
/// Network problems become <see cref="FetchResult.Error"/>; only cancellation throws.
/// </summary>
public sealed class PageFetcher(IHttpClientFactory clients, SeoEnvironment environment)
{
    public const string ClientName = "Esatto.Umbraco.Backoffice.Seo";
    public const int MaxRedirects = 10;
    public const int MaxBodyBytes = 5 * 1024 * 1024;

    /// <summary>The sitemap protocol's limit for one (uncompressed) file.</summary>
    public const int MaxSitemapBytes = 50 * 1024 * 1024;

    private const long MaxCountedBytes = 50L * 1024 * 1024;

    // .NET only knows UTF-8/16/32, ASCII and Latin-1 by default; older sites still declare windows-1252 and friends.
    static PageFetcher() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static readonly string UserAgent = $"EsattoSeoHealth/{Version()} (+https://github.com/carl-schele-esatto/Esatto.Packages)";

    /// <summary>Per request. Settable for tests.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <param name="trustedHosts">
    /// Hosts that may receive the configured request headers (basic-auth shared secret, <c>Esatto:Seo:RequestHeaders</c>).
    /// Any other host, including one reached by a redirect, gets none — like a browser dropping Authorization across hosts.
    /// </param>
    /// <param name="maxBodyBytes">How much of the body is kept (sitemaps may be larger than pages).</param>
    public async Task<FetchResult> FetchAsync(string url, string? culture, CancellationToken ct, bool headOnly = false, IReadOnlySet<string>? trustedHosts = null,
        int maxBodyBytes = MaxBodyBytes)
    {
        if (!IsHttp(url, out var start)) return Failed(url, url, [], "Not an http(s) address", 0);

        var client = clients.CreateClient(ClientName);
        var chain = new List<string>();
        var current = start.AbsoluteUri;
        // Exact addresses: /om → /om/ is a redirect, not a loop (UrlNormalizer would call them equal).
        var seen = new HashSet<string>(StringComparer.Ordinal) { current };

        while (true)
        {
            HttpResponseMessage response;
            long elapsed;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Timeout);
            var watch = Stopwatch.StartNew();
            var trusted = trustedHosts?.Contains(new Uri(current).Host) == true;
            try
            {
                response = await SendAsync(client, current, culture, headOnly, trusted, timeout.Token);
                if (headOnly && (int)response.StatusCode is 405 or 501)
                {
                    response.Dispose();
                    response = await SendAsync(client, current, culture, headOnly: false, trusted, timeout.Token);
                }
                elapsed = watch.ElapsedMilliseconds;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return Failed(url, current, chain, $"Timed out after {Timeout.TotalSeconds:0.#} s", watch.ElapsedMilliseconds);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Connection problems, and anything odd a handler throws: one page's problem, never the scan's.
                return Failed(url, current, chain, ex.Message, watch.ElapsedMilliseconds);
            }

            using (response)
            {
                var status = (int)response.StatusCode;
                if (status is >= 300 and < 400 && response.Headers.Location is { } location)
                {
                    chain.Add(current);
                    if (!Uri.TryCreate(new Uri(current), location, out var target) || target.Scheme is not ("http" or "https"))
                        return Failed(url, current, chain, $"Redirects to a non-http address ({location})", elapsed, status);
                    var next = WithoutFragment(target);
                    if (!seen.Add(next)) return Failed(url, current, chain, "Redirect loop", elapsed, status);
                    if (chain.Count >= MaxRedirects) return Failed(url, next, chain, "Too many redirects", elapsed, status);
                    current = next;
                    continue;
                }

                var contentType = response.Content.Headers.ContentType?.ToString();
                var robots = response.Headers.TryGetValues("X-Robots-Tag", out var values) ? string.Join(", ", values) : null;
                string? body = null;
                var bytes = response.Content.Headers.ContentLength ?? 0;

                if (!headOnly && IsReadable(contentType))
                {
                    try
                    {
                        (body, bytes) = await ReadBodyAsync(response.Content, maxBodyBytes, timeout.Token);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        return Failed(url, current, chain, $"Timed out after {Timeout.TotalSeconds:0.#} s", watch.ElapsedMilliseconds, status);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        return Failed(url, current, chain, ex.Message, elapsed, status);
                    }
                }

                return new FetchResult(url, current, status, chain, contentType, robots, body, bytes, elapsed, null);
            }
        }
    }

    private static bool IsHttp(string url, out Uri uri)
        => Uri.TryCreate(url, UriKind.Absolute, out uri!) && uri.Scheme is "http" or "https";

    private static string WithoutFragment(Uri uri) => uri.GetComponents(UriComponents.AbsoluteUri & ~UriComponents.Fragment, UriFormat.UriEscaped);

    private async Task<HttpResponseMessage> SendAsync(HttpClient client, string url, string? culture, bool headOnly, bool trusted, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(headOnly ? HttpMethod.Head : HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        if (!string.IsNullOrEmpty(culture)) request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(culture));
        if (trusted)
            foreach (var (name, value) in environment.RequestHeaders) request.Headers.TryAddWithoutValidation(name, value);
        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    private static bool IsReadable(string? contentType)
        => contentType is null || new[] { "html", "xml", "text/", "json", "gzip" }.Any(t => contentType.Contains(t, StringComparison.OrdinalIgnoreCase));

    /// <summary>Reads at most <paramref name="max"/> bytes into the body, but counts further (up to 50 MB) so huge pages show their size.</summary>
    private static async Task<(string Body, long Bytes)> ReadBodyAsync(HttpContent content, int max, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var kept = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;
        int n;
        while ((n = await stream.ReadAsync(chunk, ct)) > 0)
        {
            total += n;
            var room = max - kept.Length;
            if (room > 0) kept.Write(chunk, 0, (int)Math.Min(n, room));
            if (total > Math.Max(MaxCountedBytes, max)) break;
        }

        var data = kept.ToArray();
        if (data.Length > 2 && data[0] == 0x1f && data[1] == 0x8b) data = Gunzip(data, max);
        return (Decode(data, content.Headers.ContentType?.CharSet), total);
    }

    private static byte[] Gunzip(byte[] data, int max)
    {
        using var gz = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
        using var output = new MemoryStream();
        var chunk = new byte[81920];
        int n;
        while ((n = gz.Read(chunk)) > 0 && output.Length < max) output.Write(chunk, 0, n);
        return output.ToArray();
    }

    private static string Decode(byte[] data, string? charset)
    {
        Encoding encoding;
        try
        {
            encoding = string.IsNullOrWhiteSpace(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset.Trim('"', ' '));
        }
        catch (ArgumentException)
        {
            encoding = Encoding.UTF8;
        }

        return encoding.GetString(data).TrimStart('﻿');
    }

    private static FetchResult Failed(string requested, string current, List<string> chain, string error, long elapsed, int status = 0)
        => new(requested, current, status, chain, null, null, null, 0, elapsed, error);

    private static string Version()
    {
        var info = typeof(PageFetcher).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";
        var plus = info.IndexOf('+');
        return plus > 0 ? info[..plus] : info;
    }
}
