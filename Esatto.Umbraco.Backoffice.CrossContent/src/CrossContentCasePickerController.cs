using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Web.Common.Authorization;

namespace Esatto.Umbraco.Backoffice.CrossContent;

[ApiController]
[VersionedApiBackOfficeRoute("crosscontent/cases")]
[ApiExplorerSettings(GroupName = "CrossContent Cases")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
public sealed class CrossContentCasePickerController(
    ICrossContentCaseListClient caseList,
    IOptions<CrossContentOptions> options) : ManagementApiControllerBase
{
    [HttpGet("list")]
    [ProducesResponseType(typeof(CrossContentCaseListResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await caseList.ListCasesAsync(ct);
        return Ok(CrossContentCaseListResult.Create(options.Value.ContentTypes, items));
    }
}
