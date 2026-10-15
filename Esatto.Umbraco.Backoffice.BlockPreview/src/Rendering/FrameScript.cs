using System.Text.Json;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Rendering;

/// <summary>
/// The inline script every preview page gets. It reports the page height to the backoffice
/// (which sizes the iframe) and stops links inside the preview from navigating it away.
/// </summary>
public static class FrameScript
{
    private const string Template = """
        <script>
        (function () {
          var id = __NONCE__;
          function post() {
            var height = Math.ceil(document.documentElement.getBoundingClientRect().height);
            parent.postMessage({ type: "esatto-block-preview:height", id: id, height: height }, "*");
          }
          new ResizeObserver(post).observe(document.documentElement);
          window.addEventListener("load", post);
          document.addEventListener("click", function (e) {
            var a = e.target && e.target.closest ? e.target.closest("a[href]") : null;
            if (a && a.getAttribute("href").charAt(0) !== "#") e.preventDefault();
          }, true);
        })();
        </script>
        """;

    public static string Inject(string html, string nonce)
    {
        // JsonSerializer escapes quotes and '<' so the nonce can't break out of the script.
        string script = Template.Replace("__NONCE__", JsonSerializer.Serialize(nonce));
        int bodyEnd = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return bodyEnd < 0 ? html + script : html.Insert(bodyEnd, script);
    }
}
