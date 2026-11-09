using Esatto.Umbraco.Backoffice.Seo.Facts;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Seo.Tests;

public class RobotsTxtTests
{
    [Fact]
    public void Disallow_all_blocks_everything()
    {
        var r = RobotsTxt.Parse("User-agent: *\nDisallow: /");
        Assert.False(r.IsAllowed("/"));
        Assert.False(r.IsAllowed("/om"));
        Assert.True(r.DisallowsAll);
    }

    [Fact]
    public void Empty_or_null_allows_all()
    {
        Assert.True(RobotsTxt.Parse(null).IsAllowed("/x"));
        Assert.True(RobotsTxt.Parse("").IsAllowed("/x"));
        Assert.False(RobotsTxt.Parse("").DisallowsAll);
    }

    [Fact]
    public void Empty_disallow_allows_all() => Assert.True(RobotsTxt.Parse("User-agent: *\nDisallow:").IsAllowed("/x"));

    [Fact]
    public void Googlebot_group_wins_over_star()
    {
        var r = RobotsTxt.Parse("User-agent: *\nDisallow: /\n\nUser-agent: Googlebot\nDisallow: /private");
        Assert.True(r.IsAllowed("/om"));
        Assert.False(r.IsAllowed("/private/x"));
        Assert.False(r.DisallowsAll);
    }

    [Fact]
    public void Grouped_user_agents_share_rules()
    {
        var r = RobotsTxt.Parse("User-agent: bingbot\nUser-agent: googlebot\nDisallow: /a");
        Assert.False(r.IsAllowed("/a"));
    }

    [Fact]
    public void Longest_match_wins_and_allow_beats_disallow_on_tie()
    {
        var r = RobotsTxt.Parse("User-agent: *\nDisallow: /shop\nAllow: /shop/public\nDisallow: /x\nAllow: /x");
        Assert.False(r.IsAllowed("/shop/cart"));
        Assert.True(r.IsAllowed("/shop/public/a"));
        Assert.True(r.IsAllowed("/x"));
    }

    [Fact]
    public void Wildcards_and_end_anchor()
    {
        var r = RobotsTxt.Parse("User-agent: *\nDisallow: /*.pdf$\nDisallow: /search*q=");
        Assert.False(r.IsAllowed("/files/a.pdf"));
        Assert.True(r.IsAllowed("/files/a.pdf?x=1"));
        Assert.False(r.IsAllowed("/search?q=seo"));
    }

    [Fact]
    public void Comments_and_sitemaps()
    {
        var r = RobotsTxt.Parse("# hello\nUser-agent: * # all\nDisallow: /tmp\nSitemap: https://esatto.se/sitemap.xml\nsitemap: https://cdn.esatto.se/s2.xml");
        Assert.False(r.IsAllowed("/tmp"));
        Assert.Equal(["https://esatto.se/sitemap.xml", "https://cdn.esatto.se/s2.xml"], r.Sitemaps);
    }
}

public class SitemapReaderTests
{
    [Fact]
    public void Urlset_with_namespace()
    {
        var doc = SitemapReader.Parse("""
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <url><loc> https://esatto.se/ </loc></url>
              <url><loc>https://esatto.se/om</loc><lastmod>2026-01-01</lastmod></url>
            </urlset>
            """);
        Assert.False(doc.IsIndex);
        Assert.Equal(["https://esatto.se/", "https://esatto.se/om"], doc.Locations);
    }

    [Fact]
    public void Urlset_without_namespace() => Assert.Single(SitemapReader.Parse("<urlset><url><loc>https://a.se/</loc></url></urlset>").Locations);

    [Fact]
    public void Index()
    {
        var doc = SitemapReader.Parse("""<sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"><sitemap><loc>https://esatto.se/s1.xml</loc></sitemap></sitemapindex>""");
        Assert.True(doc.IsIndex);
        Assert.Equal(["https://esatto.se/s1.xml"], doc.Locations);
    }

    [Theory]
    [InlineData("<html><body>Not found</body></html>")]
    [InlineData("<urlset><url><loc>")]
    [InlineData("")]
    public void Invalid_or_not_a_sitemap_is_empty(string xml)
    {
        var doc = SitemapReader.Parse(xml);
        Assert.False(doc.IsIndex);
        Assert.Empty(doc.Locations);
    }
}
