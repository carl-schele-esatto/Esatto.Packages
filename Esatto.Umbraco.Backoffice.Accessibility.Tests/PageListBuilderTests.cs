using Esatto.Umbraco.Backoffice.Accessibility.Pages;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Accessibility.Tests;

public class PageListBuilderTests
{
    private static PageCandidate Page(string url, bool hasTemplate = true, bool isProtected = false)
        => new(Guid.NewGuid(), "Page", "sv", url, isProtected, hasTemplate);

    [Fact]
    public void Pages_without_template_are_dropped()
    {
        var pages = PageListBuilder.Build([Page("https://a.se/x", hasTemplate: false), Page("https://a.se/y")], []);
        Assert.Equal(["https://a.se/y"], pages.Select(p => p.Url));
    }

    [Fact]
    public void Excluded_prefix_matches_case_insensitively_and_not_partial_segments()
    {
        var pages = PageListBuilder.Build(
            [Page("https://a.se/Sok/x"), Page("https://a.se/sok"), Page("https://a.se/sokresultat")],
            ["/sok/"]);

        Assert.Equal("excluded", pages.Single(p => p.Url.EndsWith("/Sok/x")).SkipReason);
        Assert.Equal("excluded", pages.Single(p => p.Url.EndsWith("/sok")).SkipReason);
        Assert.Null(pages.Single(p => p.Url.EndsWith("/sokresultat")).SkipReason);
    }

    [Fact]
    public void Protected_pages_are_kept_with_skip_reason_protected()
        => Assert.Equal("protected", PageListBuilder.Build([Page("https://a.se/members", isProtected: true)], []).Single().SkipReason);

    [Fact]
    public void Page_without_url_is_kept_with_skip_reason_no_url()
    {
        var page = PageListBuilder.Build([new PageCandidate(Guid.NewGuid(), "Page", "sv", null, false, true)], []).Single();
        Assert.Equal("no-url", page.SkipReason);
        Assert.Equal(string.Empty, page.Url);
    }
}
