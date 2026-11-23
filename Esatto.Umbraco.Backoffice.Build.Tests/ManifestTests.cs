using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Build.Tests;

/// <summary>
/// The manifest in Client/public/umbraco-package.json is what registers the dashboard and its English texts, and nothing
/// else ties it to the TypeScript. Custom element names are global to the backoffice page: a second customElements.define
/// of a taken name throws, and whichever dashboard loads second renders blank. So every element this package defines
/// carries the package's own prefix, and the manifest names one of them.
/// </summary>
public class ManifestTests
{
    private const string Prefix = "esatto-build-";
    private const string PluginFolder = "/App_Plugins/Esatto.Umbraco.Backoffice.Build/";

    private static string PackageFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var package = Path.Combine(dir.FullName, "Esatto.Umbraco.Backoffice.Build");
            if (Directory.Exists(Path.Combine(package, "Client")))
            {
                return package;
            }
        }

        throw new DirectoryNotFoundException("Esatto.Umbraco.Backoffice.Build/Client not found above the test output.");
    }

    private static string Client() => Path.Combine(PackageFolder(), "Client");

    private static IReadOnlyList<string> DefinedElements() => Directory
        .EnumerateFiles(Path.Combine(Client(), "src"), "*.ts", SearchOption.AllDirectories)
        .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"@customElement\('([^']+)'\)").Select(m => m.Groups[1].Value))
        .ToList();

    /// <summary>The extensions of one type, found by their "type" and not by their position in the list.</summary>
    private static List<JsonElement> Extensions(string type)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Client(), "public", "umbraco-package.json")));
        return manifest.RootElement.GetProperty("extensions").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == type)
            .Select(e => e.Clone())
            .ToList();
    }

    /// <summary>Where a manifest path (/App_Plugins/Esatto.Umbraco.Backoffice.Build/…) lands once the client is built.</summary>
    private static string BuiltFile(string manifestPath)
    {
        Assert.StartsWith(PluginFolder, manifestPath);
        return Path.Combine(PackageFolder(), "wwwroot", "App_Plugins", "Esatto.Umbraco.Backoffice.Build", manifestPath[PluginFolder.Length..]);
    }

    [Fact]
    public void Every_element_carries_the_package_prefix()
    {
        var names = DefinedElements();

        Assert.NotEmpty(names);
        Assert.All(names, n => Assert.StartsWith(Prefix, n));
    }

    [Fact]
    public void The_dashboard_names_an_element_the_package_defines()
    {
        var dashboard = Assert.Single(Extensions("dashboard"));

        Assert.Contains(dashboard.GetProperty("elementName").GetString(), DefinedElements());
    }

    [Fact]
    public void The_dashboard_is_the_last_tab_of_Content_under_the_build_path()
    {
        var dashboard = Assert.Single(Extensions("dashboard"));

        Assert.Equal("Esatto.Umbraco.Backoffice.Build.Dashboard", dashboard.GetProperty("alias").GetString());
        Assert.Equal(-200, dashboard.GetProperty("weight").GetInt32());
        Assert.Equal("Build", dashboard.GetProperty("meta").GetProperty("label").GetString());
        Assert.Equal("build", dashboard.GetProperty("meta").GetProperty("pathname").GetString());
    }

    [Fact]
    public void The_dashboard_shows_in_the_Content_section_only()
    {
        var dashboard = Assert.Single(Extensions("dashboard"));

        var condition = Assert.Single(dashboard.GetProperty("conditions").EnumerateArray().ToList());
        Assert.Equal("Umb.Condition.SectionAlias", condition.GetProperty("alias").GetString());
        Assert.Equal("Umb.Section.Content", condition.GetProperty("match").GetString());
    }

    [Fact]
    public void The_dashboard_element_is_built_where_the_manifest_says()
    {
        var dashboard = Assert.Single(Extensions("dashboard"));

        Assert.True(File.Exists(BuiltFile(dashboard.GetProperty("element").GetString()!)), "Run `npm run build` in Client first.");
    }

    [Fact]
    public void The_only_language_is_English_and_its_file_is_built_where_the_manifest_says()
    {
        var localization = Assert.Single(Extensions("localization"));

        Assert.Equal("Esatto.Umbraco.Backoffice.Build.Localization.En", localization.GetProperty("alias").GetString());
        Assert.Equal("en", localization.GetProperty("meta").GetProperty("culture").GetString());
        Assert.Equal(PluginFolder + "lang/en.js", localization.GetProperty("js").GetString());
        Assert.True(File.Exists(BuiltFile(localization.GetProperty("js").GetString()!)), "Run `npm run build` in Client first.");
    }
}
