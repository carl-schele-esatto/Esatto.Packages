using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.AI.Web.Authorization;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;

namespace Esatto.Umbraco.Backoffice.AICosts;

/// <summary>
/// Feeds the AI → Analytics → Costs tab (<c>Client/src/ai-costs-view.element.ts</c>):
/// <c>GET /umbraco/management/api/v1/esatto/ai-costs/report?days=N</c> and
/// <c>GET /umbraco/management/api/v1/esatto/ai-costs/breakdown?dimension=profile|user|provider&amp;days=N</c>.
/// </summary>
/// <remarks>
/// Management API + bearer token, gated by the same policy as Umbraco.AI's own analytics endpoints,
/// so only users with the AI section see cost data.
/// </remarks>
[ApiController]
[VersionedApiBackOfficeRoute("esatto/ai-costs")]
[ApiExplorerSettings(GroupName = "Esatto AI costs")]
[Authorize(Policy = AIAuthorizationPolicies.SectionAccessAI)]
public sealed class AICostsController : ManagementApiControllerBase
{
    private readonly AICostReportService _reportService;
    private readonly AICostBreakdownService _breakdownService;

    public AICostsController(AICostReportService reportService, AICostBreakdownService breakdownService)
    {
        _reportService = reportService;
        _breakdownService = breakdownService;
    }

    [HttpGet("report")]
    [ProducesResponseType(typeof(AICostReport), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReport([FromQuery] int days = 90, CancellationToken cancellationToken = default)
        => Ok(await _reportService.GetReportAsync(days, cancellationToken));

    [HttpGet("breakdown")]
    [ProducesResponseType(typeof(AICostBreakdown), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetBreakdown([FromQuery] string dimension, [FromQuery] int days = 90, CancellationToken cancellationToken = default)
        => Enum.TryParse<AICostDimension>(dimension, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? Ok(await _breakdownService.GetBreakdownAsync(parsed, days, cancellationToken))
            : BadRequest($"Unknown dimension '{dimension}'. Use profile, user or provider.");
}
