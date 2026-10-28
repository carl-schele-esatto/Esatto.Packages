using System.Text.RegularExpressions;

namespace Esatto.Umbraco.Backoffice.Accessibility.Knowledge;

/// <summary>Reads WCAG success criteria out of axe-core rule tags.</summary>
public static partial class AxeTags
{
    // "wcag111" → 1.1.1, "wcag1410" → 1.4.10. Principle and guideline are always one digit (max 4 and 5).
    // Level tags ("wcag2a", "wcag21aa", "wcag22aa") contain letters and never match.
    [GeneratedRegex(@"^wcag(\d)(\d)(\d{1,2})$")]
    private static partial Regex CriterionTag();

    public static IReadOnlyList<string> CriteriaFromTags(IEnumerable<string> tags)
        => tags
            .Select(tag => CriterionTag().Match(tag))
            .Where(m => m.Success)
            .Select(m => $"{m.Groups[1].Value}.{m.Groups[2].Value}.{m.Groups[3].Value}")
            .Distinct()
            .ToList();
}
