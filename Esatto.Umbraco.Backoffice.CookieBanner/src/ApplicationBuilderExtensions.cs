using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

public static class CookieBannerApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the cookie-consent middleware and maps the consent endpoint at
    /// <see cref="CookieBannerOptions.EndpointPath"/>. Call after <c>BootUmbracoAsync()</c> and
    /// before <c>UseUmbraco()</c>.
    /// </summary>
    /// <remarks>
    /// This is the whole integration surface. The endpoint is a minimal-API <c>MapPost</c> rather
    /// than an attribute-routed controller, so no <c>MapControllers()</c> is required and nothing
    /// depends on front-end API routing (removed in Umbraco 18). Throttling is package-owned, so
    /// there is no <c>AddRateLimiter</c> and no <c>UseRateLimiter()</c> to wedge between
    /// <c>WithMiddleware(...)</c> and <c>WithEndpoints(...)</c>.
    /// The body is read explicitly instead of being model-bound, which keeps the internal
    /// <see cref="ConsentRequest"/> and <see cref="ConsentEndpointHandler"/> out of minimal-API
    /// parameter inference altogether.
    /// </remarks>
    public static IApplicationBuilder UseCookieConsent(this IApplicationBuilder app)
    {
        app.UseMiddleware<VaryByConsentCookieMiddleware>();

        if (app is not IEndpointRouteBuilder endpoints)
        {
            throw new InvalidOperationException(
                "UseCookieConsent() must be called on a WebApplication (or another "
                + "IApplicationBuilder that is also an IEndpointRouteBuilder) so the consent "
                + "endpoint can be mapped.");
        }

        var endpointPath = app.ApplicationServices
            .GetRequiredService<IOptions<CookieBannerOptions>>()
            .Value.EndpointPath;

        endpoints.MapPost(endpointPath, async (HttpContext context) =>
        {
            ConsentRequest? request;
            try
            {
                request = await context.Request.ReadFromJsonAsync<ConsentRequest>();
            }
            catch (JsonException)
            {
                return Results.BadRequest("Malformed consent request.");
            }

            if (request is null)
            {
                return Results.BadRequest("Missing consent request.");
            }

            ConsentEndpointHandler handler = context.RequestServices
                .GetRequiredService<ConsentEndpointHandler>();

            return handler.Handle(request, context);
        })
        // A visitor-facing endpoint: it must answer before anyone is authenticated, whatever
        // fallback authorization policy the host has configured.
        .AllowAnonymous();

        return app;
    }
}
