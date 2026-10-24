using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.Common.Authorization;

namespace Esatto.Umbraco.Backoffice.NavigationOrder;

/// <summary>
/// The saved backoffice order (<c>Client/src/api.ts</c>):
/// <c>GET /umbraco/management/api/v1/esatto/navigation-order</c> for every backoffice user (the order
/// applies to everyone), and <c>PUT</c> for admins only.
/// </summary>
[ApiController]
[VersionedApiBackOfficeRoute("esatto/navigation-order")]
[ApiExplorerSettings(GroupName = "Esatto navigation order")]
[Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]
public sealed class NavigationOrderController : ManagementApiControllerBase
{
    private readonly INavigationOrderStore _store;
    private readonly IBackOfficeSecurityAccessor _security;

    public NavigationOrderController(INavigationOrderStore store, IBackOfficeSecurityAccessor security)
    {
        _store = store;
        _security = security;
    }

    [HttpGet]
    [ProducesResponseType(typeof(NavigationOrderResponse), StatusCodes.Status200OK)]
    public IActionResult Get() => Ok(NavigationOrderResponse.From(_store.Get()));

    [HttpPut]
    [Authorize(Policy = AuthorizationPolicies.RequireAdminAccess)]
    [ProducesResponseType(typeof(NavigationOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult Save([FromBody] NavigationOrderRequest request)
    {
        NavigationOrderSettings settings = request.ToSettings();
        IReadOnlyList<string> errors = NavigationOrderValidator.Validate(settings);
        if (errors.Count > 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid navigation order",
                Detail = string.Join(" ", errors),
                Status = StatusCodes.Status400BadRequest,
            });
        }

        return Ok(NavigationOrderResponse.From(_store.Save(settings, _security.BackOfficeSecurity?.CurrentUser?.Name)));
    }
}
