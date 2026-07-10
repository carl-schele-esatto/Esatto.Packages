namespace Esatto.Umbraco.Backoffice.CrossContent;

/// <summary>A pickable item from the target site: its key, display title, and content type.</summary>
public sealed record CrossContentCaseListItem(Guid Key, string Title, string Type);
