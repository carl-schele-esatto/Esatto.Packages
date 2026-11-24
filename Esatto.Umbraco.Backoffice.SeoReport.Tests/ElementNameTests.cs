using System.Text.RegularExpressions;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// Custom element names are global to the backoffice page: a second customElements.define of a taken name throws, and
/// whichever dashboard loads second renders blank. Nova defines "esatto-seo-dashboard" for its own SEO report, so every
/// element this package defines carries the package's own prefix.
/// </summary>
public class ElementNameTests
{
    private const string Prefix = "esatto-seo-report-";

    private static IReadOnlyList<string> DefinedElements() => Directory
        .EnumerateFiles(Path.Combine(ClientFiles.ClientDirectory(), "src"), "*.ts", SearchOption.AllDirectories)
        .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"@customElement\(\s*(?<q>[""'])(?<name>[^""']+)\k<q>\s*\)").Select(m => m.Groups["name"].Value))
        .ToList();

    private static string ManifestElement()
    {
        using var manifest = ClientFiles.Manifest();
        return manifest.RootElement.GetProperty("extensions").EnumerateArray()
            .Single(e => e.GetProperty("type").GetString() == "dashboard")
            .GetProperty("elementName").GetString()!;
    }

    [Fact]
    public void Every_element_carries_the_package_prefix()
    {
        var names = DefinedElements();

        Assert.NotEmpty(names);
        Assert.All(names, n => Assert.StartsWith(Prefix, n));
    }

    [Fact]
    public void The_manifest_names_an_element_the_package_defines() => Assert.Contains(ManifestElement(), DefinedElements());
}
