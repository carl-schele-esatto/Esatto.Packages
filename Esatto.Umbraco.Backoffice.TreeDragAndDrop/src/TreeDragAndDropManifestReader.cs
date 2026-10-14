using System.Reflection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Manifest;
using Umbraco.Cms.Infrastructure.Manifest;

namespace Esatto.Umbraco.Backoffice.TreeDragAndDrop;

/// <summary>
/// Contributes one <c>backofficeEntryPoint</c> per enabled tree, so a disabled tree's
/// JavaScript is never loaded. The static <c>umbraco-package.json</c> (no extensions) gives the
/// package its identity — the Packages → Installed row, PackagingService and telemetry; this
/// manifest has no name, Id or telemetry so the package isn't listed or counted twice.
/// </summary>
public sealed class TreeDragAndDropManifestReader : IPackageManifestReader
{
    private static readonly string PackageVersion =
        typeof(TreeDragAndDropManifestReader).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0]
        ?? "0.0.0";

    private readonly IOptions<TreeDragAndDropOptions> _options;
    private readonly IPackageManifestFileProviderFactory _fileProviderFactory;
    private readonly ILogger<TreeDragAndDropManifestReader> _logger;

    public TreeDragAndDropManifestReader(
        IOptions<TreeDragAndDropOptions> options,
        IPackageManifestFileProviderFactory fileProviderFactory,
        ILogger<TreeDragAndDropManifestReader> logger)
    {
        _options = options;
        _fileProviderFactory = fileProviderFactory;
        _logger = logger;
    }

    public Task<IEnumerable<PackageManifest>> ReadPackageManifestsAsync()
    {
        TreeDragAndDropOptions options = ReadOptions();
        object[] extensions = TreeDefinitions.All
            .Where(tree => tree.IsEnabled(options) && !IsTakenByLegacyPackage(tree))
            .Select(object (tree) => tree.ToEntryPoint())
            .ToArray();

        PackageManifest[] manifests = extensions.Length == 0
            ? []
            :
            [
                new PackageManifest
                {
                    // Empty on purpose: Packages → Installed (ManifestService → every reader)
                    // lists each manifest that has a name, and the static umbraco-package.json
                    // already lists this package. Extensions register regardless of name.
                    Name = string.Empty,
                    Version = PackageVersion,
                    AllowTelemetry = false,
                    Extensions = extensions,
                },
            ];
        return Task.FromResult<IEnumerable<PackageManifest>>(manifests);
    }

    // Two packages on one tree means double drag handlers. While a legacy package is still
    // installed, it keeps its tree and this package stays out of it. Detected the way Umbraco's
    // AppPluginsPackageManifestReader finds manifests (a static-assets-only package's DLL is
    // never loaded, so an assembly check would not work).
    private bool IsTakenByLegacyPackage(TreeDefinition tree)
    {
        if (tree.LegacyPackageId is null) return false;
        IFileProvider? provider = _fileProviderFactory.Create();
        if (provider is null) return false;

        bool installed = provider.GetDirectoryContents($"/App_Plugins/{tree.LegacyPackageId}")
            .Any(file => !file.IsDirectory && file.Name.Equals("umbraco-package.json", StringComparison.OrdinalIgnoreCase));
        if (installed)
        {
            _logger.LogWarning(
                "{Package}: {Tree} drag-and-drop is left to the legacy package {Legacy}, which is still installed. Remove it to use this package for the {Tree} tree.",
                TreeDefinition.PackageId, tree.Name, tree.LegacyPackageId, tree.Name);
        }
        return installed;
    }

    // The manifest endpoint feeds the whole backoffice; a bad value in appsettings must
    // not take it down. The binder throws InvalidOperationException on an unconvertible value.
    private TreeDragAndDropOptions ReadOptions()
    {
        try
        {
            return _options.Value;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid '{Section}' configuration; using defaults (all trees enabled).", TreeDragAndDropOptions.SectionName);
            return new TreeDragAndDropOptions();
        }
    }
}
