using Esatto.Umbraco.Backoffice.Seo.Facts;

namespace Esatto.Umbraco.Backoffice.Seo.Evaluation;

/// <summary>
/// A scanned page as Umbraco knows it. <see cref="OtherCultures"/> = the node's other published cultures
/// (empty for invariant content); <see cref="IsVariant"/> = the node varies by culture.
/// </summary>
public sealed record PageRef(Guid ContentKey, string Name, string Culture, string Url, bool IsRoot, bool IsVariant, IReadOnlyList<string> OtherCultures);

/// <summary>Excluded = noindex (meta or header); Blocked = robots.txt disallows it. Excluded wins the label.</summary>
public enum Indexability { Indexable, Excluded, Blocked }

public sealed record PageContext(PageRef Page, PageFacts? Facts, Indexability Indexability, bool BlockedByRobots)
{
    /// <summary>The page answered 200 with HTML, so its content can be judged.</summary>
    public bool Fetched => Facts is { StatusCode: 200, IsHtml: true };

    public bool Hidden => Indexability != Indexability.Indexable;
}

/// <summary>A link target's status. StatusCode 0 = no response.</summary>
public sealed record LinkStatus(int StatusCode, string? FinalUrl, int Hops, string? Error)
{
    public bool Broken => StatusCode is 0 or >= 400;
    public bool Redirected => Hops > 0 && !Broken;
}

/// <summary>
/// Site-wide facts. <see cref="HttpsRedirects"/> and <see cref="HostDuplicate"/> are null when not
/// applicable (localhost, IP addresses). <see cref="SitemapPartial"/>: some of the sitemap wasn't read
/// (an index with more children than are read, or a child that didn't load), so a page missing from
/// <see cref="SitemapUrls"/> may still be listed.
/// </summary>
public sealed record SiteProbe(
    IReadOnlyDictionary<string, bool> RobotsFound,
    IReadOnlyDictionary<string, string?> RobotsText,
    bool SitemapFound,
    IReadOnlyList<string> SitemapUrls,
    bool? HttpsRedirects,
    bool? HostDuplicate,
    bool SitemapPartial = false)
{
    public static readonly SiteProbe None = new(new Dictionary<string, bool>(), new Dictionary<string, string?>(), false, [], null, null);
}

/// <summary>Everything one scan found, ready for the checks. Built from stored rows, so it can be rebuilt any time.</summary>
public sealed class ScanSnapshot
{
    /// <summary>Share of fetched pages that must be hidden (with the root) for the whole site to count as hidden.</summary>
    public const double SiteHiddenShare = 0.9;

    private readonly Dictionary<string, PageContext> _byUrl;

    private ScanSnapshot(Guid siteKey, IReadOnlyList<string> siteCultures, IReadOnlyList<PageContext> pages, SiteProbe probe,
        IReadOnlyDictionary<string, LinkStatus> links, bool intentionallyHidden)
    {
        SiteKey = siteKey;
        SiteCultures = siteCultures;
        Pages = pages;
        Probe = probe;
        Links = links;
        IntentionallyHidden = intentionallyHidden;

        _byUrl = new Dictionary<string, PageContext>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            _byUrl.TryAdd(UrlNormalizer.Normalize(page.Page.Url), page);
            if (page.Facts is { FinalUrl.Length: > 0 } f) _byUrl.TryAdd(UrlNormalizer.Normalize(f.FinalUrl), page);
        }

        var fetched = pages.Where(p => p.Fetched).ToList();
        var roots = fetched.Where(p => p.Page.IsRoot).ToList();
        SiteHidden = roots.Count > 0 && roots.All(r => r.Hidden)
            && fetched.Count(p => p.Hidden) >= SiteHiddenShare * fetched.Count;
    }

    private HashSet<string>? _sitemap;

    /// <summary>The sitemap's addresses, normalised; built once (checks ask per page).</summary>
    public IReadOnlySet<string> SitemapSet => _sitemap ??= Probe.SitemapUrls.Select(UrlNormalizer.Normalize).ToHashSet(StringComparer.Ordinal);

    public Guid SiteKey { get; }
    public IReadOnlyList<string> SiteCultures { get; }
    public IReadOnlyList<PageContext> Pages { get; }
    public SiteProbe Probe { get; }

    /// <summary>Link target statuses keyed by normalised URL.</summary>
    public IReadOnlyDictionary<string, LinkStatus> Links { get; }

    /// <summary>Configuration says this environment is hidden on purpose (Test/Dev).</summary>
    public bool IntentionallyHidden { get; }

    /// <summary>The root page and nearly every page are hidden from search engines.</summary>
    public bool SiteHidden { get; }

    /// <summary>Content and head checks judge this page: it loaded, and it's indexable (or the whole site is hidden).</summary>
    public bool ContentChecksApply(PageContext p) => p.Fetched && (!p.Hidden || SiteHidden);

    /// <summary>The scanned page at <paramref name="url"/> (any spelling), if any.</summary>
    public PageContext? FindPage(string url) => _byUrl.GetValueOrDefault(UrlNormalizer.Normalize(url));

    public static ScanSnapshot Build(Guid siteKey, IReadOnlyList<string> siteCultures, IEnumerable<(PageRef Ref, PageFacts? Facts)> pages,
        SiteProbe probe, IReadOnlyDictionary<string, LinkStatus> links, bool intentionallyHidden)
    {
        var robots = probe.RobotsText.ToDictionary(kv => kv.Key, kv => RobotsTxt.Parse(kv.Value), StringComparer.OrdinalIgnoreCase);

        var contexts = pages.Select(p =>
        {
            var blocked = Uri.TryCreate(p.Ref.Url, UriKind.Absolute, out var uri)
                && robots.TryGetValue(uri.Host, out var r) && !r.IsAllowed(uri.PathAndQuery);
            var noindex = p.Facts?.Noindex == true;
            var state = noindex ? Indexability.Excluded : blocked ? Indexability.Blocked : Indexability.Indexable;
            return new PageContext(p.Ref, p.Facts, state, blocked);
        }).ToList();

        return new ScanSnapshot(siteKey, siteCultures, contexts, probe, links, intentionallyHidden);
    }
}
