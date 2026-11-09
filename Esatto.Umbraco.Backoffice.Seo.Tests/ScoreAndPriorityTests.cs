using Esatto.Umbraco.Backoffice.Seo.Checks;
using Esatto.Umbraco.Backoffice.Seo.Evaluation;
using Esatto.Umbraco.Backoffice.Seo.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Seo.Tests;

public class ScoreCalculatorTests
{
    private const string Noindex = """<meta name="robots" content="noindex">""";

    private static int Score(ScanSnapshot s, IReadOnlyList<IgnoreRule>? ignores = null)
        => ScoreCalculator.Calculate(s, Evaluator.Evaluate(s, CheckCatalog.All, ignores ?? []), CheckCatalog.Info);

    private static Snap Healthy() => Snap.Site().Robots("User-agent: *\nDisallow:").Sitemap("/", "/a").Page("/", root: true).Page("/a");

    [Fact]
    public void All_pass_is_100() => Assert.Equal(100, Score(Healthy().Build()));

    [Fact]
    public void No_indexable_pages_scores_zero()
    {
        // Root visible-but-404, every other page noindex: nothing can be found.
        var s = Snap.Site().Page("/", root: true, status: 404).Page("/a", Snap.Good("/a", head: Noindex)).Build();
        Assert.False(s.SiteHidden);
        Assert.Equal(0, Score(Snap.Site().Page("/a", Snap.Good("/a", head: Noindex)).Build()));
    }

    [Fact]
    public void Critical_failure_zeroes_the_page()
    {
        // Two pages, one without a title (Critical) → average 50; site factor 1.
        var s = Healthy().Page("/b", Snap.Good("/b", title: " ")).Sitemap("/", "/a", "/b").Build();
        Assert.Equal(67, Score(s)); // (100 + 100 + 0) / 3
    }

    [Fact]
    public void Site_factor_from_https_missing()
    {
        // Site outcomes: robots 1 + sitemap 3 + https 10 (fail) + host 3 + home 10 + site-hidden 10 → 27/37.
        var s = Healthy().Https(false).Build();
        Assert.Equal(73, Score(s));
    }

    [Fact]
    public void Intentionally_hidden_site_has_no_site_penalty()
    {
        var hidden = Snap.Site().Robots("User-agent: *\nDisallow:").Sitemap("/", "/a")
            .Page("/", Snap.Good("/", head: Noindex), root: true).Page("/a", Snap.Good("/a", head: Noindex));
        var penalised = Score(hidden.Build());
        var intentional = Score(hidden.IntentionallyHidden().Build());
        Assert.True(penalised < intentional, $"{penalised} vs {intentional}");
        Assert.True(intentional > 0);
    }

    [Fact]
    public void Ignored_failure_is_not_counted()
    {
        // Only the <title> is empty; og:title stays, so title-missing is the page's one failure.
        var html = System.Text.RegularExpressions.Regex.Replace(Snap.Good("/b"), "<title>[^<]*</title>", "<title></title>");
        var s = Healthy().Page("/b", html).Sitemap("/", "/a", "/b").Build();
        var key = s.Pages.Single(p => p.Page.Url.EndsWith("/b")).Page.ContentKey;
        Assert.True(Score(s) < 100);
        Assert.Equal(100, Score(s, [new IgnoreRule(1, CheckIds.TitleMissing, key, null)]));
    }
}

public class PriorityTests
{
    [Fact]
    public void Critical_on_one_page_beats_notice_on_fifty()
    {
        var snap = Snap.Site().Page("/x", Snap.Good("/x", title: " "));
        for (var i = 0; i < 50; i++) snap.Page($"/p{i}", Snap.Good($"/p{i}", body: "<p>few words</p>"));
        var s = snap.Build();
        var ranked = Priority.Rank(Evaluator.Evaluate(s, CheckCatalog.All, []), CheckCatalog.Info);

        Assert.Equal(CheckIds.TitleMissing, ranked[0].CheckId);
        var thin = ranked.Single(r => r.CheckId == CheckIds.ThinContent);
        Assert.Equal(50, thin.Pages);
        Assert.Equal(50, thin.Priority);
        Assert.Equal(Severity.Notice, thin.Severity);
        Assert.Equal(100, ranked[0].Priority);
    }

    [Fact]
    public void Site_issue_counts_as_one()
    {
        var s = Snap.Site().Https(false).Page("/", root: true).Build();
        var https = Priority.Rank(Evaluator.Evaluate(s, CheckCatalog.All, []), CheckCatalog.Info).Single(r => r.CheckId == CheckIds.HttpsRedirect);
        Assert.Equal(0, https.Pages);
        Assert.Equal(100, https.Priority);
    }
}
