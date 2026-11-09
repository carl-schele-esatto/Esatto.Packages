using Esatto.Umbraco.Backoffice.Seo.Checks;
using Esatto.Umbraco.Backoffice.Seo.Evaluation;
using Esatto.Umbraco.Backoffice.Seo.Facts;

namespace Esatto.Umbraco.Backoffice.Seo.Tests.Support;

/// <summary>Builds <see cref="ScanSnapshot"/>s for check tests from small HTML pages on https://esatto.se.</summary>
internal sealed class Snap
{
    public const string Host = "https://esatto.se";
    private static readonly IReadOnlySet<string> Hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "esatto.se", "www.esatto.se" };

    private readonly List<(PageRef Ref, PageFacts? Facts)> _pages = [];
    private readonly Dictionary<string, LinkStatus> _links = [];
    private readonly List<string> _cultures;
    private string? _robots;
    private bool _sitemapFound;
    private List<string> _sitemap = [];
    private bool _intentionallyHidden;
    private bool? _https = true;
    private bool? _hostDuplicate = false;
    private bool _robotsFound = true;
    private bool _sitemapPartial;

    private Snap(string[] cultures) => _cultures = [.. cultures];

    public static Snap Site(params string[] cultures) => new(cultures.Length == 0 ? ["en"] : cultures);

    /// <summary>A page that passes every page check: 40-char title, 100-char description, one h1, 220 words, self canonical, og tags, lang.</summary>
    public static string Good(string path, string culture = "en", string head = "", string? body = null, string? title = null, string? description = null)
    {
        var url = Host + path;
        var t = title ?? ("Page " + path).PadRight(40, '.');
        var d = description ?? ("Description of " + path).PadRight(100, '.');
        var words = string.Join(' ', Enumerable.Repeat("word", 218));
        return $"""
            <html lang="{(culture.Length == 0 ? "en" : culture)}"><head><title>{t}</title><meta name="description" content="{d}">
            <link rel="canonical" href="{url}"><meta property="og:title" content="{t}"><meta property="og:image" content="/a.jpg">{head}</head>
            <body><main><h1>Heading</h1>{body ?? $"<p>{words}</p>"}</main></body></html>
            """;
    }

    public Snap Page(string path, string? html = null, bool root = false, string culture = "en", Guid? key = null, int status = 200,
        long ms = 100, string? xRobots = null, bool variant = false, string[]? others = null, string contentType = "text/html; charset=utf-8",
        long? bytes = null, string? error = null)
    {
        var url = Host + path;
        html ??= Good(path, culture);
        var fetch = new FetchResult(url, url, status, [], contentType, xRobots, status == 0 ? null : html, bytes ?? html.Length, ms, error);
        var pageRef = new PageRef(key ?? Guid.NewGuid(), "Page " + path, culture, url, root, variant, others ?? []);
        _pages.Add((pageRef, PageParser.Parse(fetch, Hosts)));
        return this;
    }

    public Snap Robots(string? text, bool found = true) { _robots = text; _robotsFound = found; return this; }
    public Snap Link(string path, int status, int hops = 0) { _links[UrlNormalizer.Normalize(Host + path)] = new LinkStatus(status, null, hops, null); return this; }
    public Snap Sitemap(params string[] paths) { _sitemapFound = true; _sitemap = paths.Select(p => Host + p).ToList(); return this; }
    public Snap SitemapPartial() { _sitemapPartial = true; return this; }
    public Snap IntentionallyHidden() { _intentionallyHidden = true; return this; }
    public Snap Https(bool? redirects) { _https = redirects; return this; }
    public Snap HostDuplicate(bool? duplicate) { _hostDuplicate = duplicate; return this; }

    public ScanSnapshot Build()
    {
        var probe = new SiteProbe(
            new Dictionary<string, bool> { ["esatto.se"] = _robotsFound },
            new Dictionary<string, string?> { ["esatto.se"] = _robots },
            _sitemapFound, _sitemap, _https, _hostDuplicate, _sitemapPartial);
        return ScanSnapshot.Build(Guid.Empty, _cultures, _pages, probe, _links, _intentionallyHidden);
    }

    // ----- outcome helpers -----

    public static IReadOnlyList<Outcome> Run(ICheck check, ScanSnapshot s) => check.Evaluate(s).ToList();

    public static Outcome For(ICheck check, ScanSnapshot s, string path)
        => Run(check, s).Single(o => o.Url == Host + path);

    public static bool Applies(ICheck check, ScanSnapshot s, string path) => Run(check, s).Any(o => o.Url == Host + path);
}
