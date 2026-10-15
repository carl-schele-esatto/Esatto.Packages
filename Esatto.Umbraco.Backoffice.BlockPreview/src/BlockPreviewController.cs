using Esatto.Umbraco.Backoffice.BlockPreview.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Web.Common.Authorization;

namespace Esatto.Umbraco.Backoffice.BlockPreview;

/// <summary>Renders one block's real frontend markup for the backoffice preview iframe.</summary>
[ApiController]
[VersionedApiBackOfficeRoute("esatto/block-preview")]
[ApiExplorerSettings(GroupName = "Esatto Block Preview")]
[Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]
public sealed class BlockPreviewController : ManagementApiControllerBase
{
    private readonly BlockPreviewRenderer _renderer;

    public BlockPreviewController(BlockPreviewRenderer renderer) => _renderer = renderer;

    /// <summary>Always 200 for a handled outcome: <c>rendered</c>, <c>notPreviewable</c> or <c>error</c>.</summary>
    [HttpPost("render")]
    [ProducesResponseType<RenderOutcome>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Render([FromBody] RenderRequest request)
        => Ok(await _renderer.RenderAsync(request, ControllerContext));
}
