using System.Text.Json;
using Esatto.Umbraco.Backoffice.CookieBanner;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentCookieWriterTests
{
    private static (ConsentCookieWriter Writer, DefaultHttpContext Context) Build(
        int policyVersion = 1,
        int cookieLifetimeDays = 365,
        string cookieName = "cookie-consent")
    {
        IOptions<CookieBannerOptions> options = Options.Create(new CookieBannerOptions
        {
            PolicyVersion = policyVersion,
            CookieLifetimeDays = cookieLifetimeDays,
            CookieName = cookieName,
        });

        return (new ConsentCookieWriter(options), new DefaultHttpContext());
    }

    private static string SetCookieHeader(DefaultHttpContext context)
    {
        IEnumerable<string> headers = context.Response.Headers.SetCookie
            .Where(value => value is not null)
            .Select(value => value!);

        return Assert.Single(headers);
    }

    [Fact]
    public void Writes_the_cookie_with_the_documented_attributes()
    {
        // Pins Path=/, SameSite=Lax and the deliberate absence of HttpOnly: the banner reads this
        // cookie from JavaScript to unblock scripts without a reload.
        (ConsentCookieWriter writer, DefaultHttpContext context) = Build();

        writer.Write(context.Response, context.Request, ConsentAction.AcceptAll, ["statistics", "marketing"]);

        var header = SetCookieHeader(context);
        Assert.Contains("cookie-consent=", header);
        Assert.Contains("path=/", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", header, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("httponly", header, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Secure_attribute_tracks_the_request_scheme(bool isHttps)
    {
        // Pins that Secure follows the actual scheme: hardcoding it breaks local http development,
        // omitting it leaks the cookie over http in production.
        (ConsentCookieWriter writer, DefaultHttpContext context) = Build();
        context.Request.IsHttps = isHttps;

        writer.Write(context.Response, context.Request, ConsentAction.RejectAll, []);

        var header = SetCookieHeader(context);

        if (isHttps)
        {
            Assert.Contains("secure", header, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.DoesNotContain("secure", header, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Expiry_tracks_the_configured_cookie_lifetime_rather_than_the_365_day_default()
    {
        // Pins that CookieLifetimeDays is actually read rather than a constant being written.
        (ConsentCookieWriter writer, DefaultHttpContext context) = Build(cookieLifetimeDays: 30);

        writer.Write(context.Response, context.Request, ConsentAction.RejectAll, []);

        var header = SetCookieHeader(context);
        DateTimeOffset? expires = SetCookieHeaderValue.Parse(header).Expires;

        Assert.NotNull(expires);

        // Day count, not an exact timestamp, so test-runner latency cannot make this flaky. 30
        // falls nowhere near the 365-day default, so a writer that ignored CookieLifetimeDays
        // still fails this even with a generous window.
        var daysUntilExpiry = (expires!.Value - DateTimeOffset.UtcNow).TotalDays;
        Assert.InRange(daysUntilExpiry, 29, 31);
    }

    [Fact]
    public void The_cookie_value_is_encoded_exactly_once()
    {
        // Pins the wire format. Response.Cookies.Append is what URL-encodes the value on its way
        // into Set-Cookie; if ConsentCookieCodec.Encode escaped it too, this single decode would
        // still leave an escaped string and JsonDocument.Parse would throw instead of finding "v" —
        // exactly the bug consent.js's single decodeURIComponent would hit.
        (ConsentCookieWriter writer, DefaultHttpContext context) = Build();

        writer.Write(context.Response, context.Request, ConsentAction.RejectAll, []);

        var header = SetCookieHeader(context);
        var rawValue = SetCookieHeaderValue.Parse(header).Value.ToString();
        var decodedOnce = Uri.UnescapeDataString(rawValue);

        using JsonDocument json = JsonDocument.Parse(decodedOnce);
        Assert.Equal(1, json.RootElement.GetProperty("v").GetInt32());
    }

    [Fact]
    public void Unknown_and_necessary_categories_are_discarded_rather_than_trusted()
    {
        // Pins server-side filtering of an untrusted body: necessary is implied and never stored,
        // and an invented category name must not reach the cookie.
        (ConsentCookieWriter writer, DefaultHttpContext context) = Build();

        ConsentDecision decision = writer.Write(
            context.Response,
            context.Request,
            ConsentAction.Custom,
            ["statistics", "telepathy", "necessary"]);

        Assert.Equal(new[] { ConsentCategory.Statistics }, decision.Granted.ToArray());
    }

    [Fact]
    public void A_rejection_grants_nothing_even_if_categories_are_attached()
    {
        // Pins that the action, not the client's category list, decides a reject-all/withdrawal:
        // the server must not honour grants smuggled alongside an explicit refusal.
        (ConsentCookieWriter writer, DefaultHttpContext context) = Build();

        ConsentDecision decision = writer.Write(
            context.Response,
            context.Request,
            ConsentAction.RejectAll,
            ["statistics", "marketing"]);

        Assert.Empty(decision.Granted);
    }

    [Fact]
    public void The_cookie_records_the_current_policy_version()
    {
        // Pins that the decision carries the version it was made under, which is what makes
        // NeedsRePrompt work after a wording change.
        (ConsentCookieWriter writer, DefaultHttpContext context) = Build(policyVersion: 7);

        ConsentDecision decision = writer.Write(context.Response, context.Request, ConsentAction.RejectAll, []);

        Assert.Equal(7, decision.PolicyVersion);
    }

    [Theory]
    [InlineData("accept-all", ConsentAction.AcceptAll)]
    [InlineData("reject-all", ConsentAction.RejectAll)]
    [InlineData("custom", ConsentAction.Custom)]
    [InlineData("withdrawn", ConsentAction.Withdrawn)]
    internal void TryParseAction_maps_every_wire_action(string wireName, ConsentAction expected)
    {
        // Pins the four action names consent.js posts; renaming a member must not silently change
        // the wire contract.
        Assert.True(ConsentCookieWriter.TryParseAction(wireName, out ConsentAction parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Accept-All")]
    [InlineData("definitely-not-an-action")]
    public void TryParseAction_rejects_anything_it_does_not_recognise(string? wireName)
    {
        // Pins that an unrecognised or wrongly cased action is a hard failure (the endpoint turns
        // this into 400) rather than defaulting to AcceptAll.
        Assert.False(ConsentCookieWriter.TryParseAction(wireName, out _));
    }

    [Fact]
    public void A_non_default_cookie_name_is_honoured_end_to_end()
    {
        // Pins that no cookie name is hardcoded: a consumer keeping an existing site's cookie name
        // (NDSTK's "ndstk-consent") must not re-prompt a single visitor.
        IOptions<CookieBannerOptions> options = Options.Create(new CookieBannerOptions
        {
            CookieName = "legacy-consent",
        });
        var writeContext = new DefaultHttpContext();

        new ConsentCookieWriter(options).Write(
            writeContext.Response, writeContext.Request, ConsentAction.Custom, ["statistics"]);

        var header = SetCookieHeader(writeContext);
        Assert.StartsWith("legacy-consent=", header);

        SetCookieHeaderValue setCookie = SetCookieHeaderValue.Parse(header);
        var readContext = new DefaultHttpContext();
        readContext.Request.Headers.Cookie = $"{setCookie.Name}={setCookie.Value}";

        IConsentState state = new ConsentState(
            new HttpContextAccessor { HttpContext = readContext },
            options);

        Assert.False(state.NeedsDecision);
        Assert.True(state.HasGranted(ConsentCategory.Statistics));
    }
}
