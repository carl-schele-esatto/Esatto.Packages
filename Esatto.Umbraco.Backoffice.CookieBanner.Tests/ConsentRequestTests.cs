using System.Text.Json;
using Esatto.Umbraco.Backoffice.CookieBanner;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentRequestTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Binds_the_body_consent_js_posts()
    {
        // Pins the request wire contract: camelCase names, bound through the record's positional
        // constructor the same way the minimal-API endpoint binds it.
        ConsentRequest? request = JsonSerializer.Deserialize<ConsentRequest>(
            """{"categories":["statistics","marketing"],"action":"accept-all"}""",
            WebOptions);

        Assert.NotNull(request);
        Assert.Equal(new[] { "statistics", "marketing" }, request!.Categories);
        Assert.Equal("accept-all", request.Action);
    }

    [Fact]
    public void A_body_without_categories_leaves_them_null_rather_than_failing()
    {
        // Pins that the writer's `categories ?? []` guard has a reachable null to guard against.
        ConsentRequest? request = JsonSerializer.Deserialize<ConsentRequest>(
            """{"action":"reject-all"}""",
            WebOptions);

        Assert.NotNull(request);
        Assert.Null(request!.Categories);
    }

    [Fact]
    public void Carries_no_culture_field()
    {
        // Pins the dropped consent-log scaffolding: NDSTK's ConsentRequest.Culture was written by
        // consent.js and never read, and must not ship as an unkept promise.
        Assert.Null(typeof(ConsentRequest).GetProperty("Culture"));
    }
}
