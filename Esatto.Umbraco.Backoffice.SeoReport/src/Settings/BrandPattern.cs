using System.Text.RegularExpressions;

namespace Esatto.Umbraco.Backoffice.SeoReport.Settings;

/// <summary>
/// Builds the regular expression Search Console filters brand queries on from the list of terms a person typed, so nobody
/// has to write (or escape) a pattern. Search Console evaluates it with RE2, so the pattern stays within what both RE2 and
/// .NET read the same way: an inline <c>(?i)</c> flag, a group and plain alternation.
/// </summary>
public static class BrandPattern
{
    /// <summary>
    /// The longest pattern built, in characters. Google refuses an over-long expression (Search Console's own regex filter takes
    /// 4,096 characters), and then every month would fail; the terms that do not fit are left out instead.
    /// </summary>
    public const int MaxLength = 4000;

    private const string Prefix = "(?i)(";
    private const string Suffix = ")";

    /// <summary>
    /// <c>(?i)(a|b)</c> for the terms as the settings keep them (see <see cref="SettingsNormalizer.NormalizeList"/>: tidied, without
    /// blanks or duplicates, at most 50 of at most 100 characters), escaped so they match literally, in the order given. When the
    /// pattern would be longer than <see cref="MaxLength"/>, the first term that does not fit and every term after it are left out:
    /// the order is the person's priority. Null when there is no term.
    /// </summary>
    public static string? Build(IEnumerable<string> terms)
    {
        var escaped = new List<string>();
        var length = Prefix.Length + Suffix.Length;
        foreach (var term in SettingsNormalizer.NormalizeList(terms).Select(Escape))
        {
            length += term.Length + (escaped.Count == 0 ? 0 : 1);
            if (length > MaxLength) break;

            escaped.Add(term);
        }

        return escaped.Count == 0 ? null : $"{Prefix}{string.Join('|', escaped)}{Suffix}";
    }

    // Regex.Escape also escapes a space ("\ "), which RE2 refuses; a space is an ordinary character outside a character class.
    // Every space the escaper writes is preceded by exactly one escaping backslash, so this cannot touch an escaped backslash.
    private static string Escape(string term) => Regex.Escape(term).Replace("\\ ", " ", StringComparison.Ordinal);
}
