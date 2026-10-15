using Esatto.Umbraco.Backoffice.BlockPreview.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core.Security.Authorization;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Extensions;

namespace Esatto.Umbraco.Backoffice.BlockPreview;

/// <summary>Renders one block's real frontend markup for the backoffice preview iframe.</summary>
/// <remarks>
/// The partial renders draft data (preview: true), so access mirrors Umbraco's own document endpoints:
/// tree access to documents, plus read permission on the specific document.
/// </remarks>
[ApiController]
[VersionedApiBackOfficeRoute("esatto/block-preview")]
[ApiExplorerSettings(GroupName = "Esatto Block Preview")]
[Authorize(Policy = AuthorizationPolicies.TreeAccessDocuments)]
public sealed class BlockPreviewController : ManagementApiControllerBase
{
    private const string DocumentReadPermission = "Umb.Document.Read";

    private readonly BlockPreviewRenderer _renderer;
    private readonly IAuthorizationService _authorizationService;

    public BlockPreviewController(BlockPreviewRenderer renderer, IAuthorizationService authorizationService)
    {
        _renderer = renderer;
        _authorizationService = authorizationService;
    }

    /// <summary>Always 200 for a handled outcome: <c>rendered</c>, <c>notPreviewable</c> or <c>error</c>; 403 without read access.</summary>
    [HttpPost("render")]
    [ProducesResponseType<RenderOutcome>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Render([FromBody] RenderRequest request)
    {
        AuthorizationResult access = await _authorizationService.AuthorizeResourceAsync(
            User,
            ContentPermissionResource.WithKeys(DocumentReadPermission, request.DocumentKey),
            AuthorizationPolicies.ContentPermissionByResource);
        if (!access.Succeeded)
        {
            return Forbidden();
        }

        return Ok(await _renderer.RenderAsync(request, ControllerContext));
    }
}
