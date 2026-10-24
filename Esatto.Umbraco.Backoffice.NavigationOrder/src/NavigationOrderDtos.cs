namespace Esatto.Umbraco.Backoffice.NavigationOrder;

// Tab lists are an array of { section, aliases }, never an object keyed by section alias: the
// Management API's serializer may camel-case dictionary keys ("Umb.Section.Settings" → "umb.Section.Settings").

/// <summary>One section's tab order on the wire.</summary>
public sealed record TabOrderDto(string Section, IReadOnlyList<string> Aliases);

/// <summary>The body of <c>PUT esatto/navigation-order</c>. Null <c>sections</c> = default order; null <c>tabs</c> = none saved.</summary>
public sealed record NavigationOrderRequest(IReadOnlyList<string>? Sections, IReadOnlyList<TabOrderDto>? Tabs)
{
    public NavigationOrderSettings ToSettings()
        => new(Sections, Tabs?.Select(tab => new TabOrder(tab.Section, tab.Aliases ?? [])).ToArray() ?? []);
}

/// <summary>The saved order, as returned by GET and PUT.</summary>
public sealed record NavigationOrderResponse(
    IReadOnlyList<string>? Sections,
    IReadOnlyList<TabOrderDto> Tabs,
    DateTime? UpdatedUtc,
    string? UpdatedBy)
{
    public static NavigationOrderResponse From(StoredNavigationOrder stored) => new(
        stored.Settings.Sections,
        stored.Settings.Tabs.Select(tab => new TabOrderDto(tab.Section, tab.Aliases)).ToArray(),
        stored.UpdatedUtc,
        stored.UpdatedBy);
}
