using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Rendering;

/// <summary>
/// The preview iframe is sandboxed without <c>allow-same-origin</c>, so its origin is opaque
/// (<c>Origin: null</c>). Fonts and module scripts are always fetched in CORS mode, so even the
/// site's own <c>/fonts/x.woff2</c> or module bundle would be blocked inside the preview without
/// <c>Access-Control-Allow-Origin</c>. This adds that header for exactly those requests: GET/HEAD,
/// <c>Origin: null</c>, font or script file. Pages, APIs and real cross-site origins are untouched,
/// and opaque-origin requests carry no cookies, so nothing user-specific is exposed.
/// </summary>
public static class OpaqueOriginAssets
{
    private static readonly string[] Extensions = [".woff2", ".woff", ".ttf", ".otf", ".eot", ".js", ".mjs"];

    public static bool ShouldAllow(string method, string? origin, string path) =>
        (HttpMethods.IsGet(method) || HttpMethods.IsHead(method))
        && origin == "null"
        && Extensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    public static IApplicationBuilder UseOpaqueOriginAssets(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            HttpRequest request = context.Request;
            if (ShouldAllow(request.Method, request.Headers.Origin, request.Path.Value ?? string.Empty))
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.AccessControlAllowOrigin = "*";
                    return Task.CompletedTask;
                });
            }

            await next(context);
        });
}
