using Umbraco.Cms.Core.Models.PublishedContent;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Finds the site's cookie policy page. Internal: consumers configure it through
/// <see cref="CookieBannerOptions.PolicyPageKey"/> rather than by implementing this.
/// </summary>
internal interface ICookiePolicyPageResolver
{
    /// <summary>The policy page, or <c>null</c> when the site has none published.</summary>
    IPublishedContent? Resolve();
}
