using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// The consent endpoint's request handling, extracted from the controller it replaces so it is
/// unit-testable without routing.
/// </summary>
/// <remarks>
/// Registered as a minimal-API <c>MapPost</c> by <c>UseCookieConsent()</c>. Attribute-routed
/// front-end API controllers are not a forward-compatible shape: <c>UmbracoApiController</c> and
/// convention-based front-end API routing were both removed in Umbraco 18.
/// </remarks>
internal sealed class ConsentEndpointHandler(
    ConsentCookieWriter cookieWriter,
    IConsentThrottle throttle,
    IOptions<CookieBannerOptions> options)
{
    public IResult Handle(ConsentRequest request, HttpContext context)
    {
        // Metered before the body is inspected, so cheap rejections cost budget too.
        if (options.Value.ThrottleRequestsPerMinute > 0
            && throttle.TryAcquire(ClientKey(context)) is false)
        {
            return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        if (ConsentCookieWriter.TryParseAction(request.Action, out ConsentAction action) is false)
        {
            return TypedResults.BadRequest("Unknown consent action.");
        }

        ConsentDecision decision = cookieWriter.Write(
            context.Response,
            context.Request,
            action,
            request.Categories);

        return TypedResults.Ok(new ConsentStateResponse(
            decision.PolicyVersion,
            decision.Granted.Select(ConsentCategories.ToWireName).Order(StringComparer.Ordinal).ToArray(),
            decision.ConsentId,
            decision.DecidedAt.ToString("O")));
    }

    /// <summary>
    /// Partition key for the throttle: the remote IP, matching the fixed-window limiter this
    /// replaces. Unknown addresses share one bucket rather than escaping the limit entirely.
    /// </summary>
    private static string ClientKey(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
