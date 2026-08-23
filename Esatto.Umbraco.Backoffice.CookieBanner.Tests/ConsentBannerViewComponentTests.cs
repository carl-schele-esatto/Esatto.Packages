using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core.Models.PublishedContent;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentBannerViewComponentTests
{
    private static ConsentBannerViewModel Model(IConsentState consent, CookieBannerOptions options)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICookiePolicyPageResolver>(new NoPolicyPageResolver());
        var text = new EchoTextProvider();
        services.AddSingleton<IConsentTextProvider>(text);

        var component = new ConsentBannerViewComponent(
            consent,
            Options.Create(options),
            Substitute.For<IPublishedValueFallback>(),
            text,
            services.BuildServiceProvider());

        return component.BuildModel();
    }

    [Fact]
    public void Degrades_to_an_empty_registry_when_no_policy_page_is_published()
    {
        // Policy-page resolution is best-effort. The NDSTK partial degraded through four possibly
        // absent steps; the package must still render four fieldsets rather than throw mid-request.
        ConsentBannerViewModel model = Model(
            new FakeConsentState { NeedsDecision = true },
            new CookieBannerOptions());

        Assert.Equal(ConsentCategories.All.ToArray(), model.CookiesByCategory.Keys.ToArray());
        Assert.All(model.CookiesByCategory.Values, declarations => Assert.Empty(declarations));
    }

    [Fact]
    public void Carries_the_configured_cookie_name_version_and_endpoint_into_the_model()
    {
        // A package must not bake in a site's cookie name or endpoint: NDSTK pins CookieName back to
        // ndstk-consent precisely so no existing visitor is re-prompted.
        ConsentBannerViewModel model = Model(
            new FakeConsentState(),
            new CookieBannerOptions
            {
                CookieName = "ndstk-consent",
                PolicyVersion = 7,
                EndpointPath = "/api/consent",
            });

        Assert.Equal("ndstk-consent", model.CookieName);
        Assert.Equal(7, model.PolicyVersion);
        Assert.Equal("/api/consent", model.EndpointPath);
    }

    [Fact]
    public void Consent_mode_is_off_until_a_measurement_id_is_configured()
    {
        // data-consent-mode drives whether consent.js re-signals gtag; with no id there is nothing
        // to signal and the head block is never emitted either.
        Assert.False(Model(new FakeConsentState(), new CookieBannerOptions()).ConsentModeEnabled);
        Assert.True(Model(
            new FakeConsentState(),
            new CookieBannerOptions { GoogleMeasurementId = "G-ABC123" }).ConsentModeEnabled);
    }

    [Fact]
    public void Granted_follows_HasGranted_so_a_stale_decision_pre_ticks_nothing()
    {
        // _ConsentBanner.cshtml read Decision.Granted directly, which ticks Statistics for a visitor
        // whose decision predates the current PolicyVersion - even though the gating code grants
        // nothing to that visitor. HasGranted is the single source of truth.
        ConsentBannerViewModel model = Model(
            new FakeConsentState(ConsentCategory.Statistics) { NeedsDecision = true },
            new CookieBannerOptions());

        Assert.Equal(new[] { ConsentCategory.Necessary }, model.Granted.ToArray());
    }

    [Fact]
    public void Granted_contains_necessary_plus_every_actually_granted_category()
    {
        // Necessary is implied rather than stored, so it must be added back for the disabled,
        // always-checked box.
        ConsentBannerViewModel model = Model(
            new FakeConsentState(ConsentCategory.Statistics, ConsentCategory.Marketing),
            new CookieBannerOptions());

        Assert.Contains(ConsentCategory.Necessary, model.Granted);
        Assert.Contains(ConsentCategory.Statistics, model.Granted);
        Assert.Contains(ConsentCategory.Marketing, model.Granted);
        Assert.DoesNotContain(ConsentCategory.Preferences, model.Granted);
    }

    [Fact]
    public void Text_is_resolved_through_the_package_text_provider()
    {
        // Every string in the dialog goes through IConsentTextProvider (dictionary -> resx -> English),
        // which is what removes the 26 inline Swedish fallbacks from the view.
        ConsentBannerViewModel model = Model(new FakeConsentState(), new CookieBannerOptions());

        Assert.Equal("[Cookies.Banner.Heading]", model.Text("Cookies.Banner.Heading"));
    }

    // NSubstitute cannot proxy this assembly's internal interfaces - Castle would need an
    // InternalsVisibleTo for DynamicProxyGenAssembly2, which the package deliberately does not grant -
    // so the two internal services get hand-written fakes.
    private sealed class NoPolicyPageResolver : ICookiePolicyPageResolver
    {
        public IPublishedContent? Resolve() => null;
    }

    private sealed class EchoTextProvider : IConsentTextProvider
    {
        public string Get(string key) => $"[{key}]";
    }
}
