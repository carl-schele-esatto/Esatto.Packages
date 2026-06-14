using System.Text;
using Microsoft.AspNetCore.Http;

namespace Esatto.Umbraco.Backoffice.SharedPreviewLink;

/// <summary>
/// Hides Umbraco's front-end preview badge (the floating "Preview mode"
/// toolbar rendered by <c>&lt;umb-website-preview&gt;</c>) for visitors who
/// arrived via a share link. Editors using the backoffice "Save and preview"
/// flow continue to see the badge.
/// </summary>
/// <remarks>
/// <para>
/// Detection: <see cref="PreviewLinkMiddleware"/> sets
/// <see cref="PreviewLinkMiddleware.MarkerCookieName"/> alongside
/// <c>UMB_PREVIEW</c> when activating preview via a share link. This
/// middleware sees that cookie on the follow-up request and injects a
/// <c>&lt;style&gt;</c> tag into the HTML response that hides the badge's
/// host element.
/// </para>
/// <para>
/// Performance: response-body buffering only happens when the marker cookie
/// is present, which is a narrow window (cookie is path-scoped to the
/// canonical page and expires after 5 minutes). Non-HTML responses pass
/// through unmodified.
/// </para>
/// </remarks>
public class PreviewBadgeSuppressionMiddleware
{
    private const string HideStyle =
        "<style data-source=\"backoffice-preview-link\">umb-website-preview{display:none!important}</style>";

    private readonly RequestDelegate _next;

    public PreviewBadgeSuppressionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Cookies.ContainsKey(PreviewLinkMiddleware.MarkerCookieName))
        {
            await _next(context);
            return;
        }

        // Wrap the response body so we can inspect Content-Type after the
        // pipeline has written headers but before bytes are flushed to the
        // client. Buffering is acceptable here because the marker cookie is
        // narrowly scoped (single path, 5-minute MaxAge).
        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);

            buffer.Position = 0;

            if (IsHtmlResponse(context.Response))
            {
                var html = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync();
                var injected = InjectHideStyle(html);
                var bytes = Encoding.UTF8.GetBytes(injected);

                context.Response.Body = originalBody;
                context.Response.ContentLength = bytes.Length;
                await context.Response.Body.WriteAsync(bytes);
            }
            else
            {
                context.Response.Body = originalBody;
                await buffer.CopyToAsync(originalBody);
            }
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static bool IsHtmlResponse(HttpResponse response)
    {
        var contentType = response.ContentType;
        return !string.IsNullOrEmpty(contentType)
            && contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase);
    }

    private static string InjectHideStyle(string html)
    {
        // Prefer injecting right before </body> so the style applies after the
        // badge element exists in the DOM. Fall back to appending if the page
        // doesn't have a </body> close tag (partial/fragment responses).
        var idx = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return idx < 0
            ? html + HideStyle
            : html.Insert(idx, HideStyle);
    }
}
