namespace Esatto.Umbraco.Backoffice.BlockPreview.Rendering;

/// <summary>Model of the package's Frame view: which partial to render, with what, inside which layout.</summary>
public sealed record FrameModel(string PartialPath, object Item, string LayoutPath, string[] Stylesheets, string[] Scripts);
