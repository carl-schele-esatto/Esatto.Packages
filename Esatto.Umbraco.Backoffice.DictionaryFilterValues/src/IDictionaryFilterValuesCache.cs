namespace Esatto.Umbraco.Backoffice.DictionaryFilterValues;

/// <summary>One non-empty translation of a dictionary item.</summary>
public sealed record DictionaryTranslationValue(string Iso, string Value);

/// <summary>A flattened dictionary item with its non-empty translation values.</summary>
public sealed record CachedDictionaryItem(
    Guid Id,
    Guid? ParentId,
    string Name,
    IReadOnlyList<DictionaryTranslationValue> Translations);

/// <summary>
/// Holds the full flattened dictionary (keys + translation values) so the filter
/// endpoint can match without re-walking the dictionary tree on every request.
/// Built once (lazily) and rebuilt only when invalidated on a dictionary save/delete.
/// </summary>
public interface IDictionaryFilterValuesCache
{
    /// <summary>Returns the cached set, building it on first use after each invalidation.</summary>
    Task<IReadOnlyList<CachedDictionaryItem>> GetAllAsync();

    /// <summary>Drops the cached set so the next <see cref="GetAllAsync"/> rebuilds it.</summary>
    void Invalidate();
}
