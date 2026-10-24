namespace Esatto.Umbraco.Backoffice.NavigationOrder;

/// <summary>The order of one section's tabs: dashboard and section-view aliases, first to last.</summary>
public sealed record TabOrder(string Section, IReadOnlyList<string> Aliases);

/// <summary>
/// The saved backoffice order. <paramref name="Sections"/> is the top bar, first to last; null = Umbraco's
/// default order. <paramref name="Tabs"/> holds one list per section that has a saved tab order.
/// </summary>
public sealed record NavigationOrderSettings(IReadOnlyList<string>? Sections, IReadOnlyList<TabOrder> Tabs)
{
    public static NavigationOrderSettings Empty { get; } = new(null, []);
}

/// <summary>The saved order, with when and by whom it was last saved (null when nothing is saved).</summary>
public sealed record StoredNavigationOrder(NavigationOrderSettings Settings, DateTime? UpdatedUtc, string? UpdatedBy)
{
    public static StoredNavigationOrder Empty { get; } = new(NavigationOrderSettings.Empty, null, null);
}
