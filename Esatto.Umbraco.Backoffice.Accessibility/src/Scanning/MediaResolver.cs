using System.Net;
using System.Text.RegularExpressions;
using Umbraco.Cms.Core.Services;

namespace Esatto.Umbraco.Backoffice.Accessibility.Scanning;

/// <summary>
/// Finds the media item behind an image finding, so "Edit in Umbraco" can open it. Register scoped:
/// it caches lookups for the duration of a request.
/// </summary>
public sealed partial class MediaResolver(IMediaService mediaService)
{
    private readonly Dictionary<string, Guid?> _cache = new(StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex("""<img\b[^>]*?\bsrc\s*=\s*["']([^"']+)["']""", RegexOptions.IgnoreCase)]
    private static partial Regex ImgSrc();

    /// <summary>The key of the media item the first <c>&lt;img src&gt;</c> in <paramref name="html"/> points to, or null.</summary>
    public Guid? Resolve(string html)
    {
        var match = ImgSrc().Match(html ?? string.Empty);
        if (!match.Success) return null;

        var path = MediaPath(WebUtility.HtmlDecode(match.Groups[1].Value));
        if (path is null) return null;

        if (_cache.TryGetValue(path, out var cached)) return cached;
        var key = mediaService.GetMediaByPath(path)?.Key;
        _cache[path] = key;
        return key;
    }

    /// <summary>"/media/abc/x.jpg?width=600" → "/media/abc/x.jpg"; anything not under /media/ → null.</summary>
    public static string? MediaPathOf(string src) => MediaPath(src);

    private static string? MediaPath(string src)
    {
        var path = src;
        // A root-relative src is a path. Check it first: on Unix, Uri.TryCreate("/media/…", Absolute)
        // succeeds as a file: URI, which the scheme check below would then reject.
        var rootRelative = src.StartsWith('/') && !src.StartsWith("//", StringComparison.Ordinal);
        if (!rootRelative && Uri.TryCreate(src, UriKind.Absolute, out var absolute))
        {
            // Absolute URLs are only accepted for http(s) — data: and blob: never point to media.
            if (absolute.Scheme is not ("http" or "https")) return null;
            path = absolute.AbsolutePath;
        }

        var end = path.IndexOfAny(['?', '#']);
        if (end >= 0) path = path[..end];
        return path.StartsWith("/media/", StringComparison.OrdinalIgnoreCase) ? path : null;
    }
}
