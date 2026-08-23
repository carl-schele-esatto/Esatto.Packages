using System;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Options;

namespace Esatto.Umbraco.Backoffice.CookieBanner.TagHelpers;

/// <summary>
/// Emits everything the package needs inside <c>&lt;head&gt;</c>: the stylesheet, plus - only when a
/// Google measurement id is configured - the Consent Mode v2 block and the gated gtag.js tag.
/// </summary>
/// <remarks>
/// This exists so the Consent Mode call sequence is package-internal rather than copy-pasted into
/// every consumer's layout, where the deliberate second <c>update</c> call reads like a duplicate and
/// invites deletion.
/// </remarks>
[HtmlTargetElement("consent-head", TagStructure = TagStructure.WithoutEndTag)]
public sealed class ConsentHeadTagHelper(
    IConsentState consent,
    IOptions<CookieBannerOptions> options) : TagHelper
{
    /// <summary>
    /// Root-relative on purpose: the file is a static web asset served from the package's wwwroot at
    /// this literal path (StaticWebAssetBasePath=/), and a tag helper builds raw markup with no
    /// IUrlHelper to expand a tilde.
    /// </summary>
    internal const string StylesheetPath = "/esatto-cookiebanner/consent.css";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        // The element itself is only a marker; nothing but its replacement content is rendered.
        output.TagName = null;
        output.TagMode = TagMode.StartTagOnly;

        CookieBannerOptions settings = options.Value;
        HtmlEncoder encoder = HtmlEncoder.Default;
        var head = new StringBuilder();

        head.Append($"""<link rel="stylesheet" href="{StylesheetPath}" />""");

        var measurementId = settings.GoogleMeasurementId;
        if (string.IsNullOrWhiteSpace(measurementId))
        {
            // No measurement id: emit no Google-related markup at all rather than dead script.
            output.Content.SetHtmlContent(head.ToString());
            return;
        }

        // Consent default must run before anything else Google-related. Update runs again here,
        // synchronously, straight after Defaults - even though consent.js also calls it once the
        // page has loaded - because Defaults leaves a 500ms wait_for_update window during which
        // gtag.js (once it loads) would otherwise see only the "denied" defaults. Emitting the real
        // per-request state immediately closes that window rather than relying on consent.js's later
        // call, which may run after gtag.js has already sent its first, wrongly-denied signals.
        // Do not delete this as a duplicate of consent.js's call.
        head.Append("<script>")
            .Append(ConsentModeScript.Defaults())
            .Append(ConsentModeScript.Update(consent))
            .Append(ConsentModeScript.Config(measurementId))
            .Append("</script>");

        // The same server-side gate <consent-script category="Statistics"> applies: with statistics
        // declined the library never reaches the browser, so it cannot execute at all.
        if (consent.HasGranted(ConsentCategory.Statistics))
        {
            var src = "https://www.googletagmanager.com/gtag/js?id=" + Uri.EscapeDataString(measurementId);
            head.Append($"""<script async src="{encoder.Encode(src)}"></script>""");
        }

        output.Content.SetHtmlContent(head.ToString());
    }
}
