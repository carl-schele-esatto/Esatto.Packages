using Esatto.Umbraco.Backoffice.Seo.Checks;
using Esatto.Umbraco.Backoffice.Seo.Evaluation;
using Esatto.Umbraco.Backoffice.Seo.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Seo.Tests;

public class PageChecksTests
{
    private static Outcome One(ICheck check, string html, string path = "/a")
        => Snap.For(check, Snap.Site().Page(path, html).Build(), path);

    private static Outcome One(ICheck check, Snap snap, string path = "/a") => Snap.For(check, snap.Build(), path);

    [Fact]
    public void Good_page_passes_every_page_check()
    {
        var s = Snap.Site().Page("/a").Link("/a", 200).Build();
        var checks = new ICheck[]
        {
            new TitleMissing(), new TitleLength(), new DescriptionMissing(), new DescriptionLength(), new H1Missing(), new H1Multiple(),
            new ThinContent(), new OgMissing(), new BrokenInternalLink(), new RedirectedInternalLink(), new StatusError(),
            new HiddenButBlocked(), new CanonicalMissing(), new CanonicalMismatch(), new LangMismatch(), new SlowResponse(), new HeavyHtml(),
        };
        foreach (var check in checks)
            Assert.All(Snap.Run(check, s), o => Assert.True(o.Passed, $"{check.Info.Id}: {o.Value}"));
    }

    [Fact]
    public void Title_missing_when_empty() => Assert.False(One(new TitleMissing(), Snap.Good("/a", title: "  ")).Passed);

    [Theory]
    [InlineData(29, false)]
    [InlineData(30, true)]
    [InlineData(60, true)]
    [InlineData(61, false)]
    public void Title_length(int length, bool passes)
    {
        var o = One(new TitleLength(), Snap.Good("/a", title: new string('t', length)));
        Assert.Equal(passes, o.Passed);
        if (!passes) Assert.Contains($"{length} characters", o.Value);
    }

    [Fact]
    public void Title_length_not_applicable_without_title()
        => Assert.False(Snap.Applies(new TitleLength(), Snap.Site().Page("/a", "<html><body><main>x</main></body></html>").Build(), "/a"));

    [Fact]
    public void Description_missing() => Assert.False(One(new DescriptionMissing(), "<html><head><title>t</title></head></html>").Passed);

    [Theory]
    [InlineData(69, false)]
    [InlineData(70, true)]
    [InlineData(160, true)]
    [InlineData(161, false)]
    public void Description_length(int length, bool passes)
        => Assert.Equal(passes, One(new DescriptionLength(), Snap.Good("/a", description: new string('d', length))).Passed);

    [Fact]
    public void H1_missing_when_only_empty() => Assert.False(One(new H1Missing(), Snap.Good("/a").Replace("<h1>Heading</h1>", "<h1> </h1>")).Passed);

    [Fact]
    public void H1_multiple()
    {
        var o = One(new H1Multiple(), Snap.Good("/a", body: "<h1>Second</h1><p>x</p>"));
        Assert.False(o.Passed);
        Assert.Contains("Second", o.Value);
    }

    [Theory]
    [InlineData(199, false)]
    [InlineData(200, true)]
    public void Thin_content(int words, bool passes)
        => Assert.Equal(passes, One(new ThinContent(), Snap.Good("/a", body: "<p>" + string.Join(' ', Enumerable.Repeat("w", words - 1)) + "</p>")).Passed); // + "Heading"

    [Fact]
    public void Og_missing_names_what_is_missing()
    {
        var o = One(new OgMissing(), Snap.Good("/a").Replace("<meta property=\"og:image\" content=\"/a.jpg\">", ""));
        Assert.False(o.Passed);
        Assert.Contains("og:image", o.Value);
    }

    [Fact]
    public void Broken_internal_link_lists_urls_and_applies_to_hidden_pages()
    {
        var html = Snap.Good("/a", head: """<meta name="robots" content="noindex">""", body: """<a href="/gone">x</a><a href="/ok">y</a>""");
        var o = One(new BrokenInternalLink(), Snap.Site().Page("/a", html).Link("/gone", 404).Link("/ok", 200));
        Assert.False(o.Passed);
        Assert.Equal("https://esatto.se/gone", o.Value);
    }

    [Fact]
    public void Redirected_internal_link()
    {
        var html = Snap.Good("/a", body: """<a href="/old">x</a>""");
        Assert.False(One(new RedirectedInternalLink(), Snap.Site().Page("/a", html).Link("/old", 200, hops: 1)).Passed);
        Assert.True(One(new BrokenInternalLink(), Snap.Site().Page("/a", html).Link("/old", 200, hops: 1)).Passed);
    }

