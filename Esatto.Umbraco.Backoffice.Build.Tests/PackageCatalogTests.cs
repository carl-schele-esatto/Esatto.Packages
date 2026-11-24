using Esatto.Umbraco.Backoffice.Build.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Build.Tests;

public sealed class PackageCatalogTests
{
    [Fact]
    public void Defaults_come_first_with_display_names_then_the_esatto_family_sorted()
    {
        var loaded = new[]
        {
            FakeAssemblies.Create("uSync.Core", "18.3.0+abc"),
            FakeAssemblies.Create("Umbraco.Deploy.Core", "18.2.0"),
            FakeAssemblies.Create("Esatto.Umbraco.Backoffice.Seo", "1.0.2+d3adb33"),
            FakeAssemblies.Create("Esatto.Umbraco.Backoffice.Accessibility", "1.0.1"),
        };

        var packages = PackageCatalog.Resolve(loaded, []);

        // BlockPreview is a default too, but it is not loaded, so it is not listed.
        Assert.Equal(
            [
                new PackageVersion("Umbraco Deploy", "18.2.0"),
                new PackageVersion("uSync", "18.3.0"),
                new PackageVersion("Esatto.Umbraco.Backoffice.Accessibility", "1.0.1"),
                new PackageVersion("Esatto.Umbraco.Backoffice.Seo", "1.0.2"),
            ],
            packages);
    }

    [Fact]
    public void The_block_preview_default_uses_its_display_name()
    {
        var packages = PackageCatalog.Resolve([FakeAssemblies.Create("Umbraco.Community.BlockPreview", "5.0.1")], []);

        Assert.Equal([new PackageVersion("BlockPreview", "5.0.1")], packages);
    }

    [Fact]
    public void Only_loaded_assemblies_are_listed()
    {
        var loaded = new[] { FakeAssemblies.Create("uSync.Core", "18.3.0"), FakeAssemblies.Create("Unrelated.Library", "9.9.9") };

        var packages = PackageCatalog.Resolve(loaded, ["Not.Loaded", "Not.Loaded.Family.*"]);

        Assert.Equal([new PackageVersion("uSync", "18.3.0")], packages);
    }

    [Fact]
    public void Configured_entries_are_added_exact_or_by_family()
    {
        var loaded = new[]
        {
            FakeAssemblies.Create("Contoso.Core", "1.0.0"),
            FakeAssemblies.Create("Acme.Tools", "2.0.0+abc"),
            FakeAssemblies.Create("Contoso.Beta", "4.0.0"),
            FakeAssemblies.Create("Contoso.Alpha", "3.0.0"),
            FakeAssemblies.Create("Other.Thing", "5.0.0"),
        };

        var packages = PackageCatalog.Resolve(loaded, ["Acme.Tools", "Contoso.*"]);

        // The configured entries keep their order; a family is sorted ordinally; the display name is the assembly name.
        Assert.Equal(
            [
                new PackageVersion("Acme.Tools", "2.0.0"),
                new PackageVersion("Contoso.Alpha", "3.0.0"),
                new PackageVersion("Contoso.Beta", "4.0.0"),
                new PackageVersion("Contoso.Core", "1.0.0"),
            ],
            packages);
    }

    [Fact]
    public void An_assembly_matched_twice_is_listed_once_and_blank_entries_are_ignored()
    {
        var loaded = new[]
        {
            FakeAssemblies.Create("uSync.Core", "18.3.0"),
            FakeAssemblies.Create("Esatto.Umbraco.Backoffice.Seo", "1.0.2"),
        };

        // uSync.Core is a default and configured exactly; the Esatto family is a default and configured as a family.
        var packages = PackageCatalog.Resolve(loaded, ["uSync.Core", "Esatto.Umbraco.Backoffice.*", " ", "", null!]);

        // The default's display name wins over the configured assembly name.
        Assert.Equal(
            [new PackageVersion("uSync", "18.3.0"), new PackageVersion("Esatto.Umbraco.Backoffice.Seo", "1.0.2")],
            packages);
    }

    [Fact]
    public void No_configured_entries_at_all_means_only_the_built_in_list()
    {
        var loaded = new[] { FakeAssemblies.Create("uSync.Core", "18.3.0"), FakeAssemblies.Create("Unrelated.Library", "9.9.9") };

        // Configuration binding can produce null for a list.
        var packages = PackageCatalog.Resolve(loaded, null!);

        Assert.Equal([new PackageVersion("uSync", "18.3.0")], packages);
    }

    [Fact]
    public void Version_falls_back_to_the_assembly_version()
    {
        var assembly = FakeAssemblies.Create("Esatto.Umbraco.Backoffice.Plain", informationalVersion: null, version: new Version(1, 2, 3, 4));

        var packages = PackageCatalog.Resolve([assembly], []);

        Assert.Equal([new PackageVersion("Esatto.Umbraco.Backoffice.Plain", "1.2.3.4")], packages);
    }

    [Fact]
    public void An_informational_version_that_is_only_a_suffix_falls_back_to_the_assembly_version()
    {
        var assembly = FakeAssemblies.Create("Esatto.Umbraco.Backoffice.Odd", "+abc", new Version(2, 0, 0, 0));

        var packages = PackageCatalog.Resolve([assembly], []);

        Assert.Equal([new PackageVersion("Esatto.Umbraco.Backoffice.Odd", "2.0.0.0")], packages);
    }

    [Theory]
    [InlineData("18.3.1+f1ff7ec", "18.3.1")]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("1.0.0-preview.0.3+abc", "1.0.0-preview.0.3")]
    [InlineData("", null)]
    [InlineData("+abc", null)]
    [InlineData(null, null)]
    public void CleanVersion_drops_the_build_suffix(string? informational, string? expected)
    {
        Assert.Equal(expected, PackageCatalog.CleanVersion(informational));
    }
}
