using Esatto.Umbraco.Backoffice.CookieBanner;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentModeScriptTests
{
    [Fact]
    public void Defaults_deny_every_signal()
    {
        // Pins the pre-consent state Consent Mode v2 requires: every signal denied before any
        // Google tag can read it, plus the wait_for_update window the double Update() closes.
        var script = ConsentModeScript.Defaults();

        Assert.Contains("'ad_storage':'denied'", script);
        Assert.Contains("'ad_user_data':'denied'", script);
        Assert.Contains("'ad_personalization':'denied'", script);
        Assert.Contains("'analytics_storage':'denied'", script);
        Assert.Contains("'functionality_storage':'denied'", script);
        Assert.Contains("'personalization_storage':'denied'", script);
        Assert.Contains("'wait_for_update':500", script);
        Assert.DoesNotContain("granted", script);
    }

    [Fact]
    public void Statistics_grants_only_analytics_storage()
    {
        // Pins the category -> signal mapping: statistics must not leak into the ad signals.
        var script = ConsentModeScript.Update(new FakeConsentState(ConsentCategory.Statistics));

        Assert.Contains("'ad_storage':'denied'", script);
        Assert.Contains("'ad_user_data':'denied'", script);
        Assert.Contains("'ad_personalization':'denied'", script);
        Assert.Contains("'analytics_storage':'granted'", script);
        Assert.Contains("'functionality_storage':'denied'", script);
        Assert.Contains("'personalization_storage':'denied'", script);
    }

    [Fact]
    public void Marketing_grants_the_three_ad_signals()
    {
        // Pins that marketing consent drives exactly ad_storage, ad_user_data and ad_personalization.
        var script = ConsentModeScript.Update(new FakeConsentState(ConsentCategory.Marketing));

        Assert.Contains("'ad_storage':'granted'", script);
        Assert.Contains("'ad_user_data':'granted'", script);
        Assert.Contains("'ad_personalization':'granted'", script);
        Assert.Contains("'analytics_storage':'denied'", script);
        Assert.Contains("'functionality_storage':'denied'", script);
        Assert.Contains("'personalization_storage':'denied'", script);
    }

    [Fact]
    public void Preferences_grants_functionality_and_personalization()
    {
        // Pins that preferences maps to the two storage signals and nothing else.
        var script = ConsentModeScript.Update(new FakeConsentState(ConsentCategory.Preferences));

        Assert.Contains("'ad_storage':'denied'", script);
        Assert.Contains("'ad_user_data':'denied'", script);
        Assert.Contains("'ad_personalization':'denied'", script);
        Assert.Contains("'analytics_storage':'denied'", script);
        Assert.Contains("'functionality_storage':'granted'", script);
        Assert.Contains("'personalization_storage':'granted'", script);
    }

    [Fact]
    public void Nothing_granted_denies_everything()
    {
        // Pins that an update for a visitor who granted nothing cannot contain a single grant.
        var script = ConsentModeScript.Update(new FakeConsentState());

        Assert.DoesNotContain("granted", script);
    }

    [Fact]
    public void Config_emits_js_and_config_calls_with_the_measurement_id()
    {
        // Pins that the destination is registered and the initial page view fired.
        var script = ConsentModeScript.Config("G-ABC123");

        Assert.Contains("gtag('js',new Date())", script);
        Assert.Contains("gtag('config',\"G-ABC123\")", script);
    }

    [Fact]
    public void Config_safely_encodes_a_measurement_id_that_could_break_out_of_the_script()
    {
        // Pins the injection guard: a configured id is spliced into an inline <script>, so it must
        // go through a JSON encoder that escapes '<' and '>'.
        var script = ConsentModeScript.Config("</script><script>alert(1)");

        Assert.DoesNotContain("</script><script>", script);
    }
}
