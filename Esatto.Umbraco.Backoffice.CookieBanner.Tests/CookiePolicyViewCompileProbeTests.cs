using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Umbraco.Cms.Core.Models.Blocks;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

/// <summary>
/// C2 regression guard. Umbraco writes <c>Views/CookiePolicy.cshtml</c> to the CONSUMER's own app
/// at install time (<c>CookieBannerSchemaInstaller</c>), and with Razor runtime compilation on -
/// the default for `dotnet new umbraco` - that disk copy shadows the RCL view compiled into this
/// assembly and is instead compiled inside the CONSUMER's own assembly. Every type the view calls
/// must therefore be public: an internal one is inaccessible from there (CS0122), which is exactly
/// what shipped before this fix (<see cref="CookieRegistry" /> and
/// <see cref="CookieDeclarationMapper" /> were both internal).
/// <para>
/// This reproduces that failure mode directly rather than trusting reasoning about it: a
/// standalone Roslyn compilation of a small "probe" source file, referencing the real built
/// package DLL exactly as an external consumer would - not via this test assembly's
/// <c>InternalsVisibleTo</c>, which would hide the bug.
/// </para>
/// </summary>
public class CookiePolicyViewCompileProbeTests
{
    // Mirrors the calls Views/CookiePolicy.cshtml makes: CookieRegistry.Group(...) and
    // CookieDeclarationMapper.FromBlockList(...).
    private const string ProbeSource = """
        using System.Collections.Generic;
        using Esatto.Umbraco.Backoffice.CookieBanner;
        using Umbraco.Cms.Core.Models.Blocks;
        using Umbraco.Cms.Core.Models.PublishedContent;

        public static class ConsumerRecompiledCookiePolicyProbe
        {
            public static void UseCookieRegistry(IEnumerable<CookieDeclaration> declarations)
            {
                _ = CookieRegistry.Group(declarations);
            }

            public static void UseCookieDeclarationMapper(
                BlockListModel? blocks, IPublishedValueFallback fallback)
            {
                _ = CookieDeclarationMapper.FromBlockList(blocks, fallback);
            }
        }
        """;

    [Fact]
    public void The_packaged_views_dependencies_compile_from_outside_the_assembly()
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(ProbeSource);

        var cookieBannerPath = typeof(CookieRegistry).Assembly.Location;
        var umbracoCorePath = typeof(BlockListModel).Assembly.Location;

        List<MetadataReference> references = ReferenceAssemblyPaths()
            .Append(cookieBannerPath)
            .Append(umbracoCorePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "ConsumerRecompiledCookiePolicyProbe",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        List<Diagnostic> errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.True(
            errors.Count == 0,
            "Expected a probe calling CookieRegistry.Group and CookieDeclarationMapper.FromBlockList "
                + "to compile from OUTSIDE the package assembly - exactly what happens when Umbraco "
                + "writes Views/CookiePolicy.cshtml into a consumer app and Razor runtime compilation "
                + "recompiles it there - but got:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, errors));
    }

    /// <summary>
    /// Belt-and-braces reflection check pinning the exact defect: both types must be public, not
    /// merely internal-with-InternalsVisibleTo, because the consumer's recompiled view is not this
    /// test assembly and gets no such grant.
    /// </summary>
    [Fact]
    public void CookieRegistry_and_CookieDeclarationMapper_are_public()
    {
        Assert.True(typeof(CookieRegistry).IsPublic, $"{nameof(CookieRegistry)} must be public.");
        Assert.True(
            typeof(CookieDeclarationMapper).IsPublic,
            $"{nameof(CookieDeclarationMapper)} must be public.");
    }

    /// <summary>
    /// The .NET runtime's own assemblies, resolved the same way Roslyn scripting samples do: from
    /// the trusted platform assembly list the CoreCLR host publishes, which on a normal `dotnet
    /// test` run already includes every dependency copied to the test's output directory (the
    /// package DLL and Umbraco.Core.dll included - appended again above only so the assertion does
    /// not silently depend on that).
    /// </summary>
    private static IEnumerable<string> ReferenceAssemblyPaths()
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrEmpty(trustedPlatformAssemblies) is false)
        {
            return trustedPlatformAssemblies.Split(Path.PathSeparator);
        }

        // Fallback for a host that does not publish TRUSTED_PLATFORM_ASSEMBLIES: every assembly
        // already loaded into this process is at least enough to resolve System.Private.CoreLib,
        // System.Runtime and the rest of the probe's transitive closure.
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.IsDynamic is false)
            .Select(assembly => assembly.Location)
            .Where(location => string.IsNullOrEmpty(location) is false);
    }
}
