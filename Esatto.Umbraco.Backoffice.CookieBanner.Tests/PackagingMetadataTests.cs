using System.Reflection;
using System.Text.RegularExpressions;
using Esatto.Umbraco.Backoffice.CookieBanner.TagHelpers;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

/// <summary>
/// Guards the shipped documentation surface. Every assertion here failed at least once by hand
/// during the 1.0.0 pack: an option added to <see cref="CookieBannerOptions"/> and never
/// documented, and a missing icon that made <c>dotnet pack</c> emit NU5046.
/// </summary>
/// <remarks>
/// 1.0.0 ships with no <c>docs/*.png</c> screenshots: there is no running Umbraco site to capture
/// them against in this environment, and nuget.org packages are immutable, so a broken relative
/// image reference in the README would be unfixable for the lifetime of that version. Rather than
/// invent placeholder images or skip the guard entirely, <see cref="Readme_has_no_broken_or_relative_images"/>
/// tolerates zero images today but still fails the moment anyone adds a relative or dangling one -
/// including whoever wires up the real screenshots later.
/// </remarks>
public sealed class PackagingMetadataTests
{
    private const string RawImagePrefix =
        "https://raw.githubusercontent.com/carl-schele-esatto/Esatto.Packages/main/"
        + "Esatto.Umbraco.Backoffice.CookieBanner/docs/";

    private static readonly Regex MarkdownImage = new(@"!\[[^\]]*\]\(([^)]+)\)", RegexOptions.Compiled);

    // bin/<Config>/net10.0 -> test project -> repo root.
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string PackageDirectory =>
        Path.Combine(RepoRoot, "Esatto.Umbraco.Backoffice.CookieBanner");

    private static string ReadmePath => Path.Combine(PackageDirectory, "README.md");

