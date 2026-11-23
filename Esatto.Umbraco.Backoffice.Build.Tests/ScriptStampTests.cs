using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Build.Tests;

/// <summary>
/// The other half of the round trip: pipeline/write-build-info.test.mjs asserts that the script writes exactly
/// this stamp, and this test asserts that the reader accepts it, so the two sides cannot drift apart.
/// </summary>
public sealed class ScriptStampTests
{
    private static readonly string FixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "script-stamp.json");

    [Fact]
    public void The_reader_accepts_the_stamp_the_script_writes()
    {
        var build = PipelineBuildReader.Read(FixturePath, NullLogger.Instance);

        Assert.NotNull(build);
        Assert.Equal(1, build.SchemaVersion);
        Assert.Equal("42", build.BuildNumber);
        Assert.Equal("0123456789abcdef0123456789abcdef01234567", build.Commit);
        Assert.Equal("live", build.TargetEnvironment);
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 9, 12, 0, TimeSpan.Zero), build.BuiltAtUtc);
        Assert.NotNull(build.Changes);
        Assert.Equal("41", build.Changes.SinceBuildNumber);
        Assert.False(build.Changes.Truncated);
        Assert.Equal(2, build.Changes.Commits.Count);
    }
}
