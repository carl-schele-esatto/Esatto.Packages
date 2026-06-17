using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Web.Common.Authorization;

namespace Esatto.Umbraco.Backoffice.Redirects;

/// <summary>
/// Management API for the Redirects dashboard. All endpoints require
/// SectionAccessSettings (i.e. the user can see the Settings section).
/// </summary>
[ApiController]
[VersionedApiBackOfficeRoute("backoffice/redirects")]
[ApiExplorerSettings(GroupName = "Backoffice Redirects")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
public sealed class RedirectsController : ManagementApiControllerBase
{
    private readonly IRedirectService _service;

    public RedirectsController(IRedirectService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RedirectDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateRedirectRequest request)
    {
        var error = await _service.TryCreateAsync(request);
        if (error is not null)
            return BadRequest(new ProblemDetails { Title = error, Status = StatusCodes.Status400BadRequest });
        return StatusCode(StatusCodes.Status201Created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRedirectRequest request)
    {
        var error = await _service.TryUpdateAsync(id, request);
        if (error is null) return NoContent();
        if (string.Equals(error, "Redirect not found.", StringComparison.Ordinal)) return NotFound();
        return BadRequest(new ProblemDetails { Title = error, Status = StatusCodes.Status400BadRequest });
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
        => await _service.DeleteAsync(id) ? NoContent() : NotFound();
}
