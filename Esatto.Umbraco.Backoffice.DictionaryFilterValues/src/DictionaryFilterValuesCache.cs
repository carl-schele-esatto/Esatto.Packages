using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Esatto.Umbraco.Backoffice.DictionaryFilterValues;

/// <summary>
/// Default <see cref="IDictionaryFilterValuesCache"/>: a process-wide singleton that
/// pulls the whole dictionary once via <see cref="IDictionaryItemService.GetDescendantsAsync"/>
/// (one bulk call, not an N+1 tree walk) and serves it until invalidated.
/// </summary>
internal sealed class DictionaryFilterValuesCache : IDictionaryFilterValuesCache
{
    private readonly IDictionaryItemService _dictionaryItemService;
    private readonly SemaphoreSlim _buildLock = new(1, 1);
    private volatile IReadOnlyList<CachedDictionaryItem>? _cache;

    public DictionaryFilterValuesCache(IDictionaryItemService dictionaryItemService)
        => _dictionaryItemService = dictionaryItemService;

    public async Task<IReadOnlyList<CachedDictionaryItem>> GetAllAsync()
    {
        var snapshot = _cache;
        if (snapshot is not null)
        {
            return snapshot;
        }

        await _buildLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Double-check: another caller may have built it while we waited.
            if (_cache is not null)
            {
                return _cache;
            }

            // parentId null => the whole dictionary, flattened, in a single call.
            var items = await _dictionaryItemService.GetDescendantsAsync(null).ConfigureAwait(false);
            var built = items.Select(Map).ToArray();
            _cache = built;
            return built;
        }
        finally
        {
            _buildLock.Release();
        }
    }

    public void Invalidate() => _cache = null;

    private static CachedDictionaryItem Map(IDictionaryItem item)
    {
        var translations = item.Translations
            .Where(t => !string.IsNullOrEmpty(t.Value))
            .Select(t => new DictionaryTranslationValue(t.LanguageIsoCode, t.Value))
            .ToArray();

        return new CachedDictionaryItem(item.Key, item.ParentId, item.ItemKey, translations);
    }
}
