using Xunit;

namespace Esatto.Umbraco.Backoffice.Build.Tests;

public sealed class PublicApiTests
{
    [Fact]
    public void The_public_surface_is_exactly_what_a_consumer_needs_and_nothing_more()
    {
        // The public surface is permanent once 1.0.0 is on nuget.org: the readers and parsers that are only
        // implementation (PipelineBuildReader, LocalGitLog, PackageCatalog) stay internal. A new public type must be a decision.
        string[] expected =
        [
            nameof(BuildComposer),
            nameof(BuildInfoController),
            nameof(BuildInfoService),
            nameof(BuildInfoResponse),
            nameof(BuildOptions),
            nameof(PackageVersion),
            nameof(PipelineBuild),
            nameof(PipelineChanges),
            nameof(PipelineCommit),
            nameof(LocalChanges),
        ];

        var exported = typeof(BuildComposer).Assembly.GetExportedTypes().Select(type => type.Name);

        Assert.Equal(expected.Order(StringComparer.Ordinal), exported.Order(StringComparer.Ordinal));
    }
}
