namespace Esatto.Umbraco.Backoffice.Seo.Facts;

/// <summary>
/// One spelling per page, so canonical, hreflang, sitemap and link comparisons agree:
/// lower-case scheme and host, default port dropped, trailing slash dropped (except the root),
/// fragment dropped, query and path case kept.
/// </summary>
public static class UrlNormalizer
{
    public static string Normalize(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return url;

        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.Length == 0) path = "/";
        var port = uri.IsDefaultPort ? string.Empty : ":" + uri.Port;
        return $"{uri.Scheme}://{uri.Host.ToLowerInvariant()}{port}{path}{uri.Query}";
    }

    /// <summary>Resolves a link against the page it's on. False for anything that isn't an http(s) page link.</summary>
    public static bool TryResolve(Uri baseUri, string? href, out Uri absolute)
    {
        absolute = null!;
        var value = href?.Trim();
        if (string.IsNullOrEmpty(value) || value.StartsWith('#')) return false;
        if (!Uri.TryCreate(baseUri, value, out var resolved) || resolved.Scheme is not ("http" or "https")) return false;
        absolute = resolved;
        return true;
    }
}
