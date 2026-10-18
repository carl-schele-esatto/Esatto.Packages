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
          // The backoffice sizes the iframe to the content; the preview itself never scrolls vertically,
          // so the mouse wheel scrolls the backoffice page and a clipped outer margin can't be scrolled
          // into view. Content wider than the frame scrolls sideways, as it would on the page.
          document.documentElement.style.overflowY = "hidden";
          document.documentElement.style.overflowX = "auto";
          // The content's bottom edge, not the document height: a block's outer margin
          // (spacing meant for the page around it) would otherwise show as empty space.
          // Rects are viewport-relative, so add back any scroll (a # link, focus) to get document space.
          function contentBottom() {
            var bottom = 0;
            for (var i = 0; i < document.body.children.length; i++) {
              bottom = Math.max(bottom, document.body.children[i].getBoundingClientRect().bottom);
            }
            return bottom > 0 ? bottom + window.scrollY : document.documentElement.scrollHeight;
          }
          // A horizontal scrollbar takes height from the frame; include it so it doesn't cover the content.
          function scrollbarHeight() {
            return Math.max(0, window.innerHeight - document.documentElement.clientHeight);
          }
          // Video players (YouTube) can't run inside this sandbox. A page script calls
          // EsattoBlockPreview.playEmbed(element, url) instead of inserting the player, and the backoffice
          // shows that player over the element, outside the sandbox (it only accepts known players).
          // Positions are viewport-relative, which is the iframe's own box; they're resent on changes.
          var embeds = [];
          function postEmbeds() {
            var list = [];
            for (var i = 0; i < embeds.length; i++) {
              if (!embeds[i].element.isConnected) continue;
              var r = embeds[i].element.getBoundingClientRect();
              list.push({ url: embeds[i].url, rect: { top: r.top, left: r.left, width: r.width, height: r.height } });
            }
            parent.postMessage({ type: "esatto-block-preview:embeds", id: id, embeds: list }, "*");
          }
          window.EsattoBlockPreview = {
            playEmbed: function (element, url) {
              embeds = embeds.filter(function (e) { return e.element !== element; });
              embeds.push({ element: element, url: String(url) });
              observer.observe(element);
              postEmbeds();
            }
          };
          function post() {
            var height = Math.ceil(contentBottom() + scrollbarHeight());
            parent.postMessage({ type: "esatto-block-preview:height", id: id, height: height }, "*");
            if (embeds.length) postEmbeds();
          }
          window.addEventListener("scroll", function () { if (embeds.length) postEmbeds(); });
          // Watch the content elements themselves, not just html/body: with html, body { height: 100% }
          // the body box follows the frame, so growing content (accordion, tabs) wouldn't resize it.
          var observer = new ResizeObserver(post);
          function observeContent() {
            observer.observe(document.documentElement);
            observer.observe(document.body);
            for (var i = 0; i < document.body.children.length; i++) observer.observe(document.body.children[i]);
          }
          observeContent();
          new MutationObserver(function () { observeContent(); post(); }).observe(document.body, { childList: true });
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
