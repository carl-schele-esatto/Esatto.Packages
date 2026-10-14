using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private readonly ILogger<TreeDragAndDropManifestReader> _logger;

    public TreeDragAndDropManifestReader(IOptions<TreeDragAndDropOptions> options, ILogger<TreeDragAndDropManifestReader> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task<IEnumerable<PackageManifest>> ReadPackageManifestsAsync()
    {
        TreeDragAndDropOptions options = ReadOptions();
        object[] extensions = TreeDefinitions.All
            .Where(tree => tree.IsEnabled(options))
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
