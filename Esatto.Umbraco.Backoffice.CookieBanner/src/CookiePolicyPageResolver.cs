using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Resolves the policy page by document type instead of by a picker property.
/// </summary>
/// <remarks>
/// The feature this package was extracted from added a <c>cookiePolicyPage</c> Content Picker to
/// the CONSUMING site's <c>settings</c> document type and read the page out of it. A package
/// cannot add properties to a document type it does not own, and that single cross-model write is
/// the entire reason the old site needed a hand-written upgrade document plus four manual
/// backoffice steps.
/// <para>
/// Instead: the first published node of type <see cref="ContentTypeAlias" />, with
/// <see cref="CookieBannerOptions.PolicyPageKey" /> as an explicit override for a site that has
/// more than one. No manual backoffice step, no schema write outside the package's own GUIDs.
/// </para>
/// </remarks>
internal sealed class CookiePolicyPageResolver(
    IPublishedContentCache contentCache,
    IContentTypeService contentTypeService,
    IContentService contentService,
    IOptions<CookieBannerOptions> options,
    ILogger<CookiePolicyPageResolver> logger) : ICookiePolicyPageResolver
{
    internal const string ContentTypeAlias = "cookiePolicy";

    /// <summary>
    /// A site with more policy pages than this has bigger problems than the banner. The cap keeps
    /// the fallback scan to one bounded query.
    /// </summary>
    private const int ScanPageSize = 100;

    // Registered scoped, so this memoises for the lifetime of one request: <consent-banner /> and
    // the policy template can both ask without paying for a second database round trip.
    private bool _resolved;
    private IPublishedContent? _page;

    public IPublishedContent? Resolve()
    {
        if (_resolved)
        {
            return _page;
        }

        _page = ResolveCore();
        _resolved = true;
        return _page;
    }

    private IPublishedContent? ResolveCore()
    {
        if (options.Value.PolicyPageKey is Guid key)
        {
            // An explicit override wins outright. Falling back to a by-type scan here would
            // silently point the banner at a different page than the one that was configured.
            IPublishedContent? configured = contentCache.GetById(key);
            if (configured is null)
            {
                logger.LogWarning(
                    "{Option} is set to {Key} but no published content with that key exists.",
                    $"{CookieBannerOptions.SectionName}:PolicyPageKey",
                    key);
            }

            return configured;
        }

        IContentType? contentType = contentTypeService.Get(ContentTypeAlias);
        if (contentType is null)
        {
            // The schema installer has not run yet (first boot, or it failed and logged).
            return null;
        }

        // IContentService.GetPagedOfType declares `filter` as non-nullable on the interface even
        // though passing null for "no filter" is the documented, supported usage - an annotation
        // mismatch in the shipped API, not a real nullability risk here.
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        IEnumerable<IContent> candidates =
            contentService.GetPagedOfType(contentType.Id, 0, ScanPageSize, out _, null, null);
#pragma warning restore CS8625

        foreach (IContent candidate in candidates)
        {
            // The non-preview published cache returns null for a node that is not published, so
            // this filters to published nodes without a second service.
            IPublishedContent? published = contentCache.GetById(candidate.Key);
            if (published is not null)
            {
                return published;
            }
        }

        return null;
    }
}
