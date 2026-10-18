using Esatto.Umbraco.Backoffice.TreeDragAndDrop;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Manifest;
using Xunit;

namespace Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests;

public sealed class TreeDragAndDropManifestReaderTests : IDisposable
{
    private const string PackageId = "Esatto.Umbraco.Backoffice.TreeDragAndDrop";
    private readonly string _root = Directory.CreateTempSubdirectory("tdd-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // Simulates an installed legacy package the way Umbraco discovers it: /App_Plugins/<id>/umbraco-package.json.
    private void InstallLegacy(string legacyId)
    {
        string dir = Path.Combine(_root, "App_Plugins", legacyId);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "umbraco-package.json"), "{}");
    }

    private async Task<List<PackageManifest>> ReadAsync(TreeDragAndDropOptions options, bool noFileProvider = false)
    {
        var factory = Substitute.For<IPackageManifestFileProviderFactory>();
        factory.Create().Returns(noFileProvider ? null : new PhysicalFileProvider(_root));
        var reader = new TreeDragAndDropManifestReader(
            Options.Create(options), factory, NullLogger<TreeDragAndDropManifestReader>.Instance);
        return (await reader.ReadPackageManifestsAsync()).ToList();
    }

    private static List<EntryPointExtension> Entries(PackageManifest manifest) =>
        manifest.Extensions.Cast<EntryPointExtension>().ToList();

    [Theory]
    [InlineData("Content", "content", "Umb.Section.Content")]
    [InlineData("Media", "media", "Umb.Section.Media")]
    [InlineData("Dictionary", "dictionary", "Umb.Section.Translation")]
    public async Task Emits_each_tree_entry_point(string tree, string file, string section)
    {
        var manifest = Assert.Single(await ReadAsync(new TreeDragAndDropOptions()));

        var entry = Assert.Single(Entries(manifest), e => e.Alias == $"{PackageId}.{tree}");
        Assert.Equal("backofficeEntryPoint", entry.Type);
        Assert.Equal($"/App_Plugins/{PackageId}/{file}-tree-drag-drop.js", entry.Js);
        var condition = Assert.Single(entry.Conditions);
        Assert.Equal("Umb.Condition.SectionAlias", condition.Alias);
        Assert.Equal(section, condition.Match);
    }

    [Theory]
    [InlineData("Content")]
    [InlineData("Media")]
    [InlineData("Dictionary")]
    public async Task Each_switch_removes_only_its_tree(string tree)
    {
        var options = new TreeDragAndDropOptions
        {
            Content = tree != "Content",
            Media = tree != "Media",
            Dictionary = tree != "Dictionary",
        };
        var manifest = Assert.Single(await ReadAsync(options));

        var aliases = Entries(manifest).Select(e => e.Alias).ToList();
        Assert.Equal(2, aliases.Count);
        Assert.DoesNotContain($"{PackageId}.{tree}", aliases);
    }

    [Fact]
    public async Task Emits_no_manifest_when_every_tree_is_disabled()
    {
        Assert.Empty(await ReadAsync(new TreeDragAndDropOptions { Content = false, Media = false, Dictionary = false }));
    }

    // Review Focus 3: never two packages on one tree.
    [Theory]
    [InlineData("Esatto.Umbraco.Backoffice.ContentTreeDragAndDrop", "Content")]
    [InlineData("Esatto.Umbraco.Backoffice.MediaTreeDragAndDrop", "Media")]
    public async Task Leaves_out_a_tree_whose_legacy_package_is_installed(string legacyId, string tree)
    {
        InstallLegacy(legacyId);
        var manifest = Assert.Single(await ReadAsync(new TreeDragAndDropOptions()));

        var aliases = Entries(manifest).Select(e => e.Alias).ToList();
        Assert.Equal(2, aliases.Count);
        Assert.DoesNotContain($"{PackageId}.{tree}", aliases);
    }

    [Fact]
    public async Task No_file_provider_means_no_legacy_detected()
    {
        var manifest = Assert.Single(await ReadAsync(new TreeDragAndDropOptions(), noFileProvider: true));
        Assert.Equal(3, manifest.Extensions.Length);
    }

    [Fact]
    public async Task Manifest_is_private_versioned_and_not_counted_twice()
    {
        var manifest = Assert.Single(await ReadAsync(new TreeDragAndDropOptions()));

        // The static umbraco-package.json carries the package's Id, name and telemetry;
        // this manifest only contributes entry points. Packages → Installed lists every
        // manifest with a name, so an empty Name keeps it from showing as a second package.
        Assert.Equal(string.Empty, manifest.Name);
        Assert.Null(manifest.Id);
        Assert.False(manifest.AllowTelemetry);
        Assert.False(manifest.AllowPublicAccess);
        Assert.False(string.IsNullOrWhiteSpace(manifest.Version));
        Assert.DoesNotContain("+", manifest.Version);
    }
}
