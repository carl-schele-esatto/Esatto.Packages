using Esatto.Umbraco.Backoffice.Accessibility.Knowledge;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Accessibility.Tests;

public class AxeTagsTests
{
    [Theory]
    [InlineData(new[] { "wcag2a", "wcag111", "cat.text-alternatives" }, new[] { "1.1.1" })]
    [InlineData(new[] { "wcag21aa", "wcag1410" }, new[] { "1.4.10" })]
    [InlineData(new[] { "wcag22aa", "wcag258" }, new[] { "2.5.8" })]
    [InlineData(new[] { "wcag2a", "wcag244", "wcag412" }, new[] { "2.4.4", "4.1.2" })]
    [InlineData(new[] { "best-practice" }, new string[0])]
    [InlineData(new[] { "EN-301-549", "EN-9.1.1.1", "ACT", "TTv5", "section508.22.a" }, new string[0])]
    public void CriteriaFromTags_maps_axe_tags(string[] tags, string[] expected)
        => Assert.Equal(expected, AxeTags.CriteriaFromTags(tags));
}
