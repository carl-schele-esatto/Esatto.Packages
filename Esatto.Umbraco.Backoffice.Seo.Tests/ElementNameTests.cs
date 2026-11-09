using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Seo.Tests;

/// <summary>
/// Custom element names are global to the backoffice page: a second customElements.define of a taken name throws, and
/// whichever dashboard loads second renders blank. 1.0.0 used "esatto-seo-dashboard", which Nova's own SEO dashboard
/// (GA4 / Search Console) also defines, so opening one blanked the other. Every element this package defines carries
/// the package's own prefix.
/// </summary>
public class ElementNameTests
{
    private const string Prefix = "esatto-seo-health-";

    private static string Client()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var client = Path.Combine(dir.FullName, "Esatto.Umbraco.Backoffice.Seo", "Client");
            if (Directory.Exists(client)) return client;
        }

        throw new DirectoryNotFoundException("Esatto.Umbraco.Backoffice.Seo/Client not found above the test output.");
    }

    private static IReadOnlyList<string> DefinedElements() => Directory
        .EnumerateFiles(Path.Combine(Client(), "src"), "*.ts", SearchOption.AllDirectories)
        .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"@customElement\('([^']+)'\)").Select(m => m.Groups[1].Value))
        .ToList();

    private static string ManifestElement()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Client(), "public", "umbraco-package.json")));
        return manifest.RootElement.GetProperty("extensions")[0].GetProperty("elementName").GetString()!;
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
