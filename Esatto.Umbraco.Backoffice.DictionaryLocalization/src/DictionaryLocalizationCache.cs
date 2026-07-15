using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Esatto.Umbraco.Backoffice.DictionaryLocalization;

/// <summary>
/// Default <see cref="IDictionaryLocalizationCache"/>: a process-wide singleton that
/// pulls the whole dictionary once via <see cref="IDictionaryItemService.GetDescendantsAsync"/>
/// (one bulk call, not an N+1 tree walk) and serves it until invalidated.
/// </summary>
internal sealed class DictionaryLocalizationCache : IDictionaryLocalizationCache
{
    private readonly IDictionaryItemService _dictionaryItemService;
    private readonly SemaphoreSlim _buildLock = new(1, 1);
    private volatile IReadOnlyList<DictionaryLocalizationEntry>? _cache;

    public DictionaryLocalizationCache(IDictionaryItemService dictionaryItemService)
        => _dictionaryItemService = dictionaryItemService;

    public async Task<IReadOnlyList<DictionaryLocalizationEntry>> GetAllAsync()
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

    private static DictionaryLocalizationEntry Map(IDictionaryItem item)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var translation in item.Translations)
        {
            if (string.IsNullOrEmpty(translation.Value))
            {
                continue;
            }

            // Last-wins on duplicate ISO codes for the same key; matches the shape
            // umbLocalizationManager expects (one value per culture per key).
            values[translation.LanguageIsoCode] = translation.Value;
        }

        return new DictionaryLocalizationEntry(item.ItemKey, values);
    }
}
