using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

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
    AppCaches appCaches,
    IOptions<CookieBannerOptions> options,
    ILogger<CookiePolicyPageResolver> logger) : ICookiePolicyPageResolver
{
    internal const string ContentTypeAlias = "cookiePolicy";

    /// <summary>
    /// Runtime cache key for the by-type scan's result (see <see cref="ResolveCore"/>). Internal so
    /// <see cref="CookiePolicyPageCacheInvalidator"/> and tests can share the exact same key.
    /// </summary>
    internal const string RuntimeCacheKey =
        "Esatto.Umbraco.Backoffice.CookieBanner.CookiePolicyPageResolver.PolicyPageKey";

    /// <summary>
    /// A site with more policy pages than this has bigger problems than the banner. The cap keeps
    /// the fallback scan to one bounded query.
    /// </summary>
    private const int ScanPageSize = 100;

    /// <summary>
    /// Backstop expiry for the cached policy-page key (see <see cref="RuntimeCacheKey"/> and
    /// <see cref="ResolveCore"/>). <see cref="CookiePolicyPageCacheInvalidator"/>'s
    /// publish/unpublish/delete notifications are the fast path, but Umbraco only raises them on the
    /// server instance that actually performed the edit - so on a load-balanced install a front-end
    /// replica that did not handle it never gets the clear, and without a bounded expiry it would
    /// hold a stale key (or a cached "no policy page yet") for the whole process lifetime: publish
    /// the first cookiePolicy page after boot and that replica shows no policy link until it
    /// restarts. A resolved key is cheap to recompute (one bounded content query) and this is a
    /// rarely-changing lookup, so a few minutes is ample headroom - short enough that a stale
    /// replica self-heals long before anyone would notice, long enough that the scan still only runs
    /// occasionally rather than on every request.
    /// </summary>
    private static readonly TimeSpan PolicyPageKeyCacheDuration = TimeSpan.FromMinutes(5);

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
            // Deliberately not cached: caching "none" here would keep answering that way even
            // after the schema installs moments later, and this class has no notification that
            // would tell it to stop.
            return null;
        }

        // The scan below is a core scope, a content read lock and a paged database query - and
        // <consent-banner /> is unconditional, so without this it ran once per visitor per page.
        // Only the resolved KEY is cached, never the IPublishedContent itself: re-resolving through
        // contentCache.GetById(...) below is a cheap in-memory published-cache lookup that always
        // reflects the current publish state, so a stale cache entry can at worst trigger one extra
        // scan - it can never serve stale content. Guid.Empty stands in for "scanned, found
        // nothing", so a site with no policy page does not re-scan on every request either.
        // Invalidated by CookiePolicyPageCacheInvalidator on publish/unpublish/delete - belt and
        // braces, not a replacement for it: see PolicyPageKeyCacheDuration for why an expiry is
        // needed too. The 4-arg overload used here (TimeSpan? timeout) is confirmed identical on
        // IAppPolicyCache in both Umbraco 17.0.0 and 18.1.1 by decompiling the real assemblies.
        Guid cachedKey = appCaches.RuntimeCache.GetCacheItem(
            RuntimeCacheKey,
            () => ScanForPolicyPageKey(contentType) ?? Guid.Empty,
            PolicyPageKeyCacheDuration);

        return cachedKey == Guid.Empty ? null : contentCache.GetById(cachedKey);
    }

    private Guid? ScanForPolicyPageKey(IContentType contentType)
    {
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
                return candidate.Key;
            }
        }

        return null;
    }
}
