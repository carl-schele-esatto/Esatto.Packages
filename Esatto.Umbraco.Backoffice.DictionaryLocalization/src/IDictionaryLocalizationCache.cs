namespace Esatto.Umbraco.Backoffice.DictionaryLocalization;

/// <summary>
/// A flattened dictionary entry with its non-empty translations, ready to serialize to
/// the backoffice as one culture-keyed map row.
/// </summary>
public sealed record DictionaryLocalizationEntry(
    string Key,
    IReadOnlyDictionary<string, string> ValuesByIso);

/// <summary>
/// Holds the whole content dictionary flattened by key, so the localization endpoint
/// can return the culture-grouped payload without re-walking the dictionary tree on
/// every request. Built once (lazily) and rebuilt only when invalidated on save/delete.
/// </summary>
public interface IDictionaryLocalizationCache
{
    /// <summary>Returns the cached set, building it on first use after each invalidation.</summary>
    Task<IReadOnlyList<DictionaryLocalizationEntry>> GetAllAsync();

    /// <summary>Drops the cached set so the next <see cref="GetAllAsync"/> rebuilds it.</summary>
    void Invalidate();
}
