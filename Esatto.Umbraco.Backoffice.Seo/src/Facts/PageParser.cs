using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Esatto.Umbraco.Backoffice.Seo.Facts;

/// <summary>Reduces a fetched page to <see cref="PageFacts"/>. Never throws on odd input.</summary>
public static partial class PageParser
{
    public static PageFacts Parse(FetchResult r, IReadOnlySet<string> siteHosts)
    {
        var facts = new PageFacts
        {
            FinalUrl = r.FinalUrl,
            StatusCode = r.StatusCode,
            RedirectChain = r.RedirectChain,
            ElapsedMs = r.ElapsedMs,
            Bytes = r.Bytes,
            Error = r.Error,
            HeaderNoindex = HeaderSaysNoindex(r.XRobotsTag),
        };

        if (r.Body is null || !IsHtmlType(r.ContentType)) return facts;

        // HtmlParser isn't documented as thread-safe; pages are parsed in parallel.
        var doc = new HtmlParser().ParseDocument(r.Body);
        var baseUri = Uri.TryCreate(r.FinalUrl, UriKind.Absolute, out var b) ? b : new Uri("http://localhost/");
        var metas = doc.QuerySelectorAll("meta").ToList();

        string? Meta(string attr, string name) => metas
            .Where(m => string.Equals(m.GetAttribute(attr)?.Trim(), name, StringComparison.OrdinalIgnoreCase))
            .Select(m => Collapse(m.GetAttribute("content")))
            .FirstOrDefault(v => v is not null);

        var (jsonTotal, jsonInvalid) = JsonLd(doc);

        return facts with
        {
            IsHtml = true,
            Title = Collapse(doc.QuerySelector("title")?.TextContent),
            MetaDescription = Meta("name", "description"),
            H1s = doc.QuerySelectorAll("h1").Select(h => Collapse(h.TextContent) ?? string.Empty).ToList(),
            Canonical = Resolve(baseUri, LinkHref(doc, "canonical")),
            Hreflangs = doc.QuerySelectorAll("link[hreflang]")
                .Where(l => HasRel(l, "alternate"))
                .Select(l => (Lang: l.GetAttribute("hreflang")?.Trim() ?? string.Empty, Href: Resolve(baseUri, l.GetAttribute("href"))))
                .Where(x => x.Href is not null)
                .Select(x => new HreflangLink(x.Lang, x.Href!))
                .ToList(),
            HtmlLang = Collapse(doc.DocumentElement?.GetAttribute("lang")),
            MetaNoindex = metas.Any(m => IsRobotsMeta(m.GetAttribute("name")) && SaysNoindex(m.GetAttribute("content"))),
            OgTitle = Meta("property", "og:title"),
            OgImage = Resolve(baseUri, Meta("property", "og:image")),
            JsonLdTotal = jsonTotal,
            JsonLdInvalid = jsonInvalid,
            InternalLinks = doc.QuerySelectorAll("a[href]")
                .Select(a => UrlNormalizer.TryResolve(baseUri, a.GetAttribute("href"), out var abs) ? abs : null)
                .Where(u => u is not null && siteHosts.Contains(u.Host))
                .Select(u => UrlNormalizer.Normalize(u!.ToString()))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            // Last: it removes nodes from the document.
            WordCount = CountWords(doc),
        };
    }

    private static bool IsHtmlType(string? contentType)
        => contentType is not null && (contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("application/xhtml+xml", StringComparison.OrdinalIgnoreCase));

    private static bool IsRobotsMeta(string? name)
        => name?.Trim() is { } n && (n.Equals("robots", StringComparison.OrdinalIgnoreCase) || n.Equals("googlebot", StringComparison.OrdinalIgnoreCase));

    private static string? Collapse(string? text)
    {
        if (text is null) return null;
        var collapsed = Whitespace().Replace(text, " ").Trim();
        return collapsed.Length == 0 ? null : collapsed;
    }

    private static bool HasRel(IElement link, string rel)
        => (link.GetAttribute("rel") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(rel, StringComparer.OrdinalIgnoreCase);

    private static string? LinkHref(IDocument doc, string rel)
        => doc.QuerySelectorAll("link[href]").FirstOrDefault(l => HasRel(l, rel))?.GetAttribute("href");

    private static string? Resolve(Uri baseUri, string? href)
        => UrlNormalizer.TryResolve(baseUri, href, out var abs) ? abs.ToString() : null;

    private static bool IsNoindexDirective(string d)
        => d.Equals("noindex", StringComparison.OrdinalIgnoreCase) || d.Equals("none", StringComparison.OrdinalIgnoreCase);

    private static bool SaysNoindex(string? directives)
        => (directives ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Any(IsNoindexDirective);

    /// <summary>
    /// X-Robots-Tag: "noindex, nofollow" applies to every crawler; "googlebot: noindex" to Googlebot;
    /// "otherbot: noindex" not to us. A user-agent prefix applies to the directives that follow it.
    /// </summary>
    internal static bool HeaderSaysNoindex(string? header)
    {
        if (string.IsNullOrWhiteSpace(header)) return false;
        string? agent = null;
        foreach (var raw in header.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var directive = raw;
            var colon = raw.IndexOf(':');
            if (colon > 0 && !raw.StartsWith("unavailable_after", StringComparison.OrdinalIgnoreCase))
            {
                agent = raw[..colon].Trim();
                directive = raw[(colon + 1)..].Trim();
            }

            var forUs = agent is null || agent.Equals("googlebot", StringComparison.OrdinalIgnoreCase);
            if (forUs && IsNoindexDirective(directive)) return true;
        }

        return false;
    }

    private static (int Total, int Invalid) JsonLd(IDocument doc)
    {
        var blocks = doc.QuerySelectorAll("script")
            .Where(s => string.Equals(s.GetAttribute("type")?.Trim(), "application/ld+json", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var invalid = 0;
        foreach (var block in blocks)
        {
            try
            {
                using var _ = JsonDocument.Parse(block.TextContent);
            }
            catch (JsonException)
            {
                invalid++;
            }
        }

        return (blocks.Count, invalid);
    }

    /// <summary>Words in main, else in body minus header/nav/footer; scripts and styles never count.</summary>
    private static int CountWords(IDocument doc)
    {
        var root = doc.QuerySelector("main") ?? doc.Body;
        if (root is null) return 0;

        var drop = root.LocalName == "main"
            ? "script, style, noscript, template"
            : "header, nav, footer, script, style, noscript, template";
        foreach (var node in root.QuerySelectorAll(drop).ToList()) node.Remove();

        // Per text node: TextContent would glue "</h1><p>" neighbours into one word.
        return root.GetDescendants().OfType<IText>()
            .Sum(t => Whitespace().Split(t.Data).Count(w => w.Any(char.IsLetterOrDigit)));
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
