using System.Net;
using System.Text.RegularExpressions;

namespace Backoffice.CrossContent;

/// <summary>Text helpers for site mappers. StripHtml mirrors the plain-text approach used for LLMs
/// feeds — no IHtmlHelper needed, safe in an API controller.</summary>
public static partial class CrossContentText
{
    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    public static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var stripped = HtmlTagPattern().Replace(html, " ");
        var decoded = WebUtility.HtmlDecode(stripped);
        return WhitespacePattern().Replace(decoded, " ").Trim();
    }
}
