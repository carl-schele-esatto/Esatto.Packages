using System.Text.RegularExpressions;
using Esatto.Umbraco.Backoffice.Seo.Evaluation;
using Esatto.Umbraco.Backoffice.Seo.Facts;

namespace Esatto.Umbraco.Backoffice.Seo.Checks;

// Checks that compare pages with each other or with the sitemap.

/// <summary>Same value on several pages of the same language (hidden pages left out).</summary>
public abstract class DuplicateCheck(string id, Severity severity, Func<PageFacts, string?> value) : ICheck
{
    public CheckInfo Info { get; } = new(id, severity, Owner.Editor, CheckScope.CrossPage);

    public IEnumerable<Outcome> Evaluate(ScanSnapshot s)
    {
        var pages = s.Pages.Where(p => s.ContentChecksApply(p) && value(p.Facts!) is not null).ToList();
        var counts = pages
            .GroupBy(p => (Culture: p.Page.Culture.ToLowerInvariant(), Value: value(p.Facts!)!))
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var p in pages)
        {
            var v = value(p.Facts!)!;
            var n = counts[(p.Page.Culture.ToLowerInvariant(), v)];
            yield return Outcome.For(Info.Id, p, n < 2, $"\"{v}\" ({n} pages)");
        }
    }
}

public sealed class TitleDuplicate() : DuplicateCheck(CheckIds.TitleDuplicate, Severity.Warning, f => f.Title);

public sealed class DescriptionDuplicate() : DuplicateCheck(CheckIds.DescriptionDuplicate, Severity.Notice, f => f.MetaDescription);

/// <summary>
/// hreflang as a URL graph, so both culture variants and one-tree-per-language sites work: valid codes,
/// the page lists itself, targets load, and targets in this scan link back. A target outside the scan
/// (another site root) is judged by its link status only.
/// </summary>
public sealed partial class HreflangInvalid() : PageCheck(CheckIds.HreflangInvalid, Severity.Warning, Owner.Developer, CheckScope.CrossPage)
{
    public const int MaxListed = 10;

    protected override bool AppliesTo(PageContext p, ScanSnapshot s) => base.AppliesTo(p, s) && p.Facts!.Hreflangs.Count > 0;

    protected override (bool, string?) Test(PageContext p, ScanSnapshot s)
    {
        var problems = new List<string>();
        var self = SelfUrls(p);

        foreach (var link in p.Facts!.Hreflangs)
        {
            if (!ValidCode().IsMatch(link.Lang)) problems.Add($"Invalid language code \"{link.Lang}\"");

            var target = UrlNormalizer.Normalize(link.Href);
            if (self.Contains(target)) continue;

            var page = s.FindPage(link.Href);
            if (page is null)
            {
                if (s.Links.TryGetValue(target, out var status) && status.Broken) problems.Add($"{link.Href} ({link.Lang}) doesn't load");
                continue;
            }

            if (!page.Fetched) problems.Add($"{link.Href} ({link.Lang}) doesn't load");
            else if (!page.Facts!.Hreflangs.Any(h => self.Contains(UrlNormalizer.Normalize(h.Href))))
                problems.Add($"{link.Href} ({link.Lang}) doesn't link back");
        }

        if (!p.Facts.Hreflangs.Any(h => self.Contains(UrlNormalizer.Normalize(h.Href)))) problems.Add("The hreflang list doesn't list the page itself");

        return problems.Count == 0 ? Pass : Fail(string.Join("\n", problems.Distinct().Take(MaxListed)));
    }

    private static HashSet<string> SelfUrls(PageContext p)
    {
        var urls = new HashSet<string>(StringComparer.Ordinal) { UrlNormalizer.Normalize(p.Page.Url) };
        if (p.Facts is { FinalUrl.Length: > 0 }) urls.Add(UrlNormalizer.Normalize(p.Facts.FinalUrl));
        return urls;
    }

    // BCP 47 as hreflang uses it: language, optional script, optional region (case-insensitive), or x-default.
    [GeneratedRegex(@"^(x-default|[a-z]{2,3}(-[a-z]{4})?(-([a-z]{2}|\d{3}))?)$", RegexOptions.IgnoreCase)]
    private static partial Regex ValidCode();
}

public sealed class SitemapMissingPage() : PageCheck(CheckIds.SitemapMissingPage, Severity.Notice, Owner.Developer, CheckScope.CrossPage)
{
    // A partly read sitemap can't say a page is missing from it.
    protected override bool AppliesTo(PageContext p, ScanSnapshot s) => s.Probe is { SitemapFound: true, SitemapPartial: false } && p.Fetched && !p.Hidden;

    protected override (bool, string?) Test(PageContext p, ScanSnapshot s)
    {
        var listed = s.SitemapSet;
        var inSitemap = listed.Contains(UrlNormalizer.Normalize(p.Page.Url)) || listed.Contains(UrlNormalizer.Normalize(p.Facts!.FinalUrl));
        return inSitemap ? Pass : Fail(null);
    }
}

/// <summary>One outcome per sitemap address on this site's hosts. Paused while the whole site is hidden.</summary>
public sealed class SitemapBadEntry : ICheck
{
    public CheckInfo Info { get; } = new(CheckIds.SitemapBadEntry, Severity.Warning, Owner.Developer, CheckScope.CrossPage);

    public IEnumerable<Outcome> Evaluate(ScanSnapshot s)
    {
        if (!s.Probe.SitemapFound || s.SiteHidden) yield break;

        var hosts = s.Pages.Select(p => Uri.TryCreate(p.Page.Url, UriKind.Absolute, out var u) ? u.Host : null)
            .OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var url in s.Probe.SitemapUrls.Distinct(StringComparer.Ordinal))
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !hosts.Contains(uri.Host)) continue;

            var page = s.FindPage(url);
            var why = page switch
            {
                null => "not a published page",
                { Fetched: false } => "doesn't load",
                { Indexability: Indexability.Excluded } => "marked noindex",
                { Indexability: Indexability.Blocked } => "blocked by robots.txt",
                _ => null,
            };
            yield return new Outcome(Info.Id, page?.Page.ContentKey, page?.Page.Culture ?? string.Empty, url, why is null, why);
        }
    }
}
