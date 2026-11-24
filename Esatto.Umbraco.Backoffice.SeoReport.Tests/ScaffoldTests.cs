using System.Text.Json;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Umbraco.Cms.Core.Composing;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

public class ScaffoldTests
{
    private static List<JsonElement> Extensions(JsonDocument manifest, string type)
        => manifest.RootElement.GetProperty("extensions").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == type).ToList();

    [Fact]
    public void Package_assembly_name_matches_package_id()
        => Assert.Equal("Esatto.Umbraco.Backoffice.SeoReport", typeof(SeoReportComposer).Assembly.GetName().Name);

    [Fact]
    public void Composer_is_discoverable_by_Umbraco()
    {
        Assert.Equal("Esatto.Umbraco.Backoffice.SeoReport", typeof(SeoReportComposer).Namespace);
        Assert.True(typeof(IComposer).IsAssignableFrom(typeof(SeoReportComposer)));
    }

    [Fact]
    public void Dashboard_is_registered_in_the_content_section()
    {
        using var manifest = ClientFiles.Manifest();

        var dashboard = Assert.Single(Extensions(manifest, "dashboard"));
        Assert.Equal("Esatto.Umbraco.Backoffice.SeoReport.Dashboard", dashboard.GetProperty("alias").GetString());
        Assert.Equal("seo-report", dashboard.GetProperty("meta").GetProperty("pathname").GetString());
        Assert.Equal("SEO Report", dashboard.GetProperty("meta").GetProperty("label").GetString());
        Assert.Equal(-105, dashboard.GetProperty("weight").GetInt32());

        var condition = Assert.Single(dashboard.GetProperty("conditions").EnumerateArray());
        Assert.Equal("Umb.Condition.SectionAlias", condition.GetProperty("alias").GetString());
        Assert.Equal("Umb.Section.Content", condition.GetProperty("match").GetString());
    }

    [Fact]
    public void Third_party_notices_name_the_bundled_versions_and_ship_in_the_package_and_next_to_the_script()
    {
        var client = ClientFiles.ClientDirectory();
        var packageRoot = Directory.GetParent(client)!.FullName;
        var packed = File.ReadAllText(Path.Combine(packageRoot, "THIRD-PARTY-NOTICES.md"));
        var served = File.ReadAllText(Path.Combine(client, "public", "THIRD-PARTY-NOTICES.md"));

        Assert.Equal(packed, served);
        Assert.Contains("Copyright (c) 2014-2024 Chart.js Contributors", packed);
        Assert.Contains("Copyright (c) 2018-2024 Jukka Kurkela", packed);

        // A version bump of what is bundled has to come with its notice.
        using var lockfile = JsonDocument.Parse(File.ReadAllText(Path.Combine(client, "package-lock.json")));
        var packages = lockfile.RootElement.GetProperty("packages");
        Assert.Contains($"## Chart.js {packages.GetProperty("node_modules/chart.js").GetProperty("version").GetString()}\n", packed);
        Assert.Contains($"## @kurkle/color {packages.GetProperty("node_modules/@kurkle/color").GetProperty("version").GetString()}\n", packed);

        var project = File.ReadAllText(Path.Combine(packageRoot, "Esatto.Umbraco.Backoffice.SeoReport.csproj"));
        Assert.Contains("<None Include=\"THIRD-PARTY-NOTICES.md\" Pack=\"true\" PackagePath=\"\\\" />", project);
    }

    [Fact]
    public void The_client_build_hook_is_platform_independent_and_a_pack_without_a_built_client_is_refused()
    {
        var packageRoot = Directory.GetParent(ClientFiles.ClientDirectory())!.FullName;
        var project = File.ReadAllText(Path.Combine(packageRoot, "Esatto.Umbraco.Backoffice.SeoReport.csproj"));

        // The stale-item test compares against an item's FullPath, which uses the separators of the platform: a literal
        // backslash in the prefix matches nothing on Linux or macOS and leaves the previous build's wwwroot items in the package.
        Assert.DoesNotContain("StartsWith('$(MSBuildProjectDirectory)\\wwwroot\\'", project);
        Assert.Contains("StartsWith('$(_ClientWwwroot)'", project);
        Assert.Contains("[MSBuild]::NormalizePath('$(MSBuildProjectDirectory)', 'wwwroot')", project);

        // -p:SkipClientBuild=true on a clone with no wwwroot would otherwise pack a package with no dashboard in it.
        Assert.Contains("<Target Name=\"RequireBuiltClientToPack\"", project);
        Assert.Contains("Condition=\"'$(SkipClientBuild)' == 'true'\"", project);
        Assert.Contains("<Error Code=\"SEOREPORT001\"", project);
    }

    [Fact]
    public void Only_the_english_localization_is_registered()
    {
        using var manifest = ClientFiles.Manifest();

        var localization = Assert.Single(Extensions(manifest, "localization"));
        Assert.Equal("en", localization.GetProperty("meta").GetProperty("culture").GetString());
        Assert.Equal("/App_Plugins/Esatto.Umbraco.Backoffice.SeoReport/lang/en.js", localization.GetProperty("js").GetString());
    }
}
