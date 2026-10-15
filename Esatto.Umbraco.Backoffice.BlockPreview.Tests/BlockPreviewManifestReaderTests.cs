using Esatto.Umbraco.Backoffice.BlockPreview;
using Esatto.Umbraco.Backoffice.BlockPreview.Manifest;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Manifest;
using Xunit;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Tests;

public class BlockPreviewManifestReaderTests
{
    private static async Task<List<PackageManifest>> ReadAsync(BlockPreviewOptions options) =>
        (await new BlockPreviewManifestReader(Options.Create(options), NullLogger<BlockPreviewManifestReader>.Instance)
            .ReadPackageManifestsAsync()).ToList();

    [Fact]
    public async Task Emits_the_generic_block_list_view_when_enabled()
    {
        var manifest = Assert.Single(await ReadAsync(new BlockPreviewOptions()));
        var view = Assert.IsType<BlockCustomViewExtension>(Assert.Single(manifest.Extensions));

        Assert.Equal("blockEditorCustomView", view.Type);
        Assert.Equal("Esatto.Umbraco.Backoffice.BlockPreview.Frame", view.Alias);
        Assert.Equal("/App_Plugins/Esatto.Umbraco.Backoffice.BlockPreview/block-preview-frame.js", view.Element);
        Assert.Equal("esatto-block-preview-frame", view.ElementName);
        Assert.Equal("block-list", view.ForBlockEditor);
        Assert.Equal(-1000, view.Weight);
    }

    [Fact]
    public async Task Emits_nothing_when_block_list_previews_are_disabled()
    {
        Assert.Empty(await ReadAsync(new BlockPreviewOptions { BlockList = new BlockListOptions { Enabled = false } }));
    }

    [Fact]
    public async Task Manifest_has_no_name_id_or_telemetry_so_installed_lists_the_package_once()
    {
        var manifest = Assert.Single(await ReadAsync(new BlockPreviewOptions()));
        Assert.Equal(string.Empty, manifest.Name);
        Assert.Null(manifest.Id);
        Assert.False(manifest.AllowTelemetry);
        Assert.False(manifest.AllowPublicAccess);
    }
}
