using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Esatto.Umbraco.Backoffice.CookieBanner.TagHelpers;

/// <summary>
/// Renders the consent dialog by invoking the <c>ConsentBanner</c> view component.
/// </summary>
/// <remarks>
/// Belongs first inside <c>&lt;body&gt;</c>, before the site header, so the dialog is reachable in
/// DOM order by keyboard.
/// </remarks>
[HtmlTargetElement("consent-banner", TagStructure = TagStructure.WithoutEndTag)]
public sealed class ConsentBannerTagHelper(IViewComponentHelper viewComponentHelper) : TagHelper
{
    /// <summary>
    /// The name MVC registers <see cref="ConsentBannerViewComponent"/> under: the class name minus
    /// its "ViewComponent" suffix.
    /// </summary>
    internal const string ViewComponentName = "ConsentBanner";

    [HtmlAttributeNotBound]
    [ViewContext]
    public ViewContext ViewContext { get; set; } = null!;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        // IViewComponentHelper is resolved from DI without a ViewContext. Contextualizing it is not
        // optional: invoking it uncontextualized throws at request time.
        ((IViewContextAware)viewComponentHelper).Contextualize(ViewContext);

        IHtmlContent dialog = await viewComponentHelper.InvokeAsync(ViewComponentName, null);

        // The element is a marker, so it is replaced by the dialog rather than wrapping it.
        output.TagName = null;
        output.TagMode = TagMode.StartTagOnly;
        output.Content.SetHtmlContent(dialog);
    }
}
