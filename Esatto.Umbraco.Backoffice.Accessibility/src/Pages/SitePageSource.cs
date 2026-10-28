using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Navigation;

namespace Esatto.Umbraco.Backoffice.Accessibility.Pages;

/// <summary>Reads sites and their published pages from Umbraco. A thin adapter; the rules live in <see cref="PageListBuilder"/>.</summary>
public sealed class SitePageSource(
    IDocumentNavigationQueryService navigation,
    IPublishedContentCache contentCache,
    IPublishedUrlProvider urlProvider,
    IPublicAccessService publicAccess,
    IDomainService domains)
{
    public async Task<IReadOnlyList<SiteInfo>> GetSitesAsync()
    {
        if (!navigation.TryGetRootKeys(out var rootKeys)) return [];

        var allDomains = (await domains.GetAllAsync(false)).ToList();
        var sites = new List<SiteInfo>();
        foreach (var key in rootKeys)
        {
            if (contentCache.GetById(key) is not { } root) continue; // unpublished root
            var hosts = allDomains
                .Where(d => d.RootContentId == root.Id)
                .Select(d => d.DomainName)
                .ToList();
            sites.Add(new SiteInfo(key, root.Name, hosts));
        }

        return sites;
    }

    public Task<IReadOnlyList<PageCandidate>> GetCandidatesAsync(Guid siteKey)
    {
        var keys = new List<Guid> { siteKey };
        if (navigation.TryGetDescendantsKeys(siteKey, out var descendants)) keys.AddRange(descendants);

        var candidates = new List<PageCandidate>();
        foreach (var key in keys)
        {
            if (contentCache.GetById(key) is not { } content) continue; // not published
            var isProtected = publicAccess.IsProtected(content.Path).Success;
            var hasTemplate = content.TemplateId is > 0;

            // Invariant content has no cultures in the dictionary; scan it once with culture "".
            IEnumerable<string> cultures = content.Cultures.Count > 0 ? content.Cultures.Keys : [string.Empty];
            foreach (var culture in cultures)
            {
                var url = urlProvider.GetUrl(content, UrlMode.Absolute, culture.Length == 0 ? null : culture);
                // "#" (no route) or a relative URL (no domain bound) can't be loaded by the scanner as-is.
                var absolute = Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme is ("http" or "https") ? url : null;
                var name = content.Cultures.TryGetValue(culture, out var info) && !string.IsNullOrEmpty(info.Name) ? info.Name : content.Name;
                candidates.Add(new PageCandidate(key, name, culture, absolute, isProtected, hasTemplate));
            }
        }

        return Task.FromResult<IReadOnlyList<PageCandidate>>(candidates);
    }
}