    [Fact]
    public void Readme_documents_every_configuration_option()
    {
        // Pins: an option can never be added to CookieBannerOptions without landing in the README table.
        var readme = File.ReadAllText(ReadmePath);

        Assert.Contains(CookieBannerOptions.SectionName, readme, StringComparison.Ordinal);

        PropertyInfo[] properties = typeof(CookieBannerOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.NotEmpty(properties);

        foreach (PropertyInfo property in properties)
        {
            Assert.Contains($"`{property.Name}`", readme, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Readme_install_section_names_every_namespace_a_consumer_must_import()
    {
        // Pins: the install steps once omitted `using Esatto.Umbraco.Backoffice.CookieBanner;`, so a
        // consumer following them verbatim hit `CS1061: 'WebApplication' does not contain a
        // definition for 'UseCookieConsent'` - an extension method is invisible without its
        // declaring type's namespace in scope, and the compiler error names neither the missing
        // namespace nor the word "using".
        //
        // Namespaces are derived from the real public extension/tag-helper types rather than
        // hardcoded, so a future move (either type relocating) keeps this test honest instead of
        // re-asserting a string that could go stale right along with the README:
        // - CookieBannerApplicationBuilderExtensions declares UseCookieConsent() -> its namespace is
        //   the Program.cs import.
        // - ConsentBannerTagHelper is one of the tag helpers registered by the `@addTagHelper` line
        //   -> its namespace is the _ViewImports.cshtml `@using` import.
        string rootNamespace = typeof(CookieBannerApplicationBuilderExtensions).Namespace!;
        string tagHelpersNamespace = typeof(ConsentBannerTagHelper).Namespace!;

        var readme = File.ReadAllText(ReadmePath);
        var installSection = ExtractSection(readme, "## Install", "\n## ");

        AssertImportsNamespace(installSection, rootNamespace);
        AssertImportsNamespace(installSection, tagHelpersNamespace);
    }

    private static string ExtractSection(string readme, string heading, string nextHeadingPrefix)
    {
        var start = readme.IndexOf(heading, StringComparison.Ordinal);
        Assert.True(start >= 0, $"README does not contain a \"{heading}\" heading.");

        var contentStart = start + heading.Length;
        var nextHeadingIndex = readme.IndexOf(nextHeadingPrefix, contentStart, StringComparison.Ordinal);
        return nextHeadingIndex < 0 ? readme[contentStart..] : readme[contentStart..nextHeadingIndex];
    }

    /// <summary>
    /// Matches a genuine <c>using X;</c> (C#) or <c>@using X</c> (Razor) directive naming exactly
    /// <paramref name="namespaceName"/> - not merely a longer namespace that happens to start with
    /// it, such as the <c>.TagHelpers</c> sub-namespace containing the root namespace as a prefix.
    /// A plain substring search would pass even with the root namespace's own import missing,
    /// because the root namespace's text is already present inside the (unrelated) TagHelpers
    /// import line.
    /// </summary>
    private static void AssertImportsNamespace(string section, string namespaceName)
    {
        var pattern = $@"@?using\s+{Regex.Escape(namespaceName)}(?![\w.])";
        Assert.True(
            Regex.IsMatch(section, pattern),
            $"Expected the README's Install section to import `{namespaceName}` via a `using` or "
                + "`@using` directive naming exactly that namespace.");
    }

    [Fact]
    public void Readme_has_no_broken_or_relative_images()
    {
        // Pins: nuget.org rewrites nothing, so a relative image path renders as a broken image on
        // the package page. Any markdown image the README ever gains must be an absolute
        // raw.githubusercontent URL on the main branch, and the file it names must actually exist
        // under docs/ - 1.0.0 simply has none yet, which is a valid, asserted state, not an
        // untested one.
        var readme = File.ReadAllText(ReadmePath);

        foreach (Match match in MarkdownImage.Matches(readme))
        {
            var url = match.Groups[1].Value;
            Assert.StartsWith(RawImagePrefix, url, StringComparison.Ordinal);

            var fileName = url[RawImagePrefix.Length..];
            var path = Path.Combine(PackageDirectory, "docs", fileName);
            Assert.True(File.Exists(path), $"README references docs/{fileName} but {path} does not exist.");
        }
    }

    [Fact]
    public void Package_ships_the_shared_house_icon()
    {
        // Pins: PackageIcon=icon.png is declared in the csproj, so a missing file breaks `dotnet pack` (NU5046).
        var icon = Path.Combine(PackageDirectory, "icon.png");

        Assert.True(File.Exists(icon), $"{icon} does not exist.");
        Assert.Equal(
            new FileInfo(Path.Combine(RepoRoot, "Esatto.Umbraco.Backoffice.Redirects", "icon.png")).Length,
            new FileInfo(icon).Length);
    }

    [Fact]
    public void Csproj_carries_the_nuget_metadata_the_marketplace_needs()
    {
        // Pins: the Umbraco Marketplace only lists a package carrying the umbraco-marketplace tag,
        // and the repo invariant is that every package exposes its source-code link.
        var csproj = File.ReadAllText(
            Path.Combine(PackageDirectory, "Esatto.Umbraco.Backoffice.CookieBanner.csproj"));

        Assert.Contains("<PackageId>Esatto.Umbraco.Backoffice.CookieBanner</PackageId>", csproj, StringComparison.Ordinal);
        Assert.Contains("umbraco-marketplace", csproj, StringComparison.Ordinal);
        Assert.Contains("<PackageReadmeFile>README.md</PackageReadmeFile>", csproj, StringComparison.Ordinal);
        Assert.Contains("<PackageIcon>icon.png</PackageIcon>", csproj, StringComparison.Ordinal);
        Assert.Contains("<PackageLicenseExpression>MIT</PackageLicenseExpression>", csproj, StringComparison.Ordinal);
        Assert.Contains(
            "<PackageProjectUrl>https://github.com/carl-schele-esatto/Esatto.Packages/tree/main/Esatto.Umbraco.Backoffice.CookieBanner</PackageProjectUrl>",
            csproj,
            StringComparison.Ordinal);
        Assert.Contains(
            "<RepositoryUrl>https://github.com/carl-schele-esatto/Esatto.Packages</RepositoryUrl>",
            csproj,
            StringComparison.Ordinal);
        Assert.Contains("<RepositoryType>git</RepositoryType>", csproj, StringComparison.Ordinal);
    }
}
