using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Web.Common.Authorization;

namespace Esatto.Umbraco.Backoffice.DictionaryLocalization;

/// <summary>
/// Authenticated backoffice endpoint that returns the whole content dictionary grouped
/// by culture ISO code. The client transforms this payload into
/// <c>UmbLocalizationSetBase</c> objects and registers them into
/// <c>umbLocalizationManager</c> so <c>#Key</c> tokens resolve everywhere
/// <c>localize.string()</c> is called.
/// </summary>
[ApiController]
[VersionedApiBackOfficeRoute("backoffice/dictionary-localization")]
[ApiExplorerSettings(GroupName = "Backoffice Dictionary Localization")]
[Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]
public sealed class DictionaryLocalizationController : ManagementApiControllerBase
{
    private readonly IDictionaryLocalizationCache _cache;

    public DictionaryLocalizationController(IDictionaryLocalizationCache cache)
        => _cache = cache;

    /// <summary>
    /// Returns every dictionary entry grouped by lowercased culture ISO code, matching
    /// <c>umbLocalizationManager</c>'s internal keying:
    /// <c>{ cultures: { "sv-se": { "Key1": "Value1", ... }, "en": { ... } } }</c>.
    /// Empty translations are dropped in the cache mapping.
    /// </summary>
    [HttpGet("all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var entries = await _cache.GetAllAsync();

        // Group by culture; each culture maps every dictionary key that has a non-empty
        // value in that culture. Culture ISO lowercased to match the manager.
        var cultures = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            foreach (var (iso, value) in entry.ValuesByIso)
            {
                var isoKey = iso.ToLowerInvariant();
                if (!cultures.TryGetValue(isoKey, out var bucket))
                {
                    bucket = new Dictionary<string, string>(StringComparer.Ordinal);
                    cultures[isoKey] = bucket;
                }

                // Last-wins on duplicate keys within one culture; dictionary items are
                // supposed to be unique but we don't rely on it.
                bucket[entry.Key] = value;
            }
        }

        return Ok(new { cultures });
    }
}
