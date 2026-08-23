using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentCookieCodecTests
{
    private static ConsentDecision Decision(params ConsentCategory[] granted)
        => new(1, new DateTimeOffset(2026, 8, 21, 9, 12, 33, TimeSpan.Zero), "abc123", granted.ToHashSet());

    // Pins the round trip: everything the banner and the tag helpers read must survive a trip
    // through the cookie unchanged.
    [Fact]
    public void Round_trips_a_decision()
    {
        ConsentDecision original = Decision(ConsentCategory.Preferences, ConsentCategory.Statistics);

        ConsentDecision? decoded = ConsentCookieCodec.Decode(ConsentCookieCodec.Encode(original));

        Assert.NotNull(decoded);
        Assert.Equal(original.PolicyVersion, decoded.PolicyVersion);
        Assert.Equal(original.DecidedAt, decoded.DecidedAt);
        Assert.Equal(original.ConsentId, decoded.ConsentId);
        Assert.Equal(
            new[] { ConsentCategory.Preferences, ConsentCategory.Statistics }.ToHashSet(),
            decoded.Granted.ToHashSet());
    }

    // Pins the exact documented wire shape - property order, ordinal-sorted categories, compact
    // JSON, ISO-8601 offset timestamp - because cookies in the wild are decoded by this format and
    // by the browser-side reader in consent.js.
    [Fact]
    public void Encodes_the_documented_wire_shape()
    {
        var encoded = ConsentCookieCodec.Encode(
            Decision(ConsentCategory.Statistics, ConsentCategory.Marketing, ConsentCategory.Preferences));

        Assert.Equal(
            """{"v":1,"t":"2026-08-21T09:12:33+00:00","c":["marketing","preferences","statistics"],"id":"abc123"}""",
            encoded);
    }

    // Pins that necessary is never written to the cookie, and that Encode emits plain JSON -
    // Response.Cookies.Append does the one and only URL-encoding pass, so asserting against the
    // escaped form here would hide a regression back to double encoding.
    [Fact]
    public void Omits_necessary_from_the_wire_format()
    {
        var encoded = ConsentCookieCodec.Encode(Decision(ConsentCategory.Necessary, ConsentCategory.Marketing));

        Assert.DoesNotContain("necessary", encoded);
        Assert.Contains("marketing", encoded);
    }

    // Pins that a decision with nothing granted still grants necessary after a full round trip.
    [Fact]
    public void Necessary_is_always_granted_even_when_absent()
    {
        ConsentDecision? decoded = ConsentCookieCodec.Decode(ConsentCookieCodec.Encode(Decision()));

        Assert.NotNull(decoded);
        Assert.True(decoded.HasGranted(ConsentCategory.Necessary));
        Assert.False(decoded.HasGranted(ConsentCategory.Statistics));
    }

    // Pins that decoding is total: malformed, truncated or hand-edited values mean "no decision",
    // never an exception on a front-end request.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-json")]
    [InlineData("%7B%22v%22%3A")]
    [InlineData("%7B%7D")]
    public void Returns_null_for_unusable_input(string? value)
        => Assert.Null(ConsentCookieCodec.Decode(value));

    // Pins forward compatibility: a category name this build does not know is dropped rather than
    // invalidating the whole cookie.
    [Fact]
    public void Ignores_unknown_categories()
    {
        // Plain JSON, exactly the shape Request.Cookies hands to Decode in production - never
        // percent-encoded, since the framework already decoded it once by the time Decode sees it.
        var json = """{"v":1,"t":"2026-08-21T09:12:33+00:00","c":["statistics","telepathy"],"id":"abc123"}""";

        ConsentDecision? decoded = ConsentCookieCodec.Decode(json);

        Assert.NotNull(decoded);
        Assert.Equal([ConsentCategory.Statistics], decoded.Granted.ToArray());
    }

    // Pins the corrected contract: if Decode is ever made to unescape again - reintroducing the
    // double-decode bug - a value that has already been through one round of percent-encoding
    // would start parsing successfully again. It must not.
    [Fact]
    public void A_url_encoded_cookie_value_does_not_decode_to_a_decision()
    {
        var encoded = ConsentCookieCodec.Encode(Decision(ConsentCategory.Statistics));
        var doubleEncoded = Uri.EscapeDataString(encoded);

        Assert.Null(ConsentCookieCodec.Decode(doubleEncoded));
    }

    // Pins the consent id shape: 22 base64url chars, no '+', '/' or '=', so it survives a cookie
    // value and a URL without escaping.
    [Fact]
    public void New_consent_id_is_url_safe_and_unique()
    {
        var first = ConsentCookieCodec.NewConsentId();
        var second = ConsentCookieCodec.NewConsentId();

        Assert.NotEqual(first, second);
        Assert.Equal(22, first.Length);
        Assert.DoesNotContain('+', first);
        Assert.DoesNotContain('/', first);
        Assert.DoesNotContain('=', first);
    }
}
