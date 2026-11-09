using Esatto.Umbraco.Backoffice.Seo.Facts;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Seo.Tests;

public class PageParserTests
{
    private static readonly IReadOnlySet<string> Hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "esatto.se" };

    internal static FetchResult Html(string html, string url = "https://esatto.se/om", string? xRobots = null, string contentType = "text/html; charset=utf-8")
        => new(url, url, 200, [], contentType, xRobots, html, html.Length, 100, null);

    private static PageFacts Parse(string html, string? xRobots = null) => PageParser.Parse(Html(html, xRobots: xRobots), Hosts);

    private const string Full = """
        <!doctype html><html lang="sv-SE"><head>
        <title>  Om   oss
          | Esatto </title>
        <meta name="description" content=" Vi bygger webb. ">
        <link rel="canonical" href="https://esatto.se/om/">
        <link rel="alternate" hreflang="sv" href="https://esatto.se/om">
        <link rel="alternate" hreflang="en" href="/en/about">
        <meta property="og:title" content="Om oss">
        <meta property="og:image" content="/media/a.jpg">
        <script type="application/ld+json">{"@type":"Organization"}</script>
        <script type="application/ld+json">{ broken</script>
        </head><body>
        <header><nav>Hem Om Kontakt Meny ord ord ord</nav></header>
        <main><h1> Om  oss </h1><p>Ett två tre fyra fem.</p>
          <a href="/kontakt">k</a><a href="https://ESATTO.se/kontakt/">k2</a>
          <a href="https://other.se/x">ext</a><a href="mailto:a@b.se">m</a></main>
        <footer>Sidfot ord ord</footer>
        </body></html>
        """;

    [Fact]
    public void Extracts_head_facts()
    {
        var f = Parse(Full);
        Assert.True(f.IsHtml);
        Assert.Equal("Om oss | Esatto", f.Title);
        Assert.Equal("Vi bygger webb.", f.MetaDescription);
        Assert.Equal(["Om oss"], f.H1s);
        Assert.Equal("https://esatto.se/om/", f.Canonical);
        Assert.Equal("sv-SE", f.HtmlLang);
        Assert.Equal("Om oss", f.OgTitle);
        Assert.Equal("https://esatto.se/media/a.jpg", f.OgImage);
        Assert.Equal(2, f.Hreflangs.Count);
        Assert.Equal(new HreflangLink("en", "https://esatto.se/en/about"), f.Hreflangs[1]);
    }

    [Fact]
    public void WordCount_counts_main_only() => Assert.Equal(11, Parse(Full).WordCount); // h1 2 + paragraph 5 + four link texts

    [Fact]
    public void WordCount_without_main_ignores_header_nav_footer_and_scripts()
        => Assert.Equal(3, Parse("<body><nav>a b c</nav><p>one two three</p><script>var x = 1;</script><footer>d e</footer></body>").WordCount);

    [Fact]
    public void JsonLd_invalid_is_counted()
    {
        var f = Parse(Full);
        Assert.Equal(2, f.JsonLdTotal);
        Assert.Equal(1, f.JsonLdInvalid);
    }

    [Fact]
    public void InternalLinks_only_site_hosts_normalised_and_distinct()
        => Assert.Equal(["https://esatto.se/kontakt"], Parse(Full).InternalLinks);

    [Theory]
    [InlineData("<meta name=\"robots\" content=\"noindex, follow\">")]
    [InlineData("<meta name=\"robots\" content=\"NONE\">")]
    [InlineData("<meta name=\"googlebot\" content=\"noindex\">")]
    public void Noindex_from_meta(string meta) => Assert.True(Parse($"<head>{meta}</head>").MetaNoindex);

    [Fact]
    public void Index_follow_is_not_noindex() => Assert.False(Parse("<meta name=\"robots\" content=\"index, follow\">").MetaNoindex);

    [Theory]
    [InlineData("noindex, nofollow", true)]
    [InlineData("googlebot: noindex", true)]
    [InlineData("otherbot: noindex", false)]
    [InlineData("nofollow", false)]
    public void Noindex_from_header(string header, bool expected) => Assert.Equal(expected, Parse("<p>x</p>", header).HeaderNoindex);

    [Fact]
    public void NonHtml_pdf_is_not_html_and_does_not_throw()
    {
        var f = PageParser.Parse(Html("%PDF-1.4", contentType: "application/pdf"), Hosts);
        Assert.False(f.IsHtml);
        Assert.Null(f.Title);
    }

    [Fact]
    public void Empty_body_does_not_throw()
    {
        var f = PageParser.Parse(Html(""), Hosts);
        Assert.True(f.IsHtml);
        Assert.Null(f.Title);
        Assert.Equal(0, f.WordCount);
    }

    [Fact]
    public void Failed_fetch_keeps_status_and_error()
    {
        var r = new FetchResult("https://esatto.se/x", "https://esatto.se/x", 0, [], null, null, null, 0, 30000, "timeout");
        var f = PageParser.Parse(r, Hosts);
        Assert.Equal(0, f.StatusCode);
        Assert.Equal("timeout", f.Error);
        Assert.False(f.IsHtml);
    }
}
