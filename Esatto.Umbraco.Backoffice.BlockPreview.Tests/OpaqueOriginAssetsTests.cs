using Esatto.Umbraco.Backoffice.BlockPreview.Rendering;
using Xunit;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Tests;

// Final review, Important 7: the preview iframe has an opaque ("null") origin, and fonts and module
// scripts are always fetched in CORS mode — so the site's own /fonts/*.woff2 and module bundles need
// Access-Control-Allow-Origin to load inside the preview.
public class OpaqueOriginAssetsTests
{
    [Theory]
    [InlineData("/fonts/site.woff2")]
    [InlineData("/fonts/site.woff")]
    [InlineData("/fonts/site.ttf")]
    [InlineData("/fonts/site.otf")]
    [InlineData("/js/site.mjs")]
    [InlineData("/js/site.js")]
    public void Allows_fonts_and_scripts_requested_from_the_preview(string path)
    {
        Assert.True(OpaqueOriginAssets.ShouldAllow("GET", "null", path));
    }

    [Fact]
    public void Ignores_requests_from_real_origins()
    {
        Assert.False(OpaqueOriginAssets.ShouldAllow("GET", "https://evil.example", "/fonts/site.woff2"));
        Assert.False(OpaqueOriginAssets.ShouldAllow("GET", null, "/fonts/site.woff2"));
    }

    [Fact]
    public void Ignores_pages_apis_and_writes()
    {
        Assert.False(OpaqueOriginAssets.ShouldAllow("GET", "null", "/"));
        Assert.False(OpaqueOriginAssets.ShouldAllow("GET", "null", "/umbraco/management/api/v1/document/x"));
        Assert.False(OpaqueOriginAssets.ShouldAllow("POST", "null", "/js/site.js"));
    }
}
