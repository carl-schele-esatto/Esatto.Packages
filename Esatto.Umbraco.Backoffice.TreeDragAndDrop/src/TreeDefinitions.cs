namespace Esatto.Umbraco.Backoffice.TreeDragAndDrop;

/// <summary>A tree this package can enable: its switch, its section, and its client entry file.</summary>
internal sealed record TreeDefinition(string Name, string SectionAlias, Func<TreeDragAndDropOptions, bool> IsEnabled)
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
    // Content (Umb.Section.Content) and Media (Umb.Section.Media) join here in 1.1.0 / 1.2.0.
    public static readonly IReadOnlyList<TreeDefinition> All =
    [
        new("Dictionary", "Umb.Section.Translation", options => options.Dictionary),
    ];
}
