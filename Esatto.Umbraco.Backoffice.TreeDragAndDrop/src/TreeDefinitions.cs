namespace Esatto.Umbraco.Backoffice.TreeDragAndDrop;

/// <summary>
/// A tree this package can enable: its switch, its section, its client entry file, and the
/// legacy package (if any) that already handles it.
/// </summary>
internal sealed record TreeDefinition(
    string Name,
    string SectionAlias,
    Func<TreeDragAndDropOptions, bool> IsEnabled,
    string? LegacyPackageId = null)
{
    public const string PackageId = "Esatto.Umbraco.Backoffice.TreeDragAndDrop";

    // File names match the Vite entry names in Client/vite.config.ts: <tree>-tree-drag-drop.js.
    public EntryPointExtension ToEntryPoint() => new(
        Type: "backofficeEntryPoint",
        Alias: $"{PackageId}.{Name}",
        Name: $"{Name} Tree Drag and Drop",
        Js: $"/App_Plugins/{PackageId}/{Name.ToLowerInvariant()}-tree-drag-drop.js",
        Conditions: [new ExtensionCondition("Umb.Condition.SectionAlias", SectionAlias)]);
}

internal static class TreeDefinitions
{
    public static readonly IReadOnlyList<TreeDefinition> All =
    [
        new("Content", "Umb.Section.Content", options => options.Content, "Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop"),
        new("Media", "Umb.Section.Media", options => options.Media, "Esatto.Umbraco.Backoffice.MediaTreeDragAndDrop"),
        new("Dictionary", "Umb.Section.Translation", options => options.Dictionary),
    ];
}
