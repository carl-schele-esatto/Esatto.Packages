using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class CookieBannerApplicationBuilderExtensionsTests
{
    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext
        {
            // ReadFromJsonAsync resolves IOptions<JsonOptions> off RequestServices; a bare
            // DefaultHttpContext leaves this null, which throws a NullReferenceException before
            // the code under test even runs. An empty provider resolves it to "none configured".
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };

        return context;
    }

    private static HttpRequest RequestWithBody(string? contentType, string body)
    {
        DefaultHttpContext context = NewContext();
        var bytes = Encoding.UTF8.GetBytes(body);
        context.Request.ContentType = contentType;
        context.Request.ContentLength = bytes.Length;
        context.Request.Body = new MemoryStream(bytes);
        return context.Request;
    }

    // Pins the I5 fix: before it, a non-JSON Content-Type made ReadFromJsonAsync throw
    // InvalidOperationException, which nothing caught, so this was an unauthenticated, unthrottled
    // 500 that could be repeated without limit.
    [Fact]
    public async Task A_non_json_content_type_is_rejected_with_415_rather_than_throwing()
    {
        HttpRequest request = RequestWithBody("text/plain", "irrelevant");

        CookieBannerApplicationBuilderExtensions.ConsentRequestOrResult result =
            await CookieBannerApplicationBuilderExtensions.ReadConsentRequestAsync(request);

        Assert.Null(result.Request);
        Assert.Equal(
            StatusCodes.Status415UnsupportedMediaType,
            Assert.IsType<StatusCodeHttpResult>(result.Error).StatusCode);
    }

    [Fact]
    public async Task A_missing_content_type_is_rejected_with_415_rather_than_throwing()
    {
        HttpRequest request = RequestWithBody(null, "{}");

        CookieBannerApplicationBuilderExtensions.ConsentRequestOrResult result =
            await CookieBannerApplicationBuilderExtensions.ReadConsentRequestAsync(request);

        Assert.Null(result.Request);
        Assert.Equal(
            StatusCodes.Status415UnsupportedMediaType,
            Assert.IsType<StatusCodeHttpResult>(result.Error).StatusCode);
    }

    [Fact]
    public async Task Malformed_json_with_a_json_content_type_is_a_400_not_a_500()
    {
        HttpRequest request = RequestWithBody("application/json", "{ not valid json");

        CookieBannerApplicationBuilderExtensions.ConsentRequestOrResult result =
            await CookieBannerApplicationBuilderExtensions.ReadConsentRequestAsync(request);

        Assert.Null(result.Request);
        Assert.IsType<BadRequest<string>>(result.Error);
    }

    [Fact]
    public async Task An_empty_body_with_a_json_content_type_is_a_400_missing_request()
    {
        HttpRequest request = RequestWithBody("application/json", string.Empty);

        CookieBannerApplicationBuilderExtensions.ConsentRequestOrResult result =
            await CookieBannerApplicationBuilderExtensions.ReadConsentRequestAsync(request);

        Assert.Null(result.Request);
        Assert.IsType<BadRequest<string>>(result.Error);
    }

    [Fact]
    public async Task Valid_json_with_a_json_content_type_is_read_with_no_error()
    {
        HttpRequest request = RequestWithBody(
            "application/json", """{"categories":["statistics"],"action":"custom"}""");

        CookieBannerApplicationBuilderExtensions.ConsentRequestOrResult result =
            await CookieBannerApplicationBuilderExtensions.ReadConsentRequestAsync(request);

        Assert.Null(result.Error);
        Assert.Equal("custom", result.Request!.Action);
        Assert.Equal(["statistics"], result.Request.Categories!);
    }
}
