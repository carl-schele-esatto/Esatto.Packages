namespace Esatto.Umbraco.Backoffice.TreeDragAndDrop;

/// <summary>
/// Per-tree switches, bound from <c>Esatto:TreeDragAndDrop</c>. A missing section or key means
/// enabled. Changes need an app restart (Umbraco caches package manifests).
/// Ignored for a tree whose legacy package (ContentTreeDragAndDrop / MediaTreeDragAndDrop) is still installed.
/// </summary>
public sealed class TreeDragAndDropOptions
{
    public const string SectionName = "Esatto:TreeDragAndDrop";

    /// <summary>Drag-and-drop in the Content section's document tree.</summary>
    public bool Content { get; set; } = true;

    /// <summary>Drag-and-drop in the Media section's tree.</summary>
    public bool Media { get; set; } = true;

    /// <summary>Drag-and-drop in the Translation section's Dictionary tree.</summary>
    public bool Dictionary { get; set; } = true;
}
