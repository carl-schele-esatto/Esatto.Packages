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
            ConsentRequestOrResult read = await ReadConsentRequestAsync(context.Request);
            if (read.Error is not null)
            {
                return read.Error;
            }

            ConsentEndpointHandler handler = context.RequestServices
                .GetRequiredService<ConsentEndpointHandler>();

            return handler.Handle(read.Request!, context);
        })
        // A visitor-facing endpoint: it must answer before anyone is authenticated, whatever
        // fallback authorization policy the host has configured.
        .AllowAnonymous();

        return app;
    }

    /// <summary>
    /// Reads a <see cref="ConsentRequest"/> from the body, or the <see cref="IResult"/> the endpoint
    /// should return instead of doing so. Kept separate from the <c>MapPost</c> lambda above, and
    /// internal rather than a local function, purely so a test can drive it with a bare
    /// <see cref="DefaultHttpContext"/> instead of a live server.
    /// </summary>
    internal static async Task<ConsentRequestOrResult> ReadConsentRequestAsync(HttpRequest httpRequest)
    {
        // ReadFromJsonAsync throws InvalidOperationException - not JsonException - when the
        // Content-Type header is missing or not JSON, so a request with e.g. text/plain used to
        // fall through both catches below and surface as an unhandled 500. That happened before
        // ConsentEndpointHandler (and its throttle) ever ran, so it was unauthenticated, free of
        // any rate limit, and repeatable without limit. Checked explicitly here, and also caught
        // below as defence in depth.
        if (httpRequest.HasJsonContentType() is false)
        {
            return new ConsentRequestOrResult(null, Results.StatusCode(StatusCodes.Status415UnsupportedMediaType));
        }

        ConsentRequest? request;
        try
        {
            request = await httpRequest.ReadFromJsonAsync<ConsentRequest>();
        }
        catch (JsonException)
        {
            return new ConsentRequestOrResult(null, Results.BadRequest("Malformed consent request."));
        }
        catch (InvalidOperationException)
        {
            return new ConsentRequestOrResult(null, Results.StatusCode(StatusCodes.Status415UnsupportedMediaType));
        }

        return request is null
            ? new ConsentRequestOrResult(null, Results.BadRequest("Missing consent request."))
            : new ConsentRequestOrResult(request, null);
    }

    /// <summary>Either a successfully-read <see cref="ConsentRequest"/>, or the error to return.</summary>
    internal readonly record struct ConsentRequestOrResult(ConsentRequest? Request, IResult? Error);
}
