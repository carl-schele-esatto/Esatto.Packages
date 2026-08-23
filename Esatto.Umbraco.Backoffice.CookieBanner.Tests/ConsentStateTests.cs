using Esatto.Umbraco.Backoffice.CookieBanner;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentStateTests
{
    private static IConsentState StateFor(string? cookieValue, int policyVersion = 1)
    {
        var options = new CookieBannerOptions { PolicyVersion = policyVersion };
        var httpContext = new DefaultHttpContext();

        if (cookieValue is not null)
        {
            // A real browser echoes back exactly the percent-encoded text the Set-Cookie response
            // gave it. ConsentCookieCodec.Encode returns plain JSON (the cookie layer, not the
            // codec, does the one encoding pass), so this helper must apply that encoding itself to
            // build a realistic raw Cookie header — otherwise characters like '"' and ',' break
            // RFC 6265 cookie-value grammar before ConsentState ever sees them.
            httpContext.Request.Headers.Cookie = $"{options.CookieName}={Uri.EscapeDataString(cookieValue)}";
        }

        return new ConsentState(
            new HttpContextAccessor { HttpContext = httpContext },
            Options.Create(options));
    }

    private static string CookieFor(int version, params ConsentCategory[] granted)
        => ConsentCookieCodec.Encode(
            new ConsentDecision(version, DateTimeOffset.UtcNow, "abc123", granted.ToHashSet()));

    [Fact]
    public void Needs_a_decision_when_no_cookie_is_present()
    {
        // Pins the blocking-banner guarantee: a first-time visitor must be prompted.
        IConsentState state = StateFor(null);

        Assert.True(state.NeedsDecision);
        Assert.Null(state.Decision);
    }

    [Fact]
    public void Necessary_is_granted_even_without_a_decision()
        // Pins that necessary cookies are never gated behind consent.
        => Assert.True(StateFor(null).HasGranted(ConsentCategory.Necessary));

    [Fact]
    public void Non_necessary_is_denied_without_a_decision()
    {
        // Pins deny-by-default: no cookie must never mean "granted".
        IConsentState state = StateFor(null);

        Assert.False(state.HasGranted(ConsentCategory.Statistics));
        Assert.False(state.HasGranted(ConsentCategory.Marketing));
        Assert.False(state.HasGranted(ConsentCategory.Preferences));
    }

    [Fact]
    public void Reads_granted_categories_from_the_cookie()
    {
        // Pins the cookie -> state read path that gates <consent-script>.
        IConsentState state = StateFor(CookieFor(1, ConsentCategory.Statistics));

        Assert.False(state.NeedsDecision);
        Assert.True(state.HasGranted(ConsentCategory.Statistics));
        Assert.False(state.HasGranted(ConsentCategory.Marketing));
    }

    [Fact]
    public void An_outdated_policy_version_denies_everything_and_reprompts()
    {
        // Pins why PolicyVersion exists: reworded cookie text must re-prompt and grant nothing
        // in the meantime, while the old decision stays readable for pre-selection.
        IConsentState state = StateFor(CookieFor(1, ConsentCategory.Statistics), policyVersion: 2);

        Assert.True(state.NeedsDecision);
        Assert.False(state.HasGranted(ConsentCategory.Statistics));
        Assert.True(state.HasGranted(ConsentCategory.Necessary));
        Assert.NotNull(state.Decision);
    }

    [Fact]
    public void A_corrupt_cookie_is_treated_as_no_decision()
    {
        // Pins that a hand-edited cookie degrades to "no decision" rather than throwing mid-render.
        IConsentState state = StateFor("garbage");

        Assert.True(state.NeedsDecision);
        Assert.False(state.HasGranted(ConsentCategory.Statistics));
    }

    [Fact]
    public void Survives_having_no_http_context()
    {
        // Pins safety outside a request (background work, view rendered from a null accessor).
        IConsentState state = new ConsentState(
            new HttpContextAccessor { HttpContext = null },
            Options.Create(new CookieBannerOptions()));

        Assert.True(state.NeedsDecision);
        Assert.False(state.HasGranted(ConsentCategory.Statistics));
    }
}
