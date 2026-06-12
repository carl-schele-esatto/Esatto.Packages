namespace Esatto.Umbraco.Backoffice.DictionaryFilterValues;

/// <summary>A matched dictionary item, shaped for the backoffice collection client.</summary>
public sealed record DictionaryFilterResultItem(Guid Id, string Name, Guid? ParentId, string[] TranslatedIsoCodes);

/// <summary>
/// Pure matching + paging used by the endpoint. Match is case-insensitive against the
/// dictionary key (name) OR any translation value — this is the feature: editors can
/// find an item by a translated string, not just its key. Kept as a static helper so it
/// is trivially unit-testable and is the single source of truth for the match rule.
/// </summary>
public static class DictionaryFilterValuesFilter
{
    public static (IReadOnlyList<DictionaryFilterResultItem> Items, long Total) Apply(
        IReadOnlyList<CachedDictionaryItem> items,
        string? filter,
        int skip,
        int take)
    {
        IEnumerable<CachedDictionaryItem> matched = items;

        var needle = filter?.Trim();
        if (!string.IsNullOrEmpty(needle))
        {
            matched = items.Where(i =>
                i.Name.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                i.Translations.Any(t => t.Value.Contains(needle, StringComparison.OrdinalIgnoreCase)));
        }

        var all = matched.ToArray();

        var page = all
            .Skip(skip < 0 ? 0 : skip)
            .Take(take < 0 ? 0 : take)
            .Select(i => new DictionaryFilterResultItem(
                i.Id,
                i.Name,
                i.ParentId,
                i.Translations.Select(t => t.Iso).ToArray()))
            .ToArray();

        return (page, all.LongLength);
    }
}
