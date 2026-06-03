using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.DependencyInjection;

namespace Backoffice.Redirects;

public static class BackofficeRedirectsServiceCollectionExtensions
{
    /// <summary>
    /// Registers Backoffice.Redirects in single-site mode. The dashboard's
    /// site-switcher tab bar is hidden; all redirects are unscoped (siteKey
    /// = empty string).
    /// </summary>
    /// <remarks>
    /// Single-site is already the default registered automatically by
    /// <see cref="RedirectsComposer"/>, so calling this is optional. It's kept
    /// for explicitness and back-compat; it simply ensures the default graph.
    /// </remarks>
    public static IServiceCollection AddBackofficeRedirectsSingleSite(this IServiceCollection services)
    {
        services.TryAddSingleton<IRedirectService, RedirectService>();
        services.TryAddSingleton<IRedirectSiteContext, SingleSiteRedirectContext>();
        return services;
    }

    /// <summary>
    /// Registers Backoffice.Redirects in multi-site mode. The dashboard
    /// renders a tab per site returned by your <typeparamref name="TContext"/>
    /// implementation; the runtime content finder scopes redirect lookups by
    /// the site key your context resolves from the request.
    /// </summary>
    /// <typeparam name="TContext">Your <see cref="IRedirectSiteContext"/> implementation.</typeparam>
    public static IServiceCollection AddBackofficeRedirectsMultiSite<TContext>(this IServiceCollection services)
        where TContext : class, IRedirectSiteContext
    {
        services.TryAddSingleton<IRedirectService, RedirectService>();
        // Replace (not Add) the default single-site context registered by the
        // composer, so resolution is unambiguous regardless of call order.
        services.Replace(ServiceDescriptor.Singleton<IRedirectSiteContext, TContext>());
        return services;
    }
}

/// <summary>
/// Convenience extensions for registering Backoffice.Redirects against
/// Umbraco's <see cref="IUmbracoBuilder"/> instead of
/// <see cref="IServiceCollection"/> directly.
/// </summary>
public static class BackofficeRedirectsUmbracoBuilderExtensions
{
    public static IUmbracoBuilder AddBackofficeRedirectsSingleSite(this IUmbracoBuilder builder)
    {
        builder.Services.AddBackofficeRedirectsSingleSite();
        return builder;
    }

    public static IUmbracoBuilder AddBackofficeRedirectsMultiSite<TContext>(this IUmbracoBuilder builder)
        where TContext : class, IRedirectSiteContext
    {
        builder.Services.AddBackofficeRedirectsMultiSite<TContext>();
        return builder;
    }
}
