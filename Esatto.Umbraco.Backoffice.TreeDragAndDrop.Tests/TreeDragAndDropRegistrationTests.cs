using Esatto.Umbraco.Backoffice.TreeDragAndDrop;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Manifest;
using Umbraco.Cms.Infrastructure.Manifest;
using Xunit;

namespace Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests;

public class TreeDragAndDropRegistrationTests
{
    // Stand-ins for Umbraco's BackOfficePackageManifestReader / AppPluginsPackageManifestReader,
    // registered in the same order Umbraco registers them (both plain AddSingleton).
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
        services.AddSingleton(Substitute.For<IPackageManifestFileProviderFactory>());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(config ?? []).Build());
        services.AddSingleton<IPackageManifestReader, FirstUmbracoReader>();
        services.AddSingleton<IPackageManifestReader, LastUmbracoReader>();
        for (var i = 0; i < registrations; i++) services.AddTreeDragAndDrop();
        return services.BuildServiceProvider();
    }

    private static TreeDragAndDropManifestReader OurReader(IServiceProvider provider) =>
        provider.GetServices<IPackageManifestReader>().OfType<TreeDragAndDropManifestReader>().Single();

    // PackagingService resolves ONE IPackageManifestReader (the last registered) to build
    // Packages → Installed. Ours must not become that one.
    [Fact]
    public void Single_resolution_stays_umbracos_last_reader()
    {
        using var provider = Build();
        Assert.IsType<LastUmbracoReader>(provider.GetRequiredService<IPackageManifestReader>());
    }

    [Fact]
    public void Our_reader_is_part_of_the_enumerable_that_PackageManifestService_reads()
    {
        using var provider = Build();
        Assert.Contains(provider.GetServices<IPackageManifestReader>(), r => r is TreeDragAndDropManifestReader);
    }

    [Fact]
    public void Registering_twice_adds_the_reader_once()
    {
        using var provider = Build(registrations: 2);
        Assert.Single(provider.GetServices<IPackageManifestReader>().OfType<TreeDragAndDropManifestReader>());
    }

    [Fact]
    public void Binds_the_Esatto_TreeDragAndDrop_section()
    {
        using var provider = Build(new() { ["Esatto:TreeDragAndDrop:Dictionary"] = "false" });
        Assert.False(provider.GetRequiredService<IOptions<TreeDragAndDropOptions>>().Value.Dictionary);
    }

    // Review Focus 2.
    [Fact]
    public async Task Missing_section_enables_every_tree()
    {
        using var provider = Build();
        var manifest = Assert.Single(await OurReader(provider).ReadPackageManifestsAsync());
        Assert.Equal(3, manifest.Extensions.Length);
    }

    // Review Focus 1: a typo in appsettings must not take down the manifest endpoint
    // (and with it the whole backoffice).
    [Fact]
    public async Task Invalid_config_value_falls_back_to_defaults()
    {
        using var provider = Build(new() { ["Esatto:TreeDragAndDrop:Dictionary"] = "nope" });
        var manifest = Assert.Single(await OurReader(provider).ReadPackageManifestsAsync());
        Assert.Equal(3, manifest.Extensions.Length);
    }
}
