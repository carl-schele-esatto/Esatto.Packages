using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Esatto.Umbraco.Backoffice.CookieBanner.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Options;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentHeadTagHelperTests
{
    private const string StylesheetLink = """<link rel="stylesheet" href="/esatto-cookiebanner/consent.css" />""";

    private static TagHelperContext Context() => new(
        new TagHelperAttributeList(),
        new Dictionary<object, object>(),
        Guid.NewGuid().ToString());

    private static TagHelperOutput Output() => new(
        "consent-head",
        new TagHelperAttributeList(),
        (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

    private static TagHelperOutput Render(IConsentState consent, CookieBannerOptions options)
    {
        var helper = new ConsentHeadTagHelper(consent, Options.Create(options));
        TagHelperOutput output = Output();
        helper.Process(Context(), output);
        return output;
    }

    [Fact]
    public void Emits_nothing_google_related_without_a_measurement_id()
    {
        // Pins "no dead script on every page": with no measurement id the Consent Mode block and the
        // gtag tag are absent entirely, not merely inert.
        var html = Render(new FakeConsentState(ConsentCategory.Statistics), new CookieBannerOptions())
            .Content.GetContent();

        Assert.DoesNotContain("gtag", html);
        Assert.DoesNotContain("googletagmanager", html);
        Assert.DoesNotContain("<script", html);
    }

    [Fact]
    public void Emits_defaults_then_update_then_config_in_that_order()
    {
        // Load-bearing order: 'default' must precede any Google tag, the immediately following
        // 'update' closes the 500ms wait_for_update window, and only then does config fire the first
        // page view. Reordering these silently sends wrongly-denied signals.
        var html = Render(
                new FakeConsentState(ConsentCategory.Statistics),
                new CookieBannerOptions { GoogleMeasurementId = "G-ABC123" })
            .Content.GetContent();

        var defaults = html.IndexOf("'consent','default'", StringComparison.Ordinal);
        var update = html.IndexOf("'consent','update'", StringComparison.Ordinal);
        var config = html.IndexOf("gtag('config'", StringComparison.Ordinal);

        Assert.True(defaults >= 0, "the consent default call is missing");
        Assert.True(defaults < update, "the update call must follow the defaults call");
        Assert.True(update < config, "the config call must come last");
        Assert.Contains("'wait_for_update':500", html);
    }

    [Fact]
    public void Always_emits_the_package_stylesheet()
    {
        // The dialog must be styled on every site, whether or not Google Consent Mode is in play.
        Assert.Contains(StylesheetLink, Render(new FakeConsentState(), new CookieBannerOptions()).Content.GetContent());
        Assert.Contains(
            StylesheetLink,
            Render(new FakeConsentState(), new CookieBannerOptions { GoogleMeasurementId = "G-ABC123" })
                .Content.GetContent());
    }

    [Fact]
    public void The_gtag_library_is_gated_on_statistics_consent()
    {
        // Same server-side gate as <consent-script category="Statistics">: with statistics declined
        // the library never reaches the browser, so there is no window in which it could execute.
        var options = new CookieBannerOptions { GoogleMeasurementId = "G-ABC123" };

        Assert.DoesNotContain(
            "googletagmanager.com",
            Render(new FakeConsentState(), options).Content.GetContent());
        Assert.Contains(
            "googletagmanager.com/gtag/js?id=G-ABC123",
            Render(new FakeConsentState(ConsentCategory.Statistics), options).Content.GetContent());
    }

    [Fact]
    public void Leaves_no_consent_head_element_in_the_output()
    {
        // <consent-head> is a marker; an unknown element in <head> would be invalid markup.
        Assert.Null(Render(new FakeConsentState(), new CookieBannerOptions()).TagName);
    }
}
