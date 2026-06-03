namespace Backoffice.Redirects;

/// <summary>
/// A site that the redirects dashboard can list and the runtime can scope to.
/// </summary>
public sealed record RedirectSite(string Key, string Label);

/// <summary>
/// Plugin contract for resolving which "site" a redirect belongs to.
/// </summary>
/// <remarks>
/// <para>
/// The package treats the site key as an opaque string; the consumer decides
/// what it means (a hostname, a culture, a tenant ID, a startPage GUID,
/// anything). The default <see cref="SingleSiteRedirectContext"/>
/// implementation returns an empty list and an empty key — i.e. single-site
/// mode, no tab bar in the dashboard, no per-site scoping at runtime.
/// </para>
/// <para>
/// Multi-site consumers register their own implementation via
/// <c>builder.Services.AddBackofficeRedirectsMultiSite&lt;TContext&gt;()</c>.
/// </para>
/// </remarks>
public interface IRedirectSiteContext
{
    /// <summary>
    /// Sites the current backoffice user is allowed to manage. Used by the
    /// dashboard to render the site-switcher tabs and by the controller to
    /// gate create/update/delete operations.
    /// </summary>
    /// <returns>
    /// Empty list = single-site mode (dashboard hides the tab bar; all
    /// operations target the empty-string site key).
    /// </returns>
    IReadOnlyList<RedirectSite> GetAllowedSites();

    /// <summary>
    /// Site key for the current incoming front-end request. Used by the
    /// <c>RedirectContentFinder</c> to scope its lookup so a redirect for
    /// site A doesn't fire on requests to site B.
    /// </summary>
    /// <returns>Empty string in single-site mode.</returns>
    string ResolveForCurrentRequest();
}
