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

namespace Esatto.Umbraco.Backoffice.SharedPreviewLink;

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
        string? mintedPath = null;
        try
        {
            var unprotected = _protector.Unprotect(token);
            // Payload is "<contentKey:N>" (legacy) or "<contentKey:N>|<minted-path>"
            // (1.0.4+). The embedded path is the authoritative URL the mint side computed
            // in the full Umbraco context.
            var sep = unprotected.IndexOf('|');
            if (sep >= 0)
            {
                contentKey = Guid.ParseExact(unprotected.Substring(0, sep), "N");
                mintedPath = unprotected.Substring(sep + 1);
            }
            else
            {
                contentKey = Guid.ParseExact(unprotected, "N");
            }
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            // The token could not be decrypted/validated. This is the genuine-expiry case
            // (link older than its 7-day lifetime) — but it ALSO fires when the host's
            // DataProtection key ring differs from the one that minted the token. That
            // happens when the consuming app does NOT persist its DataProtection keys:
            // every link minted before an app restart then fails here ("The payload was
            // invalid"). Logged rather than silently swallowed so it isn't misdiagnosed as
            // simple expiry — see README → Requirements for the key-persistence prerequisite.
            _logger.LogWarning(ex,
                "[PreviewLink] Token failed to decrypt/validate: {Reason}. If the link is not "
                + "genuinely older than 7 days, the host's DataProtection keys are most likely "
                + "not persisted across app restarts.",
                ex.Message);
            await RenderErrorAsync(context, "expired", StatusCodes.Status410Gone);
            return;
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex,
                "[PreviewLink] Decrypted token was not a valid content key.");
            await RenderErrorAsync(context, "invalid", StatusCodes.Status400BadRequest);
            return;
        }

        // Resolve the content. Middleware runs before UseUmbraco(), so create an explicit
        // UmbracoContext via the factory; dispose before handing off so Umbraco's pipeline
        // creates the request-scoped context that renders the page. preview: true so
        // DRAFT-only content is resolvable — the whole point of a preview link. For legacy
        // tokens (no embedded path) also build the content-relative path for the fallback.
        string? relativePath = null;
        using (var ctxRef = umbracoContextFactory.EnsureUmbracoContext())
        {
            var content = ctxRef.UmbracoContext.Content?.GetById(preview: true, contentKey);
            if (content == null)
            {
                _logger.LogWarning(
                    "[PreviewLink] Content not found for key {ContentKey} (preview: true)",
                    contentKey);
                await RenderErrorAsync(context, "notfound", StatusCodes.Status404NotFound);
                return;
            }

            if (mintedPath is null)
            {
                // The content's path RELATIVE to its culture/domain root: the segments
                // BELOW the root node, "" for the home/root page. Excludes any culture/
                // domain prefix (e.g. "/en"), which is reconciled in the fallback matcher.
                var cultureCode = content.GetCultureFromDomains();
                relativePath = BuildContentRelativePath(
                    ctxRef.UmbracoContext.Content!, navigation, contentKey, cultureCode);
            }
        }

        // Original-case request path for the cookie + redirect.
        var rawRequestPath = context.Request.Path.Value ?? "/";

        // Verify the request URL belongs to the token's content so a token can't be reused
        // to preview a different page.
        bool pathOk;
        if (mintedPath is not null)
        {
            // 1.0.4+ token: exact match against the authoritative path the mint side
            // recorded — tight across every routing config (culture-in-path, domains, root).
            pathOk = string.Equals(
                PreviewLinkController.NormalizePath(rawRequestPath),
                mintedPath,
                StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            // Legacy token (pre-1.0.4, no embedded path): the request may carry a leading
            // culture/domain prefix (e.g. "/en") the content-relative path omits, so match
            // the relative segments against the TAIL of the request (≤1 prefix segment).
            var requestPath = rawRequestPath.TrimEnd('/').ToLowerInvariant();
            pathOk = RequestPathMatchesContent(requestPath, relativePath!);
        }

        if (!pathOk)
        {
            _logger.LogWarning(
                "[PreviewLink] Path mismatch for key {ContentKey}: expected='{Expected}' request='{Request}'",
                contentKey, mintedPath ?? relativePath, rawRequestPath);
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
            // Cookie Path scoped to the request path (the URL the recipient is on,
            // and the target of the 302 below), trimmed of any trailing slash. This
            // is correct regardless of culture/domain prefixing; the previously-used
            // reconstructed path dropped the culture prefix and broke prefixed sites.
            // RFC 6265 path matching: Path "/foo" is sent on "/foo" and "/foo/sub";
            // a trailing slash ("/foo/") would NOT be sent on "/foo".
            var cookiePath = rawRequestPath.TrimEnd('/');
            if (cookiePath.Length == 0) cookiePath = "/";
            var cookieOptions = new CookieOptions
            {
                Path = cookiePath,
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

    // Build the content's path RELATIVE to its culture/domain root for any document
    // (published or draft) by walking the ancestor key chain via the navigation
    // service and looking each one up in the draft-aware content cache. Avoids the
    // unreliable IPublishedContent.Parent traversal which fails on draft-loaded
    // content in the ad-hoc UmbracoContext.
    //
    // Returns the segments BELOW the root node: "" for the home/root page itself,
    // "/about" for a level-2 page, "/about/team" for level-3, etc. The root's own
    // segment is never part of the URL path (the root maps to the culture/domain
    // root), so it is dropped.
    private static string BuildContentRelativePath(
        IPublishedContentCache contentCache,
        IDocumentNavigationQueryService navigation,
        Guid contentKey,
        string? cultureCode)
    {
        // Full chain root → … → self, then drop the FIRST element (the root). Done
        // AFTER appending self so the home/root page (no ancestors) correctly yields
        // an empty chain rather than its own segment.
        IEnumerable<Guid> pathKeys;
        if (navigation.TryGetAncestorsKeys(contentKey, out var ancestorKeys))
        {
            // ancestorKeys: immediate parent first, root last → Reverse() = root first.
            pathKeys = ancestorKeys.Reverse().Concat(new[] { contentKey }).Skip(1);
        }
        else
        {
            // No ancestors ⇒ this IS the root/home page ⇒ empty relative path.
            pathKeys = Array.Empty<Guid>();
        }

        var segments = new List<string>();
        foreach (var key in pathKeys)
        {
            var node = contentCache.GetById(preview: true, key);
            if (node == null) continue;
            var seg = node.UrlSegment(cultureCode);
            if (!string.IsNullOrEmpty(seg))
            {
                segments.Add(seg);
            }
        }

        return segments.Count == 0 ? string.Empty : "/" + string.Join("/", segments);
    }

    // True when the request path corresponds to the token's content. The content-
    // relative segments must match the TAIL of the request path; any leading
    // remainder is the culture/domain path prefix (e.g. "/en") and is allowed up to
    // one segment. Both inputs are expected lowercased and trailing-slash-trimmed.
    //
    // Examples (relativePath ⇒ accepted request paths): "/about" ⇒ "/about",
    // "/en/about"; "" (home) ⇒ "/", "/en". Rejected: "/about" ⇒ "/en/contact"
    // (tail mismatch) or "/a/b/about" (prefix longer than one segment).
    private static bool RequestPathMatchesContent(string requestPath, string relativePath)
    {
        var reqSegs = requestPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var relSegs = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (reqSegs.Length < relSegs.Length) return false;

        // The relative segments must be the suffix of the request segments.
        var offset = reqSegs.Length - relSegs.Length;
        for (var i = 0; i < relSegs.Length; i++)
        {
            if (!string.Equals(reqSegs[offset + i], relSegs[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Allow at most one leading prefix segment (a culture/domain path like "/en").
        // 0 = no prefix (invariant or hostname-routed); 1 = culture-in-path.
        return offset <= 1;
    }
}
