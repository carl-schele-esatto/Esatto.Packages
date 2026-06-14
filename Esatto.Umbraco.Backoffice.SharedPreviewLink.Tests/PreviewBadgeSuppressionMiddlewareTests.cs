using System.Text;
using Microsoft.AspNetCore.Http;
using Esatto.Umbraco.Backoffice.SharedPreviewLink;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests;

public class PreviewBadgeSuppressionMiddlewareTests
{
    // The exact tag PreviewBadgeSuppressionMiddleware injects — asserting on the
    // full tag (not just the CSS rule) also guards the <style> wrapper and the
    // data-source attribute against accidental change.
    private const string HideMarker =
        "<style data-source=\"backoffice-preview-link\">umb-website-preview{display:none!important}</style>";

    private static async Task<string> RunAsync(string body, string contentType, bool withMarkerCookie)
    {
        var context = new DefaultHttpContext();
        var finalBody = new MemoryStream();
        context.Response.Body = finalBody;
        if (withMarkerCookie)
        {
            context.Request.Headers["Cookie"] = $"{PreviewLinkMiddleware.MarkerCookieName}=1";
        }

        var middleware = new PreviewBadgeSuppressionMiddleware(async ctx =>
        {
            ctx.Response.ContentType = contentType;
            var bytes = Encoding.UTF8.GetBytes(body);
            await ctx.Response.Body.WriteAsync(bytes);
        });

        await middleware.InvokeAsync(context);

        return Encoding.UTF8.GetString(finalBody.ToArray());
    }

    [Fact]
    public async Task Injects_hide_style_before_closing_body_when_marker_present_and_html()
    {
        var html = "<html><head></head><body><p>hi</p></body></html>";
        var result = await RunAsync(html, "text/html; charset=utf-8", withMarkerCookie: true);

        Assert.Contains(HideMarker, result);
        Assert.True(result.IndexOf(HideMarker, StringComparison.Ordinal)
            < result.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Appends_hide_style_when_html_has_no_closing_body()
    {
        var html = "<div>fragment with no body tag</div>";
        var result = await RunAsync(html, "text/html", withMarkerCookie: true);

        Assert.Contains(HideMarker, result);
        Assert.StartsWith(html, result);
    }

    [Fact]
    public async Task Leaves_non_html_response_untouched_even_with_marker()
    {
        var json = "{\"hello\":\"world\"}";
        var result = await RunAsync(json, "application/json", withMarkerCookie: true);

        Assert.Equal(json, result);
        Assert.DoesNotContain(HideMarker, result);
    }

    [Fact]
    public async Task Passes_through_untouched_when_marker_cookie_absent()
    {
        var html = "<html><body>hi</body></html>";
        var result = await RunAsync(html, "text/html", withMarkerCookie: false);

        Assert.Equal(html, result);
        Assert.DoesNotContain(HideMarker, result);
    }
}
