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
