using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.DependencyInjection;

namespace Esatto.Umbraco.Backoffice.Redirects;

public static class BackofficeRedirectsServiceCollectionExtensions
{
    /// <summary>
    /// Registers Backoffice.Redirects services. Optional — the package's
    /// <see cref="RedirectsComposer"/> already registers the full graph
    /// automatically. Kept for explicitness; idempotent.
    /// </summary>
    public static IServiceCollection AddBackofficeRedirects(this IServiceCollection services)
    {
        services.TryAddSingleton<IRedirectService, RedirectService>();
        return services;
    }
}

public static class BackofficeRedirectsUmbracoBuilderExtensions
{
    public static IUmbracoBuilder AddBackofficeRedirects(this IUmbracoBuilder builder)
    {
        builder.Services.AddBackofficeRedirects();
        return builder;
    }
}
