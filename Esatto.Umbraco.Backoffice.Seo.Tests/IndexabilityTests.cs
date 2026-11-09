using Esatto.Umbraco.Backoffice.Seo.Checks;
using Esatto.Umbraco.Backoffice.Seo.Evaluation;
using Esatto.Umbraco.Backoffice.Seo.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Seo.Tests;

public class IndexabilityTests
{
    private const string Noindex = """<meta name="robots" content="noindex">""";

    private static PageContext P(ScanSnapshot s, string path) => s.Pages.Single(p => p.Page.Url == Snap.Host + path);

    [Fact]
    public void Meta_noindex_is_excluded()
    {
        var s = Snap.Site().Page("/", root: true).Page("/a", Snap.Good("/a", head: Noindex)).Build();
        Assert.Equal(Indexability.Excluded, P(s, "/a").Indexability);
        Assert.Equal(Indexability.Indexable, P(s, "/").Indexability);
    }

    [Fact]
    public void Header_noindex_is_excluded()
        => Assert.Equal(Indexability.Excluded, P(Snap.Site().Page("/a", xRobots: "noindex").Build(), "/a").Indexability);

    [Fact]
    public void Robots_disallow_is_blocked()
    {
        var s = Snap.Site().Robots("User-agent: *\nDisallow: /a").Page("/a").Build();
        Assert.Equal(Indexability.Blocked, P(s, "/a").Indexability);
        Assert.True(P(s, "/a").BlockedByRobots);
    }

    [Fact]
    public void Excluded_wins_over_blocked_for_the_label()
    {
        var s = Snap.Site().Robots("User-agent: *\nDisallow: /a").Page("/a", Snap.Good("/a", head: Noindex)).Build();
        Assert.Equal(Indexability.Excluded, P(s, "/a").Indexability);
        Assert.True(P(s, "/a").BlockedByRobots);
    }

    private static Snap Pages(int hidden, int visible, bool rootHidden = true)
    {
        var snap = Snap.Site().Page("/", rootHidden ? Snap.Good("/", head: Noindex) : null, root: true);
        for (var i = 0; i < hidden; i++) snap.Page($"/h{i}", Snap.Good($"/h{i}", head: Noindex));
        for (var i = 0; i < visible; i++) snap.Page($"/v{i}");
        return snap;
    }

    [Fact]
    public void Site_hidden_when_root_and_ninety_percent_hidden() => Assert.True(Pages(hidden: 8, visible: 1).Build().SiteHidden); // 9 of 10

    [Fact]
    public void Not_site_hidden_below_ninety_percent() => Assert.False(Pages(hidden: 7, visible: 2).Build().SiteHidden); // 8 of 10

    [Fact]
    public void Not_site_hidden_when_root_visible() => Assert.False(Pages(hidden: 20, visible: 0, rootHidden: false).Build().SiteHidden);

    [Fact]
    public void Excluded_page_content_checks_not_applicable()
    {
        var s = Snap.Site().Page("/", root: true).Page("/a", Snap.Good("/a", head: Noindex, title: "x")).Build();
        Assert.False(s.ContentChecksApply(P(s, "/a")));
        Assert.False(Snap.Applies(new TitleLength(), s, "/a"));
    }

    [Fact]
    public void Site_hidden_content_checks_apply_to_hidden_pages()
    {
        var s = Pages(hidden: 9, visible: 0).Build();
        Assert.True(s.SiteHidden);
        Assert.True(s.ContentChecksApply(P(s, "/h1")));
    }

    [Fact]
    public void Failed_page_content_checks_not_applicable()
    {
        var s = Snap.Site().Page("/a", status: 404).Build();
        Assert.False(s.ContentChecksApply(P(s, "/a")));
    }
}
