using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Web.Common.Authorization;

namespace Esatto.Umbraco.Backoffice.DictionaryFilterValues;

/// <summary>
/// Authenticated backoffice endpoint that powers the Dictionary collection filter.
/// Matches the editor's filter text against the dictionary key OR any translation
/// VALUE (the feature), reading from a cached, pre-flattened dictionary so there is
/// no per-request tree walk. The response shape mirrors the built-in dictionary
/// collection endpoint, so our client data source maps it 1:1 and the stock
/// collection UI keeps working unchanged.
/// </summary>
[ApiController]
[VersionedApiBackOfficeRoute("backoffice/dictionary-filter-values")]
[ApiExplorerSettings(GroupName = "Backoffice Dictionary Filter Values")]
[Authorize(Policy = AuthorizationPolicies.TreeAccessDictionary)]
public sealed class DictionaryFilterValuesController : ManagementApiControllerBase
{
    private readonly IDictionaryFilterValuesCache _cache;

    public DictionaryFilterValuesController(IDictionaryFilterValuesCache cache)
        => _cache = cache;

    /// <summary>
    /// Returns dictionary items whose key or any translation value matches
    /// <paramref name="filter"/> (case-insensitive). An empty filter returns all.
    /// Shape: <c>{ items: [{ id, name, parent: { id } | null, translatedIsoCodes }], total }</c>.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? filter,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100)
    {
        var items = await _cache.GetAllAsync();
        var (page, total) = DictionaryFilterValuesFilter.Apply(items, filter, skip, take);

        var result = page.Select(i => new
        {
            id = i.Id,
            name = i.Name,
            parent = i.ParentId.HasValue ? (object?)new { id = i.ParentId.Value } : null,
            translatedIsoCodes = i.TranslatedIsoCodes,
        });

        return Ok(new { items = result, total });
    }
}
