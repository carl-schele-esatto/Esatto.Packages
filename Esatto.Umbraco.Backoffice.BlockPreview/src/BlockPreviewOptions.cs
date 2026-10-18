namespace Esatto.Umbraco.Backoffice.BlockPreview;

/// <summary>Bound from <c>Esatto:BlockPreview</c>. Everything is optional.</summary>
public sealed class BlockPreviewOptions
{
    public const string SectionName = "Esatto:BlockPreview";

    public BlockListOptions BlockList { get; set; } = new();

    /// <summary>
    /// The site's preview layout: plain Razor with the site's &lt;head&gt; and scripts and
    /// <c>@RenderBody()</c>. It must not declare a model. Missing → the built-in layout with
    /// <see cref="Stylesheets"/> and <see cref="Scripts"/>.
    /// </summary>
    public string Layout { get; set; } = "~/Views/BlockPreview.cshtml";

    /// <summary>Stylesheet URLs for the built-in layout (ignored when the site layout exists).</summary>
    public string[] Stylesheets { get; set; } = [];

    /// <summary>Script URLs for the built-in layout (ignored when the site layout exists).</summary>
    public string[] Scripts { get; set; } = [];
}

public sealed class BlockListOptions
{
    /// <summary>When false, the generic Block List preview isn't registered at all.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Block content type aliases that keep Umbraco's plain row.</summary>
    public string[] IgnoredContentTypes { get; set; } = [];
}
