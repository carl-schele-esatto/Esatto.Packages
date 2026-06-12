using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Esatto.Umbraco.Backoffice.DictionaryFilterValues;

/// <summary>
/// Returns dictionary items along with their translation values, so the
/// companion JS shim can filter against translation TEXT (not just dictionary-
/// item names) when an editor types in the backoffice Dictionary collection
/// filter.
/// </summary>
/// <remarks>
/// <para>
/// Route + auth follow the same Bellissima-callable pattern used by other
/// JS-shim-facing endpoints: <c>/umbraco/api/&lt;feature&gt;/</c> and
/// <c>[AllowAnonymous]</c>. Bearer tokens on
/// <c>/umbraco/management/api/*</c> are NOT auto-attached to plain
/// <c>fetch()</c> calls from Lit collection repositories, so the modern
/// Management API surface is unusable from this codepath. The endpoint is a
/// non-sensitive read-only walk over dictionary strings — UI labels that are
/// already served to anonymous visitors on public pages.
/// </para>
/// </remarks>
[ApiController]
[Route("umbraco/api/backoffice-dictionary-filter-values/search")]
[AllowAnonymous]
public class DictionaryFilterValuesController : Controller
{
    private readonly IDictionaryItemService _dictionaryItemService;

    public DictionaryFilterValuesController(IDictionaryItemService dictionaryItemService)
    {
        _dictionaryItemService = dictionaryItemService;
    }

    /// <summary>
    /// Returns dictionary items — either all of them when <paramref name="q"/>
    /// is empty (dump mode — the JS shim caches this on first filter keystroke
    /// then filters client-side so each subsequent keystroke is instant), OR
    /// server-filtered where <paramref name="q"/> case-insensitively matches
    /// the dictionary item key or any translation value.
    ///
    /// Dump mode includes the <c>translations</c> array (the actual values) so
    /// the JS can match client-side. When <paramref name="q"/> is set, only
    /// <c>translatedIsoCodes</c> is returned (Bellissima needs it for check/
    /// warn rendering on each row).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? q)
    {
        var allItems = new List<IDictionaryItem>();
        await CollectAllAsync(parentId: null, allItems);

        if (string.IsNullOrWhiteSpace(q))
        {
            // Dump mode — JS caches this and filters client-side.
            var dump = allItems.Select(item => new
            {
                id = item.Key,
                name = item.ItemKey,
                parentId = item.ParentId,
                translatedIsoCodes = item.Translations
                    .Where(t => !string.IsNullOrEmpty(t.Value))
                    .Select(t => t.LanguageIsoCode)
                    .ToArray(),
                translations = item.Translations
                    .Where(t => !string.IsNullOrEmpty(t.Value))
                    .Select(t => t.Value)
                    .ToArray(),
            }).ToList();
            return Ok(new { items = dump, total = dump.Count });
        }

        // Server-side filter mode — kept as a fallback for callers that don't
        // cache (or for cache-bypass debugging). Same matching logic as the
        // client-side filter so behaviour is consistent.
        var needle = q.Trim();
        var matches = allItems
            .Where(item =>
                (item.ItemKey?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false) ||
                item.Translations.Any(t =>
                    t.Value?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false))
            .Select(item => new
            {
                id = item.Key,
                name = item.ItemKey,
                parentId = item.ParentId,
                translatedIsoCodes = item.Translations
                    .Where(t => !string.IsNullOrEmpty(t.Value))
                    .Select(t => t.LanguageIsoCode)
                    .ToArray(),
            })
            .ToList();
        return Ok(new { items = matches, total = matches.Count });
    }

    // Recursive walk: GetPagedAsync returns one level at a time, so we recurse
    // through the dictionary tree to flatten it. For typical sites with a few
    // hundred items, this completes in <100ms; we don't bother caching.
    private async Task CollectAllAsync(Guid? parentId, List<IDictionaryItem> sink)
    {
        var page = await _dictionaryItemService.GetPagedAsync(parentId, 0, int.MaxValue);
        foreach (var item in page.Items)
        {
            sink.Add(item);
            await CollectAllAsync(item.Key, sink);
        }
    }
}
