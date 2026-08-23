using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.DependencyInjection;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

public static class CookieBannerServiceCollectionExtensions
{
    /// <summary>
    /// Registers the cookie-consent service graph. Optional — <see cref="CookieBannerComposer"/>
    /// already calls this automatically. Kept for explicitness; idempotent.
    /// </summary>
    public static IServiceCollection AddCookieConsent(this IServiceCollection services)
    {
        // ConsentState reads the cookie off the ambient request.
        services.AddHttpContextAccessor();

        services.AddOptions<CookieBannerOptions>()
            .BindConfiguration(CookieBannerOptions.SectionName);

        // ConsentThrottle's injectable clock. Not registered by the host by default.
        services.TryAddSingleton(TimeProvider.System);

        // Scoped: the cookie is parsed at most once per request however many tag helpers ask.
        services.TryAddScoped<IConsentState, ConsentState>();

        services.TryAddSingleton<ConsentCookieWriter>();

        // Singleton, or every request would get a fresh window and no throttle at all.
        services.TryAddSingleton<IConsentThrottle, ConsentThrottle>();

        services.TryAddSingleton<ConsentEndpointHandler>();

        // Scoped: resolves against the current request's culture via ICultureDictionaryFactory.
        services.TryAddScoped<IConsentTextProvider, ConsentTextProvider>();

        // Scoped: resolves against the current request's published content, and memoises its
        // answer for one request so <consent-banner /> and the policy template share a lookup.
        // TryAdd keeps this idempotent alongside CookieBannerComposer, which registers the same
        // pair for the auto-discovered install path.
        services.TryAddScoped<ICookiePolicyPageResolver, CookiePolicyPageResolver>();

        return services;
    }
}

public static class CookieBannerUmbracoBuilderExtensions
{
    /// <summary>
    /// Registers the cookie-consent service graph on an <see cref="IUmbracoBuilder"/>. Optional
    /// for the same reason as the <see cref="IServiceCollection"/> overload.
    /// </summary>
    public static IUmbracoBuilder AddCookieConsent(this IUmbracoBuilder builder)
    {
        builder.Services.AddCookieConsent();
        return builder;
    }
}
