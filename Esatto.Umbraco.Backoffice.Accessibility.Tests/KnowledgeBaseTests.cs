using Esatto.Umbraco.Backoffice.Accessibility.Knowledge;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Accessibility.Tests;

public class KnowledgeBaseTests
{
    private static readonly KnowledgeBase Kb = new();

    [Theory]
    [InlineData(WcagVersion.V20, WcagLevel.A, 25)]
    [InlineData(WcagVersion.V20, WcagLevel.AA, 38)]
    [InlineData(WcagVersion.V20, WcagLevel.AAA, 61)]
    [InlineData(WcagVersion.V21, WcagLevel.A, 30)]
    [InlineData(WcagVersion.V21, WcagLevel.AA, 50)]
    [InlineData(WcagVersion.V21, WcagLevel.AAA, 78)]
    [InlineData(WcagVersion.V22, WcagLevel.A, 31)] // 32 incl. 4.1.1, obsolete in 2.2
    [InlineData(WcagVersion.V22, WcagLevel.AA, 55)]
    [InlineData(WcagVersion.V22, WcagLevel.AAA, 86)]
    public void Target_selects_the_right_number_of_criteria(WcagVersion version, WcagLevel level, int count)
        => Assert.Equal(count, Kb.For(new WcagTarget(version, level)).Count);

    [Fact]
    public void Parsing_4_1_1_is_in_2_1_but_not_2_2()
    {
        Assert.Contains(Kb.For(new WcagTarget(WcagVersion.V21, WcagLevel.A)), c => c.Id == "4.1.1");
        Assert.DoesNotContain(Kb.For(new WcagTarget(WcagVersion.V22, WcagLevel.A)), c => c.Id == "4.1.1");
    }

    [Fact]
    public void Target_size_minimum_is_2_2_AA()
    {
        var c = Kb.Find("2.5.8")!;
        Assert.Equal(WcagLevel.AA, c.Level);
        Assert.Equal(WcagVersion.V22, c.Since);
    }

    [Fact]
    public void Every_criterion_has_an_understanding_url_on_w3_org()
        => Assert.All(Kb.All, c => Assert.StartsWith("https://www.w3.org/WAI/WCAG22/Understanding/", c.UnderstandingUrl));

    [Fact]
    public void All_87_criteria_are_loaded() => Assert.Equal(87, Kb.All.Count);

    [Fact]
    public void Only_language_of_page_is_fully_automatable()
        => Assert.Equal(new[] { "3.1.1" }, Kb.All.Where(c => c.FullyAutomatable).Select(c => c.Id));

    [Fact]
    public void Default_target_is_2_2_AA() => Assert.Equal(new WcagTarget(WcagVersion.V22, WcagLevel.AA), WcagTarget.Default);

    [Fact]
    public void Every_criterion_has_all_four_texts()
        => Assert.Empty(Kb.All
            .Where(c => new[] { c.Why, c.HowToFix, c.Caveats, c.HowToTest }.Any(string.IsNullOrWhiteSpace))
            .Select(c => c.Id));

    [Fact]
    public void Image_alt_is_owned_by_editors_and_mentions_the_media_item()
    {
        var rule = Kb.Rule("image-alt")!;
        Assert.Equal(Owner.Editor, rule.Owner);
        Assert.Contains("media", rule.HowToFix, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Color_contrast_is_owned_by_developers() => Assert.Equal(Owner.Developer, Kb.Rule("color-contrast")!.Owner);

    [Theory]
    [InlineData("image-alt")] [InlineData("role-img-alt")] [InlineData("input-image-alt")] [InlineData("link-name")]
    [InlineData("button-name")] [InlineData("color-contrast")] [InlineData("color-contrast-enhanced")] [InlineData("heading-order")]
    [InlineData("empty-heading")] [InlineData("page-has-heading-one")] [InlineData("html-has-lang")] [InlineData("html-lang-valid")]
    [InlineData("valid-lang")] [InlineData("document-title")] [InlineData("label")] [InlineData("select-name")]
    [InlineData("frame-title")] [InlineData("list")] [InlineData("listitem")] [InlineData("region")]
    [InlineData("landmark-one-main")] [InlineData("target-size")] [InlineData("video-caption")] [InlineData("meta-viewport")]
    [InlineData("aria-valid-attr")] [InlineData("aria-required-attr")] [InlineData("duplicate-id-aria")] [InlineData("link-in-text-block")]
    [InlineData("scrollable-region-focusable")] [InlineData("nested-interactive")]
    public void Listed_axe_rules_have_guidance(string ruleId)
    {
        var rule = Kb.Rule(ruleId);
        Assert.NotNull(rule);
        Assert.NotNull(rule!.Owner);
        Assert.False(string.IsNullOrWhiteSpace(rule.HowToFix));
    }
}
