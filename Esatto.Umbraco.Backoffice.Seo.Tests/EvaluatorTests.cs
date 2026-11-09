using Esatto.Umbraco.Backoffice.Seo.Checks;
using Esatto.Umbraco.Backoffice.Seo.Evaluation;
using Esatto.Umbraco.Backoffice.Seo.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Seo.Tests;

public class EvaluatorTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();

    private static ScanSnapshot TwoUntitled()
        => Snap.Site("sv", "en")
            .Page("/a", "<html><body></body></html>", key: A, culture: "sv")
            .Page("/en/a", "<html><body></body></html>", key: A, culture: "en")
            .Page("/b", "<html><body></body></html>", key: B, culture: "sv")
            .Build();

    [Fact]
    public void No_ignores_keeps_every_outcome()
    {
        var e = Evaluator.Evaluate(TwoUntitled(), [new TitleMissing()], []);
        Assert.Equal(3, e.Outcomes.Count);
        Assert.Empty(e.Ignored);
    }

    [Fact]
    public void Ignore_whole_check_removes_all_outcomes()
    {
        var e = Evaluator.Evaluate(TwoUntitled(), [new TitleMissing()], [new IgnoreRule(1, CheckIds.TitleMissing, null, null)]);
        Assert.Empty(e.Outcomes);
        Assert.Equal(3, e.Ignored.Count);
    }

    [Fact]
    public void Ignore_page_keeps_other_pages()
    {
        var e = Evaluator.Evaluate(TwoUntitled(), [new TitleMissing()], [new IgnoreRule(1, CheckIds.TitleMissing, A, null)]);
        Assert.Single(e.Outcomes);
        Assert.Equal(B, e.Outcomes[0].ContentKey);
    }

    [Fact]
    public void Ignore_page_in_one_culture_keeps_the_other_culture()
    {
        var e = Evaluator.Evaluate(TwoUntitled(), [new TitleMissing()], [new IgnoreRule(1, CheckIds.TitleMissing, A, "en")]);
        Assert.Equal(2, e.Outcomes.Count);
        Assert.DoesNotContain(e.Outcomes, o => o.ContentKey == A && o.Culture == "en");
    }

    [Fact]
    public void Ignore_for_other_check_changes_nothing()
        => Assert.Equal(3, Evaluator.Evaluate(TwoUntitled(), [new TitleMissing()], [new IgnoreRule(1, CheckIds.H1Missing, null, null)]).Outcomes.Count);
}
