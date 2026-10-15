using Esatto.Umbraco.Backoffice.BlockPreview;
using Esatto.Umbraco.Backoffice.BlockPreview.Manifest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Manifest;
using Umbraco.Cms.Infrastructure.Manifest;
using Xunit;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Tests;

public class BlockPreviewRegistrationTests
{
    private sealed class FirstUmbracoReader : IPackageManifestReader
    {
        public Task<IEnumerable<PackageManifest>> ReadPackageManifestsAsync() => Task.FromResult(Enumerable.Empty<PackageManifest>());
    }

    private sealed class LastUmbracoReader : IPackageManifestReader
    {
        public Task<IEnumerable<PackageManifest>> ReadPackageManifestsAsync() => Task.FromResult(Enumerable.Empty<PackageManifest>());
    }

    private static ServiceProvider Build(Dictionary<string, string?>? config = null, int registrations = 1)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(config ?? []).Build());
        services.AddSingleton<IPackageManifestReader, FirstUmbracoReader>();
        services.AddSingleton<IPackageManifestReader, LastUmbracoReader>();
        for (var i = 0; i < registrations; i++) services.AddBlockPreview();
        return services.BuildServiceProvider();
    }

    // PackagingService resolves ONE IPackageManifestReader (the last registered); ours must not become it.
    [Fact]
    public void Single_resolution_stays_umbracos_last_reader()
    {
        using var provider = Build();
        Assert.IsType<LastUmbracoReader>(provider.GetRequiredService<IPackageManifestReader>());
    }

    [Fact]
    public void Registering_twice_adds_the_reader_once()
    {
        using var provider = Build(registrations: 2);
        Assert.Single(provider.GetServices<IPackageManifestReader>().OfType<BlockPreviewManifestReader>());
    }

    [Fact]
    public void Binds_the_section()
    {
        using var provider = Build(new()
        {
            ["Esatto:BlockPreview:BlockList:Enabled"] = "false",
            ["Esatto:BlockPreview:BlockList:IgnoredContentTypes:0"] = "heroBlock",
            ["Esatto:BlockPreview:Layout"] = "~/Views/Preview.cshtml",
            ["Esatto:BlockPreview:Stylesheets:0"] = "/css/site.css",
        });
        var options = provider.GetRequiredService<IOptions<BlockPreviewOptions>>().Value;
        Assert.False(options.BlockList.Enabled);
        Assert.Equal(["heroBlock"], options.BlockList.IgnoredContentTypes);
        Assert.Equal("~/Views/Preview.cshtml", options.Layout);
        Assert.Equal(["/css/site.css"], options.Stylesheets);
    }

    [Fact]
    public void Defaults_without_a_section()
    {
        using var provider = Build();
        var options = provider.GetRequiredService<IOptions<BlockPreviewOptions>>().Value;
        Assert.True(options.BlockList.Enabled);
        Assert.Empty(options.BlockList.IgnoredContentTypes);
        Assert.Equal("~/Views/BlockPreview.cshtml", options.Layout);
    }

    [Fact]
    public async Task Invalid_config_value_falls_back_to_defaults()
    {
        using var provider = Build(new() { ["Esatto:BlockPreview:BlockList:Enabled"] = "nope" });
        var reader = provider.GetServices<IPackageManifestReader>().OfType<BlockPreviewManifestReader>().Single();
        var manifest = Assert.Single(await reader.ReadPackageManifestsAsync());
        Assert.Single(manifest.Extensions);
    }
}