    [Fact]
    public void Translation_missing_on_variant_sites_only()
    {
        var s = Snap.Site("sv", "en").Page("/a", culture: "sv", variant: true).Page("/b", culture: "sv").Build();
        var o = Snap.For(new TranslationMissing(), s, "/a");
        Assert.False(o.Passed);
        Assert.Equal("en", o.Value);
        Assert.False(Snap.Applies(new TranslationMissing(), s, "/b"));
        Assert.True(Snap.For(new TranslationMissing(), Snap.Site("sv", "en").Page("/a", culture: "sv", variant: true, others: ["EN"]).Build(), "/a").Passed);
    }

    [Fact]
    public void Status_error_404_fails_with_status()
    {
        var o = One(new StatusError(), Snap.Site().Page("/a", status: 404));
        Assert.False(o.Passed);
        Assert.Contains("404", o.Value);
    }

    [Fact]
    public void Status_error_on_timeout_and_non_html()
    {
        Assert.Contains("timeout", One(new StatusError(), Snap.Site().Page("/a", status: 0, error: "timeout")).Value);
        Assert.Contains("Not an HTML page", One(new StatusError(), Snap.Site().Page("/a", contentType: "application/pdf")).Value);
    }

    [Fact]
    public void Hidden_but_blocked()
    {
        var html = Snap.Good("/a", head: """<meta name="robots" content="noindex">""");
        Assert.False(One(new HiddenButBlocked(), Snap.Site().Robots("User-agent: *\nDisallow: /a").Page("/a", html)).Passed);
        Assert.True(One(new HiddenButBlocked(), Snap.Site().Page("/a", html)).Passed);
    }

    [Fact]
    public void Canonical_missing() => Assert.False(One(new CanonicalMissing(), Snap.Good("/a").Replace("rel=\"canonical\"", "rel=\"x\"")).Passed);

    [Fact]
    public void Canonical_trailing_slash_difference_passes()
        => Assert.True(One(new CanonicalMismatch(), Snap.Good("/a").Replace("href=\"https://esatto.se/a\"", "href=\"https://ESATTO.se/a/\"")).Passed);

    [Fact]
    public void Canonical_to_other_page_fails()
    {
        var o = One(new CanonicalMismatch(), Snap.Good("/a").Replace("href=\"https://esatto.se/a\"", "href=\"https://esatto.se/b\""));
        Assert.False(o.Passed);
        Assert.Contains("https://esatto.se/b", o.Value);
    }

    [Fact]
    public void Hreflang_missing_only_when_node_has_other_cultures()
    {
        var s = Snap.Site("sv", "en").Page("/a", culture: "sv", variant: true, others: ["en"]).Page("/b", culture: "sv", variant: true).Build();
        Assert.False(Snap.For(new HreflangMissing(), s, "/a").Passed);
        Assert.False(Snap.Applies(new HreflangMissing(), s, "/b"));
    }

    [Fact]
    public void Hreflang_x_default()
    {
        var with = Snap.Good("/a", head: """<link rel="alternate" hreflang="en" href="/a"><link rel="alternate" hreflang="x-default" href="/a">""");
        var without = Snap.Good("/a", head: """<link rel="alternate" hreflang="en" href="/a">""");
        Assert.True(One(new HreflangNoXDefault(), with).Passed);
        Assert.False(One(new HreflangNoXDefault(), without).Passed);
        Assert.False(Snap.Applies(new HreflangNoXDefault(), Snap.Site().Page("/a").Build(), "/a"));
    }

    [Theory]
    [InlineData("sv-SE", "sv", true)]
    [InlineData("en", "en-US", true)]
    [InlineData("en", "sv", false)]
    public void Lang_mismatch(string htmlLang, string culture, bool passes)
        => Assert.Equal(passes, One(new LangMismatch(), Snap.Site().Page("/a", Snap.Good("/a").Replace("lang=\"en\"", $"lang=\"{htmlLang}\""), culture: culture)).Passed);

    [Fact]
    public void Lang_missing_fails_even_for_invariant()
        => Assert.False(One(new LangMismatch(), Snap.Site().Page("/a", Snap.Good("/a").Replace(" lang=\"en\"", ""), culture: "")).Passed);

    [Fact]
    public void Structured_data_invalid()
    {
        Assert.False(One(new StructuredDataInvalid(), Snap.Good("/a", head: """<script type="application/ld+json">{ nope</script>""")).Passed);
        Assert.False(Snap.Applies(new StructuredDataInvalid(), Snap.Site().Page("/a").Build(), "/a"));
    }

    [Theory]
    [InlineData(1500, true)]
    [InlineData(1501, false)]
    public void Slow_response(long ms, bool passes) => Assert.Equal(passes, One(new SlowResponse(), Snap.Site().Page("/a", ms: ms)).Passed);

    [Theory]
    [InlineData(512_000, true)]
    [InlineData(512_001, false)]
    public void Heavy_html(long bytes, bool passes) => Assert.Equal(passes, One(new HeavyHtml(), Snap.Site().Page("/a", bytes: bytes)).Passed);
}
