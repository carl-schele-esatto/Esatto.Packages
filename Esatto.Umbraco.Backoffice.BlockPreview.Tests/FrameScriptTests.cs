using Esatto.Umbraco.Backoffice.BlockPreview.Rendering;
using Xunit;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Tests;

public class FrameScriptTests
{
    [Fact]
    public void Injects_before_the_closing_body_tag_once()
    {
        string result = FrameScript.Inject("<html><body><p>x</p></body></html>", "abc");
        int script = result.IndexOf("esatto-block-preview:height", StringComparison.Ordinal);
        Assert.True(script > 0 && script < result.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, CountOf(result, "esatto-block-preview:height"));
    }

    // Review Focus 3.
    [Fact]
    public void Appends_script_when_body_tag_missing()
    {
        string result = FrameScript.Inject("<p>no body tag</p>", "abc");
        Assert.StartsWith("<p>no body tag</p>", result);
        Assert.Contains("esatto-block-preview:height", result);
    }

    [Fact]
    public void Matches_body_tag_case_insensitively()
    {
        string result = FrameScript.Inject("<HTML><BODY>x</BODY></HTML>", "abc");
        Assert.True(result.IndexOf("<script>", StringComparison.Ordinal) < result.IndexOf("</BODY>", StringComparison.Ordinal));
    }

    // Live finding (Task 7): the carousel's margin-bottom (4rem) collapsed into the document height,
    // leaving empty space under every preview. The height must be the content's bottom edge
    // (outer margins excluded), not the whole document.
    [Fact]
    public void Measures_the_content_bottom_edge_not_the_document_height()
    {
        string result = FrameScript.Inject("<body></body>", "abc");
        Assert.Contains("document.body.children", result);
        Assert.DoesNotContain("document.documentElement.getBoundingClientRect().height", result);
    }

    // Live finding (Task 7): sized to the content, the document (content + trailing margin) was still
    // taller than the iframe, so it scrolled — the mouse wheel over a preview scrolled the block, not the
    // page. The frame's height is managed by the backoffice; the preview itself must never scroll.
    [Fact]
    public void The_preview_document_never_scrolls_vertically()
    {
        Assert.Contains("overflowY = \"hidden\"", FrameScript.Inject("<body></body>", "abc"));
    }

    // Deferred minor (1.0.1): overflow hidden on both axes clipped content wider than the preview.
    // It scrolls sideways like the page would, and the scrollbar's height is added so it doesn't
    // cover the block's bottom edge.
    [Fact]
    public void Wide_content_scrolls_sideways_and_the_scrollbar_is_measured()
    {
        string result = FrameScript.Inject("<body></body>", "abc");
        Assert.Contains("overflowX = \"auto\"", result);
        Assert.Contains("window.innerHeight - document.documentElement.clientHeight", result);
    }

    // Deferred minor (1.0.1): a # link or focus scrolls the document programmatically, and the
    // viewport-relative bottom edge then came out short by the scroll offset.
    [Fact]
    public void Height_adds_back_the_scroll_offset()
    {
        Assert.Contains("window.scrollY", FrameScript.Inject("<body></body>", "abc"));
    }

    // Final review, Important 6: with html, body { height: 100% } the body box follows the viewport, so
    // growing content (accordion, tabs, a taller slide) never resizes it. Watch the content itself.
    [Fact]
    public void Watches_the_content_elements_and_new_ones()
    {
        string result = FrameScript.Inject("<body></body>", "abc");
        Assert.Contains("MutationObserver", result);
        Assert.Contains("observer.observe(document.body.children[i])", result);
    }

    [Fact]
    public void Embeds_the_nonce_as_a_json_string()
    {
        Assert.Contains("\"a\\u0022b\"", FrameScript.Inject("<body></body>", "a\"b"));
    }

    private static int CountOf(string text, string value)
    {
        int count = 0;
        for (int i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + 1, StringComparison.Ordinal)) count++;
        return count;
    }
}
