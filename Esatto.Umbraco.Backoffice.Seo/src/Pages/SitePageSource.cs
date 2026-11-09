using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Esatto.Umbraco.Backoffice.Seo.Pages;

/// <summary>
/// Reads sites and their published pages from Umbraco. A thin adapter; the rules live in <see cref="PageListBuilder"/>.
/// Scans run on background threads, where there is no request and so no UmbracoContext; URL building needs
/// one, so every read makes sure there is one (a no-op inside a request).
/// </summary>
public sealed class SitePageSource(
    IDocumentNavigationQueryService navigation,
    IPublishedContentCache contentCache,
    IPublishedUrlProvider urlProvider,
    IPublicAccessService publicAccess,
    IDomainService domains,
    ILanguageService languages,
    IUmbracoContextFactory umbracoContextFactory) : IPageSource
{
    public async Task<IReadOnlyList<SiteInfo>> GetSitesAsync()
    {
        if (!navigation.TryGetRootKeys(out var rootKeys)) return [];

        var allDomains = (await domains.GetAllAsync(false)).ToList();
        var sites = new List<SiteInfo>();
        foreach (var key in rootKeys)
        {
            if (contentCache.GetById(key) is not { } root) continue; // unpublished root
            var hosts = allDomains.Where(d => d.RootContentId == root.Id).Select(d => d.DomainName).ToList();
            sites.Add(new SiteInfo(key, root.Name, hosts));
        }

        return sites;
    }

    public async Task<IReadOnlyList<PageCandidate>> GetCandidatesAsync(Guid siteKey)
    {
        // With a hostname bound, absolute URLs carry it. Without one - nothing bound, or only paths such as "/en" -
        // Umbraco would make them absolute against the current request, and outside a request that's a placeholder
        // host (http://localhost), so ask for paths and let the scan resolve them against its base URL.
        var rootId = contentCache.GetById(siteKey)?.Id;
        var hasDomain = (await domains.GetAllAsync(false)).Any(d => d.RootContentId == rootId && !d.IsWildcard && BoundDomains.NamesHost(d.DomainName));
        var mode = hasDomain ? UrlMode.Absolute : UrlMode.Relative;

        using var context = umbracoContextFactory.EnsureUmbracoContext();
        var keys = new List<Guid> { siteKey };
        if (navigation.TryGetDescendantsKeys(siteKey, out var descendants)) keys.AddRange(descendants);

        var candidates = new List<PageCandidate>();
        foreach (var key in keys)
        {
            if (contentCache.GetById(key) is not { } content) continue; // not published
            var isProtected = publicAccess.IsProtected(content.Path).Success;
            var hasTemplate = content.TemplateId is > 0;
            var isVariant = content.ContentType.VariesByCulture();

            // Invariant content is scanned once with culture "".
            var published = isVariant ? content.Cultures.Keys.Where(c => c.Length > 0).ToList() : [string.Empty];
            foreach (var culture in published)
            {
                var url = urlProvider.GetUrl(content, mode, culture.Length == 0 ? null : culture);
                var name = content.Cultures.TryGetValue(culture, out var info) && !string.IsNullOrEmpty(info.Name) ? info.Name : content.Name;
                var others = published.Where(c => c.Length > 0 && !c.Equals(culture, StringComparison.OrdinalIgnoreCase)).ToList();
                candidates.Add(new PageCandidate(key, name, culture, url, isProtected, hasTemplate, key == siteKey, isVariant, others));
            }
        }

        return candidates;
    }

    public async Task<IReadOnlyList<string>> GetSiteCulturesAsync(Guid siteKey)
    {
        if (contentCache.GetById(siteKey) is { } root)
        {
            var bound = (await domains.GetAllAsync(false))
                .Where(d => d.RootContentId == root.Id && !string.IsNullOrEmpty(d.LanguageIsoCode))
                .Select(d => d.LanguageIsoCode!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (bound.Count > 0) return bound;
        }

        return (await languages.GetAllAsync()).Select(l => l.IsoCode).ToList();
    }
}
