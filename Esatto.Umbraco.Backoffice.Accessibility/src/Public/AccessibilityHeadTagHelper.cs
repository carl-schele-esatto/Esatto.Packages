using System.Text;
using System.Text.Encodings.Web;
using Esatto.Umbraco.Backoffice.Accessibility.Settings;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Esatto.Umbraco.Backoffice.Accessibility.Public;

/// <summary>
/// <c>&lt;a11y-head /&gt;</c>, in <c>&lt;head&gt;</c>: the visitor stylesheet and the small boot script that
/// applies saved preferences and OS settings before the page paints. Emits nothing when every visitor
/// feature is switched off.
/// </summary>
/// <remarks>
/// The boot script is an external, synchronous script (not inline) so sites with a strict
/// Content-Security-Policy don't need a nonce; its configuration travels in data attributes. Asset URLs carry
/// a content hash (as asp-append-version does), so a package upgrade reaches browsers that cached the old files.
/// </remarks>
[HtmlTargetElement("a11y-head", TagStructure = TagStructure.WithoutEndTag)]
public sealed class AccessibilityHeadTagHelper(SettingsService settings, IFileVersionProvider versions) : TagHelper
{
    internal const string StylesheetPath = "/esatto-accessibility/a11y.css";
    internal const string BootScriptPath = "/esatto-accessibility/a11y-boot.js";

    [HtmlAttributeNotBound]
    [ViewContext]
    public ViewContext ViewContext { get; set; } = null!;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        var visitor = (await settings.GetAsync()).Settings.Visitor;
        if (!visitor.FollowOsMotion && !visitor.FollowOsContrast && !visitor.Panel.Enabled)
        {
            output.SuppressOutput();
            return;
        }

        var encoder = HtmlEncoder.Default;
        var options = visitor.Panel.Enabled ? string.Join(',', PanelOptions.Enabled(visitor.Panel).Select(o => o.Pref)) : string.Empty;
        var pathBase = ViewContext.HttpContext.Request.PathBase;
        var html = new StringBuilder()
            .Append($"""<link rel="stylesheet" href="{encoder.Encode(versions.AddFileVersionToPath(pathBase, StylesheetPath))}" />""")
            .Append($"""<script src="{encoder.Encode(versions.AddFileVersionToPath(pathBase, BootScriptPath))}" data-follow-motion="{Bool(visitor.FollowOsMotion)}" data-follow-contrast="{Bool(visitor.FollowOsContrast)}" data-options="{encoder.Encode(options)}"></script>""");
        output.Content.SetHtmlContent(html.ToString());
    }

    private static string Bool(bool value) => value ? "true" : "false";
}
