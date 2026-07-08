using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Web.Common.Authorization;

namespace Esatto.Umbraco.Backoffice.CrossContent;

[ApiController]
[VersionedApiBackOfficeRoute("crosscontent/cases")]
[ApiExplorerSettings(GroupName = "CrossContent Cases")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
public sealed class CrossContentCasePickerController(ICrossContentCaseListClient caseList) : ManagementApiControllerBase
{
    [HttpGet("list")]
    [ProducesResponseType(typeof(IReadOnlyList<CrossContentCaseListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await caseList.ListCasesAsync(ct));
}
