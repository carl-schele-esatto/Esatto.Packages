using System.Text.Json;
using Esatto.Umbraco.Backoffice.CookieBanner;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentStateResponseTests
{
    [Fact]
    public void Serialises_to_the_camel_cased_shape_consent_js_reads()
    {
        // Pins the response wire contract the banner uses to unblock scripts without a reload:
        // renaming a member here silently breaks consent.js, which has no compiler to catch it.
        var json = JsonSerializer.Serialize(
            new ConsentStateResponse(
                3,
                ["marketing", "statistics"],
                "abc123",
                "2026-08-23T10:00:00.0000000+00:00"),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        // System.Text.Json's default encoder is conservative: even under JsonSerializerDefaults.Web
        // (no Encoder override applied here) it escapes the plus sign as a six-character unicode
        // escape below. JSON.parse resolves that back to a literal plus sign identically, so this
        // is the real wire text, not a laxer stand-in for it.
        Assert.Equal(
            """{"version":3,"categories":["marketing","statistics"],"consentId":"abc123","decidedAt":"2026-08-23T10:00:00.0000000\u002B00:00"}""",
            json);
    }
}
