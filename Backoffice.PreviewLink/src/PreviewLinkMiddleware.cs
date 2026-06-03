using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Preview;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Backoffice.PreviewLink;

public static class PreviewLinkMiddlewareExtension
{
    /// <summary>
    /// Adds the preview-link middlewares to the request pipeline. Call BEFORE
    /// <c>app.UseUmbraco()</c> so the <c>UMB_PREVIEW</c> cookie set by the
    /// validation middleware affects Umbraco's content resolution, and so the
    /// suppression middleware can wrap Umbraco's HTML response.
    /// </summary>
    /// <remarks>
    /// Registration order matters: <see cref="PreviewBadgeSuppressionMiddleware"/>
    /// is registered first so it is OUTERMOST in the pipeline — it wraps the
    /// response body, then delegates inward to
    /// <see cref="PreviewLinkMiddleware"/> (which handles share-link
    /// validation + cookie setup) and the rest of the pipeline. On the way
    /// out, the suppression middleware sees Umbraco's rendered HTML last and
    /// injects the hide-style before flushing to the client.
    /// </remarks>
    public static IApplicationBuilder UsePreviewLink(this IApplicationBuilder builder)
    {
        builder.UseMiddleware<PreviewBadgeSuppressionMiddleware>();
        builder.UseMiddleware<PreviewLinkMiddleware>();
        return builder;
    }
}

/// <summary>
/// Validates shareable preview links minted by <see cref="PreviewLinkController"/>.
/// Runs BEFORE Umbraco's pipeline so the <c>UMB_PREVIEW</c> cookie set here
/// takes effect on the follow-up request's content resolution.
/// </summary>
/// <remarks>
/// Flow when <c>?preview-token=…</c> is present:
/// <list type="number">
///   <item><see cref="ITimeLimitedDataProtector.Unprotect(string)"/> — catches
///   tampered, malformed, and expired tokens (all surface as
///   <see cref="System.Security.Cryptography.CryptographicException"/>).</item>
///   <item>Look up content by the unprotected GUID.</item>
///   <item>Compute canonical URL path for the content; if it doesn't match the
///   request path → 404 (token is hard-bound to one page).</item>
///   <item>Mint an Umbraco preview token via <see cref="IPreviewTokenGenerator"/>
///   using the super-user key as the identity (preview infrastructure needs SOME
///   user identity).</item>
///   <item>Set the <c>UMB_PREVIEW</c> cookie with <c>Path</c> scoped to the
///   canonical page path + a 5-minute <c>MaxAge</c>. Cookie expires fast and
///   doesn't send on unrelated URLs.</item>
///   <item>302 redirect to the same URL minus the <c>preview-token</c> param so
///   the browser sends the now-present cookie on the follow-up request.</item>
///   <item>Umbraco renders the page in preview mode.</item>
/// </list>
/// </remarks>
public class PreviewLinkMiddleware
{
    public const string QueryParam = "preview-token";
    public const string ErrorItemsKey = "backoffice-preview-link-error";

    /// <summary>
    /// Marker cookie set alongside <c>UMB_PREVIEW</c> when activating preview
    /// via a share link. <see cref="PreviewBadgeSuppressionMiddleware"/> reads
    /// this cookie to decide whether to hide the Umbraco preview badge —
    /// recipients of share links shouldn't see the "End preview" toolbar
    /// (no backoffice access to return to); editors using "Save and preview"
    /// still see it because they never receive this cookie.
    /// </summary>
    public const string MarkerCookieName = "BackofficePreviewLinkShare";

    private readonly RequestDelegate _next;
    private readonly ITimeLimitedDataProtector _protector;
    private readonly ILogger<PreviewLinkMiddleware> _logger;

