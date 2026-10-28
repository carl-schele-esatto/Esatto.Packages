namespace Esatto.Umbraco.Backoffice.Accessibility.Pages;

/// <summary>Turns published content into the scan list: routable pages only, with skip reasons.</summary>
public static class PageListBuilder
{
    public static IReadOnlyList<ScanPage> Build(IEnumerable<PageCandidate> candidates, IReadOnlyList<string> excludePaths)
    {
        var prefixes = excludePaths.Select(NormalizePrefix).Where(p => p.Length > 0).ToList();

        return candidates
            .Where(c => c.HasTemplate)
            .Select(c => new ScanPage(c.ContentKey, c.Name, c.Culture, c.AbsoluteUrl ?? string.Empty, SkipReason(c, prefixes)))
            .ToList();
    }

    private static string? SkipReason(PageCandidate c, IReadOnlyList<string> prefixes)
    {
        if (!Uri.TryCreate(c.AbsoluteUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return SkipReasons.NoUrl;
        if (c.Protected) return SkipReasons.Protected;
        if (prefixes.Any(p => Matches(uri.AbsolutePath, p))) return SkipReasons.Excluded;
        return null;
    }

    /// <summary>"/sok" matches "/sok" and "/sok/…", never "/sokresultat". Case-insensitive.</summary>
    private static bool Matches(string path, string prefix)
    {
        var p = path.TrimEnd('/');
        return p.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || p.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePrefix(string prefix)
    {
        var trimmed = prefix.Trim().TrimEnd('/');
        if (trimmed.Length == 0) return string.Empty;
        return trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
    }
}
