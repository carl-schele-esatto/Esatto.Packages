using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Web.Common.Authorization;

namespace Esatto.Umbraco.Backoffice.Build;

/// <summary>
/// Feeds the Content ▸ Build dashboard (<c>Client/src</c>), at <c>/umbraco/management/api/v1/esatto/build-info</c>:
/// which build is deployed on this environment. Anyone with the Content section can read it.
/// </summary>
[ApiController]
[VersionedApiBackOfficeRoute("esatto/build-info")]
[ApiExplorerSettings(GroupName = "Esatto Build")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
public sealed class BuildInfoController(BuildInfoService buildInfo) : ManagementApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<BuildInfoResponse>(StatusCodes.Status200OK)]
    public IActionResult Get() => Ok(buildInfo.Get());
}
