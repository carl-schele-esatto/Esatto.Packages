using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Extensions;

namespace Esatto.Umbraco.Backoffice.SharedPreviewLink;

/// <summary>
/// Mints a shareable preview link for an Umbraco draft (or published) page.
/// </summary>
/// <remarks>
/// Modern Umbraco 17 Management API pattern:
/// <see cref="VersionedApiBackOfficeRouteAttribute"/> +
/// <c>[Authorize(Policy = SectionAccessContent)]</c>. Bearer-token auth is
/// handled by Umbraco's pipeline. Clients call via <c>umbHttpClient</c>
/// (auto-attaches the token).
/// </remarks>
[ApiController]
[VersionedApiBackOfficeRoute("backoffice/preview-link")]
[ApiExplorerSettings(GroupName = "Backoffice Preview Link")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
public sealed class PreviewLinkController : ManagementApiControllerBase
{
    /// <summary>
    /// DataProtection purpose for the share-token. Used by both
    /// <see cref="PreviewLinkController"/> (mint) and
    /// <see cref="PreviewLinkMiddleware"/> (verify) — must match.
    /// </summary>
    public const string ProtectorPurpose = "Esatto.Umbraco.Backoffice.SharedPreviewLink";

    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(7);

    private readonly ITimeLimitedDataProtector _protector;
    private readonly IUmbracoContextAccessor _umbracoContextAccessor;
    private readonly IPublishedUrlProvider _urlProvider;

    public PreviewLinkController(
        IDataProtectionProvider dataProtectionProvider,
        IUmbracoContextAccessor umbracoContextAccessor,
        IPublishedUrlProvider urlProvider)
    {
        _protector = dataProtectionProvider
            .CreateProtector(ProtectorPurpose)
            .ToTimeLimitedDataProtector();
        _umbracoContextAccessor = umbracoContextAccessor;
        _urlProvider = urlProvider;
    }

    [HttpPost]
    [ProducesResponseType(typeof(PreviewLinkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult Mint([FromBody] PreviewLinkRequest request)
    {
        if (request == null || request.ContentKey == Guid.Empty)
        {
            return BadRequest(new { error = "contentKey is required" });
        }

        if (!_umbracoContextAccessor.TryGetUmbracoContext(out var umbCtx))
        {
            return StatusCode(500, new { error = "Umbraco context unavailable" });
        }

        // preview: true so DRAFT-only content is resolvable. Without this,
        // Content.GetById(key) only checks the published cache and returns null
        // for never-published or unpublished-edits documents — which is exactly
        // the content this endpoint exists to share previews of.
        var content = umbCtx.Content?.GetById(preview: true, request.ContentKey);
        if (content == null)
        {
            return NotFound(new { error = "Content not found" });
        }

        // Prefer the culture the editor is previewing (sent by the workspace action from
        // the backoffice's active language). Without it, GetCultureFromDomains() returns the
        // DEFAULT culture for every request, so variant/multilingual pages get the wrong
        // culture's URL (e.g. a Swedish page minted as "/en/..."). Fall back to the
        // domain-derived culture for invariant content or clients that don't send it.
        var cultureCode = !string.IsNullOrWhiteSpace(request.Culture)
            ? request.Culture
            : content.GetCultureFromDomains();
        var absoluteUrl = _urlProvider.GetUrl(content, UrlMode.Absolute, culture: cultureCode);
        if (string.IsNullOrWhiteSpace(absoluteUrl) || absoluteUrl == "#")
        {
            // The published URL provider's route table is published-only —
            // drafts return "#". Construct the URL manually: walk up the tree
            // collecting URL segments, then prepend the root's URL (root is
            // always published, gives us the right hostname + culture-path
            // prefix). UrlSegment is set when content is saved, so it's
            // available even on never-published documents.
            absoluteUrl = BuildDraftUrl(content, cultureCode);
            if (string.IsNullOrWhiteSpace(absoluteUrl))
            {
                return BadRequest(new { error = "Content has no resolvable URL" });
            }
        }

        // Embed the EXACT minted path alongside the content key. Minting runs in the
        // full Umbraco request context where the URL is resolved correctly (including the
        // culture/domain prefix). The verifying middleware runs BEFORE UseUmbraco(), where
        // URL reconstruction is unreliable — so it compares the request path against this
        // authoritative path exactly, which binds a token to one page across every routing
        // config (culture-in-path, domains, home/root) without guesswork.
        var mintedPath = NormalizePath(new Uri(absoluteUrl).AbsolutePath);
        var token = _protector.Protect(
            request.ContentKey.ToString("N") + "|" + mintedPath,
            TokenLifetime);

        var separator = absoluteUrl.Contains('?') ? "&" : "?";
        var fullUrl = $"{absoluteUrl}{separator}preview-token={token}";

        return Ok(new PreviewLinkResponse
        {
            Url = fullUrl,
            ExpiresAt = DateTime.UtcNow.Add(TokenLifetime),
        });
    }

    /// <summary>
    /// Normalizes a URL path for stable comparison between mint and verify: decodes
    /// percent-escapes, trims surrounding slashes, lower-cases, and guarantees a single
    /// leading slash. The site root and the home page both normalize to "/".
    /// </summary>
    internal static string NormalizePath(string? path)
    {
        var trimmed = Uri.UnescapeDataString(path ?? "/").Trim('/');
        return trimmed.Length == 0 ? "/" : "/" + trimmed.ToLowerInvariant();
    }

    private string? BuildDraftUrl(IPublishedContent content, string? cultureCode)
    {
        var segments = new List<string>();
        var cur = content;
        while (cur != null && cur.Level > 1)
        {
            var seg = cur.UrlSegment(cultureCode);
            if (!string.IsNullOrEmpty(seg))
            {
                segments.Insert(0, seg);
            }
            cur = cur.Parent<IPublishedContent>();
        }

        var root = content.AncestorOrSelf(1);
        if (root == null) return null;
        var baseUrl = _urlProvider.GetUrl(root, UrlMode.Absolute, culture: cultureCode);
        if (string.IsNullOrWhiteSpace(baseUrl) || baseUrl == "#") return null;

        var sb = new System.Text.StringBuilder(baseUrl.TrimEnd('/'));
        foreach (var seg in segments)
        {
            sb.Append('/').Append(seg);
        }
        return sb.ToString();
    }
}
