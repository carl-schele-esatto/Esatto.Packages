using Microsoft.AspNetCore.Http;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Marks front-end HTML responses as private and varying by the consent cookie.
/// </summary>
/// <remarks>
/// The consent dialog, and any consent-gated <c>&lt;script&gt;</c> or embed such as the Google
/// tag, are baked into server-rendered markup based on the visitor's consent cookie. The moment
/// any shared cache — a CDN, a reverse proxy, an edge network — handles that markup, one
/// visitor's consent state, including a third-party analytics tag, could be served to another.
/// Scoped to <c>text/html</c> responses outside <c>/umbraco</c>: static assets and API responses
/// never carry <c>text/html</c>, and the backoffice is excluded by path, so neither is affected.
/// Registered by <c>UseCookieConsent()</c>, which the consumer calls before <c>UseUmbraco()</c> so
/// this <c>OnStarting</c> callback is queued before anything downstream starts writing the body.
/// </remarks>
internal sealed class VaryByConsentCookieMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/umbraco") is false)
        {
            context.Response.OnStarting(() =>
            {
                if (context.Response.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) is true)
                {
                    context.Response.Headers.Vary = "Cookie";
                    context.Response.Headers.CacheControl = "private, no-cache";
                }

                return Task.CompletedTask;
            });
        }

        await next(context);
    }
}
