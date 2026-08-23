using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Esatto.Umbraco.Backoffice.CookieBanner.TagHelpers;

/// <summary>
/// Renders a third-party embed, or a placeholder inviting the visitor to grant the category it needs.
/// </summary>
/// <remarks>
/// The placeholder deliberately does not contain the embed URL in any form. Emitting it - even hidden,
/// even in a data attribute - is how "blocked" embeds end up firing requests anyway.
/// <para>
/// Text comes from <see cref="IConsentTextProvider" /> rather than the <c>ICultureDictionary</c>
/// indexer, which has no fallback at all: a site missing the dictionary item rendered an empty
/// paragraph and an unlabelled button.
/// </para>
/// </remarks>
[HtmlTargetElement("consent-embed", TagStructure = TagStructure.WithoutEndTag)]
public sealed class ConsentEmbedTagHelper(IConsentState consent, IConsentTextProvider text) : TagHelper
{
    /// <summary>The consent category this element is gated on.</summary>
    /// <remarks>
    /// In Razor, the attribute value must exactly match the PascalCase enum member name, e.g.
    /// <c>category="Statistics"</c>, not <c>category="statistics"</c>. Tag-helper attribute
    /// codegen binds this case-sensitively, so a lowercase value fails at compile time with CS0117.
    /// </remarks>
    [HtmlAttributeName("category")]
    public ConsentCategory Category { get; set; } = ConsentCategory.Marketing;

    [HtmlAttributeName("src")]
    public string? Src { get; set; }

    [HtmlAttributeName("title")]
    public string? Title { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        HtmlEncoder encoder = HtmlEncoder.Default;
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;

        if (consent.HasGranted(Category))
        {
            output.Attributes.SetAttribute("class", "consent-embed");
            output.Content.SetHtmlContent(
                $"""<iframe src="{encoder.Encode(Src ?? string.Empty)}" title="{encoder.Encode(Title ?? string.Empty)}" loading="lazy" allowfullscreen></iframe>""");
            return;
        }

        var body = text.Get("Cookies.Embed.Blocked.Body");
        var button = text.Get("Cookies.Embed.Blocked.Button");

        output.Attributes.SetAttribute("class", "consent-embed consent-embed--blocked");
        output.Attributes.SetAttribute("data-consent-category", ConsentCategories.ToWireName(Category));
        output.Content.SetHtmlContent(
            $"""
            <p>{encoder.Encode(body)}</p>
            <button type="button" class="consent-btn consent-btn--primary" data-consent-open>{encoder.Encode(button)}</button>
            """);
    }
}
