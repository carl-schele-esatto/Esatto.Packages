using System.Reflection;
using Esatto.Umbraco.Backoffice.Seo.Checks;
using Esatto.Umbraco.Backoffice.Seo.Evaluation;
using Esatto.Umbraco.Backoffice.Seo.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Seo.Tests;

public class CrossPageChecksTests
{
    private const string Noindex = """<meta name="robots" content="noindex">""";
    private static readonly string SameTitle = "Same title for both of these pages here";

    [Fact]
    public void Title_duplicate_within_same_culture_only()
    {
        var s = Snap.Site("sv", "en")
            .Page("/a", Snap.Good("/a", "sv", title: SameTitle), culture: "sv")
            .Page("/b", Snap.Good("/b", "sv", title: SameTitle), culture: "sv")
            .Page("/en/a", Snap.Good("/en/a", title: SameTitle), culture: "en")
            .Build();
        var a = Snap.For(new TitleDuplicate(), s, "/a");
        Assert.False(a.Passed);
        Assert.Equal($"\"{SameTitle}\" (2 pages)", a.Value);
        Assert.True(Snap.For(new TitleDuplicate(), s, "/en/a").Passed);
    }

    [Fact]
    public void Title_duplicate_ignores_hidden_pages()
    {
        var s = Snap.Site()
            .Page("/", root: true)
            .Page("/a", Snap.Good("/a", title: SameTitle))
            .Page("/b", Snap.Good("/b", head: Noindex, title: SameTitle))
            .Build();
        Assert.True(Snap.For(new TitleDuplicate(), s, "/a").Passed);
        Assert.False(Snap.Applies(new TitleDuplicate(), s, "/b"));
    }

    [Fact]
    public void Description_duplicate()
    {
        var d = new string('d', 100);
        var s = Snap.Site().Page("/a", Snap.Good("/a", description: d)).Page("/b", Snap.Good("/b", description: d)).Build();
        Assert.False(Snap.For(new DescriptionDuplicate(), s, "/b").Passed);
    }

    private static string Alt(params (string Lang, string Path)[] links)
        => string.Concat(links.Select(l => $"""<link rel="alternate" hreflang="{l.Lang}" href="{Snap.Host}{l.Path}">"""));

    [Fact]
    public void Hreflang_reciprocal_across_trees_passes()
    {
        var s = Snap.Site("sv", "en")
            .Page("/sv/om", Snap.Good("/sv/om", "sv", head: Alt(("sv", "/sv/om"), ("en", "/en/about"))), culture: "sv")
            .Page("/en/about", Snap.Good("/en/about", head: Alt(("sv", "/sv/om"), ("en", "/en/about"))), culture: "en")
            .Build();
        Assert.True(Snap.For(new HreflangInvalid(), s, "/sv/om").Passed);
        Assert.True(Snap.For(new HreflangInvalid(), s, "/en/about").Passed);
    }

    [Fact]
    public void Hreflang_not_reciprocal_fails()
    {
        var s = Snap.Site("sv", "en")
            .Page("/sv/om", Snap.Good("/sv/om", "sv", head: Alt(("sv", "/sv/om"), ("en", "/en/about"))), culture: "sv")
            .Page("/en/about", Snap.Good("/en/about", head: Alt(("en", "/en/about"))), culture: "en")
            .Build();
        var o = Snap.For(new HreflangInvalid(), s, "/sv/om");
        Assert.False(o.Passed);
        Assert.Contains("doesn't link back", o.Value);
    }

    [Fact]
    public void Hreflang_bad_code_and_missing_self_fail()
    {
        var s = Snap.Site().Page("/a", Snap.Good("/a", head: Alt(("en_GB", "/b")))).Page("/b").Build();
        var o = Snap.For(new HreflangInvalid(), s, "/a");
        Assert.False(o.Passed);
        Assert.Contains("en_GB", o.Value);
        Assert.Contains("doesn't list the page itself", o.Value);
    }

    [Fact]
    public void Hreflang_target_outside_scan_uses_link_status()
    {
        var head = Alt(("en", "/a"), ("sv", "/other-root/a"));
        Assert.True(Snap.For(new HreflangInvalid(), Snap.Site().Page("/a", Snap.Good("/a", head: head)).Link("/other-root/a", 200).Build(), "/a").Passed);
        var broken = Snap.For(new HreflangInvalid(), Snap.Site().Page("/a", Snap.Good("/a", head: head)).Link("/other-root/a", 404).Build(), "/a");
        Assert.False(broken.Passed);
        Assert.Contains("doesn't load", broken.Value);
    }

    [Fact]
    public void Sitemap_missing_page_not_applicable_without_sitemap()
        => Assert.Empty(Snap.Run(new SitemapMissingPage(), Snap.Site().Page("/a").Build()));

