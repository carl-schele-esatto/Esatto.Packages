using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class CookieBannerServiceCollectionExtensionsTests
{
    [Fact]
    public void Registering_twice_leaves_one_registration_per_service()
    {
        // CookieBannerComposer calls AddCookieConsent() automatically, and the public
        // AddCookieConsent() is documented as safe to call as well. Only TryAdd* keeps that
        // idempotent — plain Add* would give ConsentThrottle two singletons and two budgets.
        var services = new ServiceCollection();

        services.AddCookieConsent();
        services.AddCookieConsent();

        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(IConsentState)));
        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(ConsentCookieWriter)));
        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(IConsentThrottle)));
        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(ConsentEndpointHandler)));
    }

    [Fact]
    public void The_consent_graph_resolves_from_the_container()
    {
        // Pins the lifetimes: ConsentState is scoped, so validateScopes catches a singleton that
        // captures it, and the throttle's TimeProvider dependency must be registered too.
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCookieConsent();

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

        Assert.NotNull(provider.GetRequiredService<ConsentCookieWriter>());
        Assert.NotNull(provider.GetRequiredService<IConsentThrottle>());
        Assert.NotNull(provider.GetRequiredService<ConsentEndpointHandler>());

        using IServiceScope scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IConsentState>());
    }

    [Fact]
    public void An_absent_configuration_section_leaves_the_defaults_intact()
    {
        // BindConfiguration against a missing "Esatto:CookieBanner" section must not blank the
        // options: a consumer with no appsettings entry still gets a working endpoint path.
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddCookieConsent();

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        CookieBannerOptions options = provider.GetRequiredService<IOptions<CookieBannerOptions>>().Value;

        Assert.Equal("/api/cookie-consent", options.EndpointPath);
        Assert.Equal(10, options.ThrottleRequestsPerMinute);
    }
}
