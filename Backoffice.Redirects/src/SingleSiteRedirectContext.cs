namespace Backoffice.Redirects;

/// <summary>
/// Default <see cref="IRedirectSiteContext"/> implementation for single-site
/// installs. Returns an empty allowed-sites list and an empty request site
/// key — the dashboard hides its site-switcher and the content finder
/// treats every request as belonging to the unscoped (empty) site.
/// </summary>
/// <remarks>
/// Registered automatically by <c>AddBackofficeRedirectsSingleSite()</c>.
/// </remarks>
public sealed class SingleSiteRedirectContext : IRedirectSiteContext
{
    private static readonly IReadOnlyList<RedirectSite> EmptySites = Array.Empty<RedirectSite>();

    public IReadOnlyList<RedirectSite> GetAllowedSites() => EmptySites;

    public string ResolveForCurrentRequest() => string.Empty;
}