    [Fact]
    public void Sitemap_missing_page()
    {
        var s = Snap.Site().Page("/a").Page("/b").Page("/c", Snap.Good("/c", head: Noindex)).Sitemap("/a/").Build();
        Assert.True(Snap.For(new SitemapMissingPage(), s, "/a").Passed);
        Assert.False(Snap.For(new SitemapMissingPage(), s, "/b").Passed);
        Assert.False(Snap.Applies(new SitemapMissingPage(), s, "/c"));
    }

    [Fact]
    public void Sitemap_bad_entry_for_noindex_404_and_unknown_urls()
    {
        var s = Snap.Site().Page("/", root: true).Page("/a").Page("/hidden", Snap.Good("/hidden", head: Noindex)).Page("/gone", status: 404)
            .Sitemap("/", "/a", "/hidden", "/gone", "/unknown").Build();
        var outcomes = Snap.Run(new SitemapBadEntry(), s);
        Assert.Equal(5, outcomes.Count);
        Assert.Equal(["https://esatto.se/gone", "https://esatto.se/hidden", "https://esatto.se/unknown"],
            outcomes.Where(o => !o.Passed).Select(o => o.Url).Order());
        Assert.Null(outcomes.Single(o => o.Url!.EndsWith("/unknown")).ContentKey);
    }

    [Fact]
    public void Sitemap_bad_entry_not_applicable_when_site_hidden()
    {
        var s = Snap.Site().Page("/", Snap.Good("/", head: Noindex), root: true).Sitemap("/").Build();
        Assert.True(s.SiteHidden);
        Assert.Empty(Snap.Run(new SitemapBadEntry(), s));
    }
}

public class SiteChecksTests
{
    private const string Noindex = """<meta name="robots" content="noindex">""";

    private static Outcome? Site(ICheck check, ScanSnapshot s) => Snap.Run(check, s).SingleOrDefault();

    [Fact]
    public void Site_outcomes_have_no_page()
    {
        var o = Site(new SitemapMissing(), Snap.Site().Page("/", root: true).Build())!;
        Assert.Null(o.ContentKey);
        Assert.False(o.Passed);
    }

    [Fact]
    public void Robots_missing() => Assert.False(Site(new RobotsMissing(), Snap.Site().Robots(null, found: false).Page("/").Build())!.Passed);

    [Fact]
    public void Https_and_host_checks()
    {
        Assert.False(Site(new HttpsRedirect(), Snap.Site().Https(false).Page("/").Build())!.Passed);
        Assert.True(Site(new HttpsRedirect(), Snap.Site().Https(true).Page("/").Build())!.Passed);
        Assert.False(Site(new HostDuplicate(), Snap.Site().HostDuplicate(true).Page("/").Build())!.Passed);
    }

    [Fact]
    public void Localhost_https_and_host_not_applicable()
    {
        var s = Snap.Site().Https(null).HostDuplicate(null).Page("/").Build();
        Assert.Null(Site(new HttpsRedirect(), s));
        Assert.Null(Site(new HostDuplicate(), s));
    }

    [Fact]
    public void Home_hidden()
    {
        var s = Snap.Site().Page("/", Snap.Good("/", head: Noindex), root: true).Page("/a").Page("/b").Build();
        Assert.False(s.SiteHidden);
        Assert.False(Site(new HomeHidden(), s)!.Passed);
        Assert.True(Site(new HomeHidden(), Snap.Site().Page("/", root: true).Build())!.Passed);
    }

    [Fact]
    public void Home_hidden_not_raised_when_site_hidden()
    {
        var s = Snap.Site().Page("/", Snap.Good("/", head: Noindex), root: true).Build();
        Assert.Null(Site(new HomeHidden(), s));
        Assert.False(Site(new SiteHidden(), s)!.Passed);
    }

    [Fact]
    public void Site_hidden_intentionally_is_not_applicable()
        => Assert.Null(Site(new SiteHidden(), Snap.Site().IntentionallyHidden().Page("/", Snap.Good("/", head: Noindex), root: true).Build()));
}

public class CheckCatalogTests
{
    [Fact]
    public void Catalog_has_every_check_id_once()
    {
        var ids = typeof(CheckIds).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (string)f.GetRawConstantValue()!).Order().ToList();
        Assert.Equal(ids, CheckCatalog.All.Select(c => c.Info.Id).Order().ToList());
    }

    [Fact]
    public void Scopes_are_set()
    {
        Assert.Equal(CheckScope.Site, CheckCatalog.Info[CheckIds.HttpsRedirect].Scope);
        Assert.Equal(CheckScope.Site, CheckCatalog.Info[CheckIds.HomeHidden].Scope);
        Assert.Equal(CheckScope.CrossPage, CheckCatalog.Info[CheckIds.TitleDuplicate].Scope);
        Assert.Equal(CheckScope.Page, CheckCatalog.Info[CheckIds.TitleLength].Scope);
    }
}
