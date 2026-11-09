using Esatto.Umbraco.Backoffice.Seo.Evaluation;

namespace Esatto.Umbraco.Backoffice.Seo.Pages;

/// <summary>Turns published content into the scan list: routable pages only, with skip reasons.</summary>
public static class PageListBuilder
{
    public static IReadOnlyList<ScanPage> Build(IEnumerable<PageCandidate> candidates, IReadOnlyList<string> excludePaths, Uri? baseUri)
    {
        var prefixes = excludePaths.Select(NormalizePrefix).Where(p => p.Length > 0).ToList();

        return candidates
            .Where(c => c.HasTemplate)
            .Select(c =>
            {
                var url = BaseUrlResolver.MakeAbsolute(c.Url, baseUri);
                var pageRef = new PageRef(c.ContentKey, c.Name, c.Culture, url ?? c.Url ?? string.Empty, c.IsRoot, c.IsVariant, c.OtherCultures);
                return new ScanPage(pageRef, SkipReason(c, url, prefixes));
            })
            .ToList();
    }

    private static string? SkipReason(PageCandidate c, string? url, IReadOnlyList<string> prefixes)
    {
        if (url is null) return SkipReasons.NoUrl;
        if (c.Protected) return SkipReasons.Protected;
        if (prefixes.Any(p => Matches(new Uri(url).AbsolutePath, p))) return SkipReasons.Excluded;
        return null;
    }

    /// <summary>"/sok" matches "/sok" and "/sok/…", never "/sokresultat". Case-insensitive.</summary>
    private static bool Matches(string path, string prefix)
    {
        var p = path.TrimEnd('/');
        return p.Equals(prefix, StringComparison.OrdinalIgnoreCase) || p.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePrefix(string prefix)
    {
        var trimmed = prefix.Trim().TrimEnd('/');
        if (trimmed.Length == 0) return string.Empty;
        return trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
    }
}

/// <summary>Where relative page URLs (no domain bound in Umbraco) are fetched from.</summary>
public static class BaseUrlResolver
{
    /// <summary>The Settings override, else Umbraco's application URL, else (manual scans) the backoffice request's host.</summary>
    public static Uri? Resolve(string? overrideUrl, string? applicationUrl, Uri? requestBase)
        => Parse(overrideUrl) ?? Parse(applicationUrl) ?? requestBase;

    /// <summary>An absolute http(s) URL, or null when <paramref name="url"/> can't be made one.</summary>
    public static string? MakeAbsolute(string? url, Uri? baseUri)
    {
        if (string.IsNullOrWhiteSpace(url) || url.StartsWith('#')) return null;
        if (Uri.TryCreate(url, UriKind.Absolute, out var abs) && abs.Scheme is "http" or "https") return abs.ToString();
        if (baseUri is null || !url.StartsWith('/')) return null;
        return new Uri(baseUri, url).ToString();
    }

    private static Uri? Parse(string? value)
        => !string.IsNullOrWhiteSpace(value) && Uri.TryCreate(value.Trim(), UriKind.Absolute, out var u) && u.Scheme is "http" or "https" ? u : null;
}

/// <summary>Culture and Hostnames entries, as Umbraco stores them in <c>IDomain.DomainName</c>.</summary>
public static class BoundDomains
{
    /// <summary>
    /// Whether a bound domain names a host ("esatto.se", "https://esatto.se/en", "localhost:44324"). "/" and "/en" bind
    /// a path only: Umbraco takes their host from the current request, and a background scan has none, so absolute
    /// URLs for them come out as http://localhost/… — a site bound that way is scanned like one without a domain.
    /// Wildcards ("*1071") are <c>IDomain.IsWildcard</c> and are the caller's concern.
    /// </summary>
    public static bool NamesHost(string? domainName)
        => !string.IsNullOrWhiteSpace(domainName) && !domainName.TrimStart().StartsWith('/');
}
