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
          // The backoffice sizes the iframe to the content; the preview itself never scrolls, so the
          // mouse wheel scrolls the backoffice page and a clipped outer margin can't be scrolled into view.
          document.documentElement.style.overflow = "hidden";
          document.body.style.overflow = "hidden";
          // The content's bottom edge, not the document height: a block's outer margin
          // (spacing meant for the page around it) would otherwise show as empty space.
          function contentBottom() {
            var bottom = 0;
            for (var i = 0; i < document.body.children.length; i++) {
              bottom = Math.max(bottom, document.body.children[i].getBoundingClientRect().bottom);
            }
            return bottom > 0 ? bottom : document.documentElement.scrollHeight;
          }
          function post() {
            var height = Math.ceil(contentBottom());
            parent.postMessage({ type: "esatto-block-preview:height", id: id, height: height }, "*");
          }
          var observer = new ResizeObserver(post);
          observer.observe(document.documentElement);
          observer.observe(document.body);
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
