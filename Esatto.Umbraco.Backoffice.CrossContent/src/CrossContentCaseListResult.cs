namespace Esatto.Umbraco.Backoffice.CrossContent;

/// <summary>Picker payload: the configured content types (for the dropdown) plus the merged items.</summary>
public sealed record CrossContentCaseListResult(
    IReadOnlyList<CrossContentContentTypeInfo> Types,
    IReadOnlyList<CrossContentCaseListItem> Items)
{
    /// <summary>Projects configured options into dropdown infos (label → alias fallback,
    /// blank aliases skipped) and pairs them with the already-fetched items.</summary>
    public static CrossContentCaseListResult Create(
        IEnumerable<CrossContentContentTypeOption> typeOptions,
        IReadOnlyList<CrossContentCaseListItem> items)
    {
        var types = typeOptions
            .Where(t => !string.IsNullOrWhiteSpace(t.Alias))
            .Select(t => new CrossContentContentTypeInfo(
                t.Alias, string.IsNullOrWhiteSpace(t.Label) ? t.Alias : t.Label!))
            .ToList();
        return new CrossContentCaseListResult(types, items);
    }
}
