using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Manifest;
using Umbraco.Cms.Infrastructure.Manifest;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Manifest;

/// <summary>
/// Contributes the generic Block List custom view, but only when
/// <c>Esatto:BlockPreview:BlockList:Enabled</c> is true, so a disabled preview loads no script.
/// The static <c>umbraco-package.json</c> (no extensions) carries the package identity;
/// this manifest has no name, Id or telemetry so Packages → Installed lists it once.
/// </summary>
public sealed class BlockPreviewManifestReader : IPackageManifestReader
{
    public const string PackageId = "Esatto.Umbraco.Backoffice.BlockPreview";

    private static readonly string PackageVersion =
        typeof(BlockPreviewManifestReader).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0]
        ?? "0.0.0";

    // The block list renders only the highest-weight matching custom view, so any view a site
    // registers for a specific block type (default weight 0) wins over this generic one.
    private const int GenericViewWeight = -1000;

    private readonly IOptions<BlockPreviewOptions> _options;
    private readonly ILogger<BlockPreviewManifestReader> _logger;

    public BlockPreviewManifestReader(IOptions<BlockPreviewOptions> options, ILogger<BlockPreviewManifestReader> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task<IEnumerable<PackageManifest>> ReadPackageManifestsAsync()
    {
        if (!ReadOptions().BlockList.Enabled)
        {
            return Task.FromResult(Enumerable.Empty<PackageManifest>());
        }

        PackageManifest[] manifests =
        [
            new PackageManifest
            {
                // Empty on purpose: Packages → Installed lists each manifest that has a name.
                Name = string.Empty,
                Version = PackageVersion,
                AllowTelemetry = false,
                Extensions =
                [
                    new BlockCustomViewExtension(
                        Type: "blockEditorCustomView",
                        Alias: $"{PackageId}.Frame",
                        Name: "Block Preview (frontend rendering)",
                        Element: $"/App_Plugins/{PackageId}/block-preview-frame.js",
                        ElementName: "esatto-block-preview-frame",
                        ForBlockEditor: "block-list",
                        Weight: GenericViewWeight),
                ],
            },
        ];
        return Task.FromResult<IEnumerable<PackageManifest>>(manifests);
    }

    // The manifest endpoint feeds the whole backoffice; a bad appsettings value must not take it down.
    private BlockPreviewOptions ReadOptions()
    {
        try
        {
            return _options.Value;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid '{Section}' configuration; using defaults.", BlockPreviewOptions.SectionName);
            return new BlockPreviewOptions();
        }
    }
}
