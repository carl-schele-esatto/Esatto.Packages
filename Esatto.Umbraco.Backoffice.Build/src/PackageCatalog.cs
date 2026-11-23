using System.Reflection;

namespace Esatto.Umbraco.Backoffice.Build;

/// <summary>One package the site runs, with the version its assembly carries.</summary>
public sealed record PackageVersion(string Name, string? Version);

/// <summary>Decides which of the loaded assemblies the dashboard lists as packages, and what to call them.</summary>
internal static class PackageCatalog
{
    /// <summary>Packages beyond the CMS that decide how a site behaves, with the assembly that carries the version.</summary>
    private static readonly (string DisplayName, string Assembly)[] Defaults =
    [
        ("Umbraco Deploy", "Umbraco.Deploy.Core"),
        ("uSync", "uSync.Core"),
        ("BlockPreview", "Umbraco.Community.BlockPreview"),
    ];

    private const string EsattoPackagePrefix = "Esatto.Umbraco.Backoffice.";

    /// <summary>
    /// The defaults first, then the loaded <c>Esatto.Umbraco.Backoffice.*</c> packages, then the configured entries in
    /// order (an exact assembly name, or a trailing <c>*</c> for a family). Only loaded assemblies are listed, and an
    /// assembly only once, under the first name it was matched by; blank entries are ignored, and so is a null list
    /// (configuration binding can produce one).
    /// </summary>
    public static IReadOnlyList<PackageVersion> Resolve(IEnumerable<Assembly> loaded, IEnumerable<string>? configured)
    {
        var byName = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        foreach (var assembly in loaded)
        {
            if (assembly.GetName().Name is { Length: > 0 } name)
            {
                byName.TryAdd(name, assembly);
            }
        }

        var listed = new HashSet<string>(StringComparer.Ordinal);
        var packages = new List<PackageVersion>();

        void Add(string displayName, string assemblyName)
        {
            if (byName.TryGetValue(assemblyName, out var assembly) && listed.Add(assemblyName))
            {
                packages.Add(new PackageVersion(displayName, VersionOf(assembly)));
            }
        }

        void AddFamily(string prefix)
        {
            foreach (var name in byName.Keys.Where(n => n.StartsWith(prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
            {
                Add(name, name);
            }
        }

        foreach (var (displayName, assemblyName) in Defaults)
        {
            Add(displayName, assemblyName);
        }

        AddFamily(EsattoPackagePrefix);

        foreach (var entry in configured ?? [])
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            var trimmed = entry.Trim();
            if (trimmed.EndsWith('*'))
            {
                AddFamily(trimmed[..^1]);
            }
            else
            {
                Add(trimmed, trimmed);
            }
        }

        return packages;
    }

    /// <summary>"18.3.1+f1ff7ec" becomes "18.3.1". The suffix is the commit the package was built from, not a version.</summary>
    public static string? CleanVersion(string? informational)
    {
        var version = informational?.Split('+')[0];
        return string.IsNullOrWhiteSpace(version) ? null : version;
    }

    private static string? VersionOf(Assembly assembly) =>
        CleanVersion(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion)
        ?? assembly.GetName().Version?.ToString();
}
