using Esatto.Umbraco.Backoffice.Accessibility.Settings;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Esatto.Umbraco.Backoffice.Accessibility.Public;

/// <summary>
/// <c>&lt;a11y-panel /&gt;</c>, at the end of <c>&lt;body&gt;</c>: the accessibility preferences button and
/// dialog (the <c>AccessibilityPanel</c> view component). Emits nothing when the panel is switched off.
/// </summary>
[HtmlTargetElement("a11y-panel", TagStructure = TagStructure.WithoutEndTag)]
public sealed class AccessibilityPanelTagHelper(IViewComponentHelper viewComponentHelper, SettingsService settings) : TagHelper
{
    /// <summary>The name MVC registers <see cref="AccessibilityPanelViewComponent"/> under.</summary>
    public const string ViewComponentName = "AccessibilityPanel";

    [HtmlAttributeNotBound]
    [ViewContext]
    public ViewContext ViewContext { get; set; } = null!;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        if (!(await settings.GetAsync()).Settings.Visitor.Panel.Enabled)
        {
            output.SuppressOutput();
            return;
        }

        // IViewComponentHelper from DI has no ViewContext; invoking it uncontextualized throws.
        ((IViewContextAware)viewComponentHelper).Contextualize(ViewContext);
        IHtmlContent panel = await viewComponentHelper.InvokeAsync(ViewComponentName, null);
        output.Content.SetHtmlContent(panel);
    }
}
