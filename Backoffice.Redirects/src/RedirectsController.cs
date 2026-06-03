using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Web.Common.Authorization;

namespace Backoffice.Redirects;

/// <summary>
/// Management API for the Redirects dashboard. All endpoints require
/// SectionAccessSettings (i.e. the user can see the Settings section).
/// Per-site access is enforced via the <see cref="IRedirectSiteContext"/>
/// plugin — operations on a siteKey the user can't manage return 403.
/// </summary>
[ApiController]
[VersionedApiBackOfficeRoute("backoffice/redirects")]
[ApiExplorerSettings(GroupName = "Backoffice Redirects")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
public sealed class RedirectsController : ManagementApiControllerBase
{
    private readonly IRedirectService _service;
    private readonly IRedirectSiteContext _siteContext;

    public RedirectsController(IRedirectService service, IRedirectSiteContext siteContext)
    {
        _service = service;
        _siteContext = siteContext;
    }

    /// <summary>Returns the sites the current user is allowed to manage.</summary>
    [HttpGet("sites")]
    [ProducesResponseType(typeof(IReadOnlyList<RedirectSite>), StatusCodes.Status200OK)]
    public IActionResult GetSites() => Ok(_siteContext.GetAllowedSites());

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RedirectDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll([FromQuery] string? site)
    {
        if (!IsAllowed(site)) return Forbid();
        return Ok(await _service.GetAllAsync(site ?? string.Empty));
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateRedirectRequest request)
    {
        if (!IsAllowed(request.SiteKey)) return Forbid();
        var error = await _service.TryCreateAsync(request);
        if (error is not null)
            return BadRequest(new ProblemDetails { Title = error, Status = StatusCodes.Status400BadRequest });
        return StatusCode(StatusCodes.Status201Created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRedirectRequest request)
    {
        if (!IsAllowed(request.SiteKey)) return Forbid();

        // Gate on the row's ACTUAL site, not just the client's claim.
        // Without this, a user could reference any row id and the service-layer
        // "Cannot move between sites" rejection would leak existence as a 400.
        var rowSite = await _service.GetSiteKeyAsync(id);
        if (rowSite is null) return NotFound();
        if (!IsAllowed(rowSite)) return Forbid();

        var error = await _service.TryUpdateAsync(id, request);
        if (error is null) return NoContent();
        if (string.Equals(error, "Redirect not found.", StringComparison.Ordinal)) return NotFound();
        return BadRequest(new ProblemDetails { Title = error, Status = StatusCodes.Status400BadRequest });
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int id)
    {
        var rowSite = await _service.GetSiteKeyAsync(id);
        if (rowSite is null) return NotFound();
        if (!IsAllowed(rowSite)) return Forbid();

        return await _service.DeleteAsync(id) ? NoContent() : NotFound();
    }

    /// <summary>
    /// True if the siteKey is one the current user can manage. Empty siteKey
    /// is treated as "single-site mode" and is allowed only when the context
    /// returns an empty allowed-sites list.
    /// </summary>
    private bool IsAllowed(string? siteKey)
    {
        var allowed = _siteContext.GetAllowedSites();
        var key = siteKey?.Trim() ?? string.Empty;

        if (allowed.Count == 0)
        {
            // Single-site mode: only the empty key is acceptable.
            return key.Length == 0;
        }

        return allowed.Any(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));
    }
}