    public PreviewLinkMiddleware(
        RequestDelegate next,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<PreviewLinkMiddleware> logger)
    {
        _next = next;
        _protector = dataProtectionProvider
            .CreateProtector(PreviewLinkController.ProtectorPurpose)
            .ToTimeLimitedDataProtector();
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IUmbracoContextFactory umbracoContextFactory,
        IPublishedUrlProvider urlProvider,
        IPreviewTokenGenerator previewTokenGenerator,
        IDocumentNavigationQueryService navigation)
    {
        if (!context.Request.Query.TryGetValue(QueryParam, out var tokenValues)
            || string.IsNullOrWhiteSpace(tokenValues.ToString()))
        {
            await _next(context);
            return;
        }

        var token = tokenValues.ToString();

        Guid contentKey;
        try
        {
            var unprotected = _protector.Unprotect(token);
            contentKey = Guid.ParseExact(unprotected, "N");
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            await RenderErrorAsync(context, "expired", StatusCodes.Status410Gone);
            return;
        }
        catch (FormatException)
        {
            await RenderErrorAsync(context, "invalid", StatusCodes.Status400BadRequest);
            return;
        }

        // Middleware runs before UseUmbraco(), so IUmbracoContextAccessor has
        // no ambient context yet. Create one explicitly via the factory so we
        // can resolve content + canonical URL. Dispose before handing off —
        // Umbraco's own pipeline will create the request-scoped context that
        // actually renders the page.
        string? canonicalPath;
        using (var ctxRef = umbracoContextFactory.EnsureUmbracoContext())
        {
            // preview: true so DRAFT-only content is resolvable here — the
            // whole point of a preview link is to render unpublished drafts.
            var content = ctxRef.UmbracoContext.Content?.GetById(preview: true, contentKey);
            if (content == null)
            {
                _logger.LogWarning(
                    "[PreviewLink] Content not found for key {ContentKey} (preview: true)",
                    contentKey);
                await RenderErrorAsync(context, "notfound", StatusCodes.Status404NotFound);
                return;
            }

            // Canonical URL path. Compare against the request path to ensure
            // the token isn't being reused across pages. URL provider's route
            // table is published-only — for drafts it returns "#" OR (in the
            // middleware's ad-hoc UmbracoContext) just the leaf segment without
            // the parent chain. Build the path from the document navigation
            // service which works on content keys directly and is independent
            // of cache/preview state.
            var cultureCode = content.GetCultureFromDomains();
            canonicalPath = BuildDraftRelativeUrl(
                ctxRef.UmbracoContext.Content!, navigation, contentKey, cultureCode)
                ?.TrimEnd('/').ToLowerInvariant();
        }

        var requestPath = context.Request.Path.Value?.TrimEnd('/').ToLowerInvariant();

        if (string.IsNullOrEmpty(canonicalPath) || canonicalPath != requestPath)
        {
            _logger.LogWarning(
                "[PreviewLink] Path mismatch for key {ContentKey}: canonical='{Canonical}' request='{Request}'",
                contentKey, canonicalPath, requestPath);
            await RenderErrorAsync(context, "notfound", StatusCodes.Status404NotFound);
            return;
        }

        // Generate a preview token bound to the super user (any backoffice
        // user works — Umbraco's preview pipeline just needs a verifiable
        // identity attached to the cookie value).
        var tokenAttempt = await previewTokenGenerator
            .GenerateTokenAsync(Constants.Security.SuperUserKey);
        if (tokenAttempt.Success && !string.IsNullOrEmpty(tokenAttempt.Result))
        {
            // Cookie Path scoped to the canonical path WITHOUT a trailing slash.
            // RFC 6265 path matching: a cookie with Path "/foo/" is NOT sent on
            // a request to "/foo" — the cookie-path can't be longer than the
            // request-path. Path "/foo" matches both "/foo" and "/foo/sub".
            var cookieOptions = new CookieOptions
            {
                Path = canonicalPath,
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromMinutes(5),
            };
            context.Response.Cookies.Append(
                Constants.Web.PreviewCookieName,
                tokenAttempt.Result,
                cookieOptions);

            // Marker cookie: hides the Umbraco preview badge for share-link
            // recipients. PreviewBadgeSuppressionMiddleware reads this on the
            // follow-up request. Mirrors the same Path + MaxAge as the preview
            // cookie so it expires in lockstep.
            context.Response.Cookies.Append(
                MarkerCookieName,
                "1",
                cookieOptions);
        }
        else
        {
            await RenderErrorAsync(context, "invalid", StatusCodes.Status500InternalServerError);
            return;
        }

        // Cookies set via Response.Cookies.Append only take effect on the NEXT
        // request — context.Request.Cookies is parsed once at request start and
        // never updated. So we can't just call _next(context) here: Umbraco's
        // pipeline would run without seeing the UMB_PREVIEW cookie, fall back
        // to published mode, and 404 on draft-only pages.
        //
        // Mirror Umbraco's own backoffice "Preview" flow: set the cookie, then
        // 302 the browser back to the same URL minus the preview-token. The
        // browser follows the redirect with the now-present cookie, Umbraco
        // enters preview mode, and the draft renders.
        var query = QueryHelpers.ParseQuery(context.Request.QueryString.ToString());
        query.Remove(QueryParam);
        var redirectQuery = QueryString.Create(
            query.Select(kv => new KeyValuePair<string, StringValues>(kv.Key, kv.Value)));
        var redirectUrl = context.Request.Path + redirectQuery;
        context.Response.Redirect(redirectUrl);
    }

    private async Task RenderErrorAsync(
        HttpContext context,
        string variant,
        int statusCode)
    {
        context.Items[ErrorItemsKey] = variant;
        context.Response.StatusCode = statusCode;

        // Rewrite to the error endpoint. PreviewLinkErrorController serves the
        // friendly view.
        context.Request.Path = "/preview-link-error";
        context.Request.QueryString = QueryString.Empty;

        await _next(context);
    }

    // Construct the relative URL for any document (published or draft) by
    // walking the ancestor key chain via the navigation service and looking
    // each one up in the draft-aware content cache. Avoids the unreliable
    // IPublishedContent.Parent traversal which fails on draft-loaded content
    // in the ad-hoc UmbracoContext.
    private static string? BuildDraftRelativeUrl(
        IPublishedContentCache contentCache,
        IDocumentNavigationQueryService navigation,
        Guid contentKey,
        string? cultureCode)
    {
        if (!navigation.TryGetAncestorsKeys(contentKey, out var ancestorKeys))
        {
            return null;
        }

        // TryGetAncestorsKeys returns immediate parent first, root last —
        // reverse to walk root → parent → ... → self. Skip the root itself
        // (level 1) because its UrlSegment isn't part of the URL path
        // (domains attach there, not path).
        var keys = ancestorKeys.Reverse().Skip(1).Concat(new[] { contentKey });

        var segments = new List<string>();
        foreach (var key in keys)
        {
            var node = contentCache.GetById(preview: true, key);
            if (node == null) continue;
            var seg = node.UrlSegment(cultureCode);
            if (!string.IsNullOrEmpty(seg))
            {
                segments.Add(seg);
            }
        }

        return segments.Count == 0 ? "/" : "/" + string.Join("/", segments);
    }
}
