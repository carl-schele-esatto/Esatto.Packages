using Esatto.Umbraco.Backoffice.Seo.Facts;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Seo.Tests;

public class UrlNormalizerTests
{
    [Theory]
    [InlineData("HTTPS://Esatto.se:443/om/#x", "https://esatto.se/om")]
    [InlineData("https://esatto.se/om", "https://esatto.se/om")]
    [InlineData("https://esatto.se", "https://esatto.se/")]
    [InlineData("https://esatto.se/", "https://esatto.se/")]
    [InlineData("http://esatto.se:80/a/", "http://esatto.se/a")]
    [InlineData("https://esatto.se:8443/a", "https://esatto.se:8443/a")]
    [InlineData("https://esatto.se/a/?q=1", "https://esatto.se/a?q=1")]
    [InlineData("https://esatto.se/Om", "https://esatto.se/Om")]
    public void Normalize(string input, string expected) => Assert.Equal(expected, UrlNormalizer.Normalize(input));

    [Fact]
    public void Normalize_returns_input_when_not_absolute() => Assert.Equal("/om", UrlNormalizer.Normalize("/om"));

    [Theory]
    [InlineData("mailto:a@b.se")]
    [InlineData("tel:+4612345")]
    [InlineData("javascript:void(0)")]
    [InlineData("#top")]
    [InlineData("")]
    [InlineData(null)]
    public void TryResolve_rejects_non_page_links(string? href)
        => Assert.False(UrlNormalizer.TryResolve(new Uri("https://esatto.se/om/"), href, out _));

    [Fact]
    public void TryResolve_makes_relative_absolute()
    {
        Assert.True(UrlNormalizer.TryResolve(new Uri("https://esatto.se/om/"), "../kontakt", out var abs));
        Assert.Equal("https://esatto.se/kontakt", abs.ToString());
    }
}
