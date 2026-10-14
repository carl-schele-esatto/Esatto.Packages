using Esatto.Umbraco.Backoffice.TreeDragAndDrop;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Manifest;
using Xunit;

namespace Esatto.Umbraco.Backoffice.TreeDragAndDrop.Tests;

public class TreeDragAndDropManifestReaderTests
{
    private const string PackageId = "Esatto.Umbraco.Backoffice.TreeDragAndDrop";

    private static async Task<List<PackageManifest>> ReadAsync(TreeDragAndDropOptions options)
    {
        var reader = new TreeDragAndDropManifestReader(
            Options.Create(options), NullLogger<TreeDragAndDropManifestReader>.Instance);
        return (await reader.ReadPackageManifestsAsync()).ToList();
    }

    [Fact]
    public async Task Emits_the_dictionary_entry_point_when_enabled()
    {
        var manifest = Assert.Single(await ReadAsync(new TreeDragAndDropOptions()));

        var entry = Assert.IsType<EntryPointExtension>(Assert.Single(manifest.Extensions));
        Assert.Equal("backofficeEntryPoint", entry.Type);
        Assert.Equal($"{PackageId}.Dictionary", entry.Alias);
        Assert.Equal($"/App_Plugins/{PackageId}/dictionary-tree-drag-drop.js", entry.Js);
        var condition = Assert.Single(entry.Conditions);
        Assert.Equal("Umb.Condition.SectionAlias", condition.Alias);
        Assert.Equal("Umb.Section.Translation", condition.Match);
    }

    [Fact]
    public async Task Emits_no_manifest_when_every_tree_is_disabled()
    {
        Assert.Empty(await ReadAsync(new TreeDragAndDropOptions { Dictionary = false }));
    }

    [Fact]
    public async Task Manifest_is_private_versioned_and_not_counted_twice()
    {
        var manifest = Assert.Single(await ReadAsync(new TreeDragAndDropOptions()));

        // The static umbraco-package.json carries the package's Id and telemetry;
        // this manifest only contributes entry points.
        Assert.Null(manifest.Id);
        Assert.False(manifest.AllowTelemetry);
        Assert.False(manifest.AllowPublicAccess);
        Assert.False(string.IsNullOrWhiteSpace(manifest.Version));
        Assert.DoesNotContain("+", manifest.Version);
    }
}
