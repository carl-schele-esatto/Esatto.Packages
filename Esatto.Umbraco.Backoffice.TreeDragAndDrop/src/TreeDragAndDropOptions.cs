namespace Esatto.Umbraco.Backoffice.TreeDragAndDrop;

/// <summary>
/// Per-tree switches, bound from <c>Esatto:TreeDragAndDrop</c>. A missing section or key means
/// enabled. Changes need an app restart (Umbraco caches package manifests).
/// Content and Media switches arrive with their adapters in 1.1.0 / 1.2.0.
/// </summary>
public sealed class TreeDragAndDropOptions
{
    public const string SectionName = "Esatto:TreeDragAndDrop";

    /// <summary>Drag-and-drop in the Translation section's Dictionary tree.</summary>
    public bool Dictionary { get; set; } = true;
}
