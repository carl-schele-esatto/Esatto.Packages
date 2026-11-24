using System.Text;
using Esatto.Umbraco.Backoffice.Build.Tests.Support;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Build.Tests;

public sealed class PipelineBuildReaderTests : IDisposable
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"build-info-{Guid.NewGuid():N}.json");
    private readonly ListLogger _logger = new();

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    [Fact]
    public void Reads_the_stamp()
    {
        File.WriteAllText(_path, $$"""
            {
              "schemaVersion": 1,
              "buildNumber": "42",
              "buildUrl": "https://ci.example.com/builds/42",
              "commit": "{{Sha}}",
              "commitMessage": "Fix the footer",
              "commitUrl": "https://git.example.com/repo/commit/{{Sha}}",
              "branch": "main",
              "targetEnvironment": "live",
              "builtAtUtc": "2026-11-20T09:12:00Z"
            }
            """);

        var build = PipelineBuildReader.Read(_path, _logger);

        Assert.NotNull(build);
        Assert.Equal(1, build.SchemaVersion);
        Assert.Equal("42", build.BuildNumber);
        Assert.Equal("https://ci.example.com/builds/42", build.BuildUrl);
        Assert.Equal(Sha, build.Commit);
        Assert.Equal("Fix the footer", build.CommitMessage);
        Assert.Equal($"https://git.example.com/repo/commit/{Sha}", build.CommitUrl);
        Assert.Equal("main", build.Branch);
        Assert.Equal("live", build.TargetEnvironment);
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 9, 12, 0, TimeSpan.Zero), build.BuiltAtUtc);
        Assert.Null(build.Changes);
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public void Reads_the_commits_since_the_previous_deployment()
    {
        File.WriteAllText(_path, $$"""
            {
              "buildNumber": "42",
              "commit": "{{Sha}}",
              "changes": {
                "sinceCommit": "fedcba9876543210fedcba9876543210fedcba98",
                "sinceBuildNumber": "41",
                "sinceBuildUrl": "https://ci.example.com/builds/41",
                "truncated": false,
                "commits": [
                  {
                    "sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    "subject": "Fix the footer",
                    "body": "The links wrapped on narrow screens.",
                    "author": "Ada Example",
                    "dateUtc": "2026-11-19T15:30:00Z",
                    "url": "https://git.example.com/repo/commit/aaaaaaaa"
                  }
                ]
              }
            }
            """);

        var build = PipelineBuildReader.Read(_path, _logger);

        Assert.NotNull(build);
        var changes = build.Changes;
        Assert.NotNull(changes);
        Assert.Equal("fedcba9876543210fedcba9876543210fedcba98", changes.SinceCommit);
        Assert.Equal("41", changes.SinceBuildNumber);
        Assert.Equal("https://ci.example.com/builds/41", changes.SinceBuildUrl);
        Assert.False(changes.Truncated);
        var commit = Assert.Single(changes.Commits);
        Assert.Equal("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", commit.Sha);
        Assert.Equal("Fix the footer", commit.Subject);
        Assert.Equal("The links wrapped on narrow screens.", commit.Body);
        Assert.Equal("Ada Example", commit.Author);
        Assert.Equal(new DateTimeOffset(2026, 11, 19, 15, 30, 0, TimeSpan.Zero), commit.DateUtc);
        Assert.Equal("https://git.example.com/repo/commit/aaaaaaaa", commit.Url);
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public void A_change_list_without_commits_reads_as_an_empty_list()
    {
        File.WriteAllText(_path, """
            { "buildNumber": "42", "commit": "abc", "changes": { "sinceCommit": "abc" } }
            """);

        var build = PipelineBuildReader.Read(_path, _logger);

        Assert.NotNull(build);
        Assert.NotNull(build.Changes);
        Assert.Equal("abc", build.Changes.SinceCommit);
        Assert.NotNull(build.Changes.Commits);
        Assert.Empty(build.Changes.Commits);
        Assert.Empty(_logger.Entries);
    }

    private IReadOnlyList<PipelineCommit> ReadCommits(string commitsJson)
    {
        File.WriteAllText(_path, $$"""
            { "buildNumber": "42", "commit": "abc", "changes": { "sinceCommit": "abc", "commits": {{commitsJson}} } }
            """);

        var build = PipelineBuildReader.Read(_path, _logger);

        Assert.NotNull(build);
        Assert.NotNull(build.Changes);
        return build.Changes.Commits;
    }

    [Fact]
    public void A_null_commit_entry_is_dropped_and_leaves_an_empty_list()
    {
        var commits = ReadCommits("[null]");

        Assert.NotNull(commits);
        Assert.Empty(commits);
        Assert.Empty(_logger.Entries);
    }

    [Theory]
    [InlineData("""{ "subject": "No sha" }""")]
    [InlineData("""{ "sha": null, "subject": "Null sha" }""")]
    [InlineData("""{ "sha": "", "subject": "Empty sha" }""")]
    [InlineData("""{ "sha": "   ", "subject": "Whitespace sha" }""")]
    [InlineData("""{ "sha": "aaaaaaa" }""")]
    [InlineData("""{ "sha": "aaaaaaa", "subject": null }""")]
    [InlineData("""{ "sha": "aaaaaaa", "subject": "" }""")]
    [InlineData("""{ "sha": "aaaaaaa", "subject": "  " }""")]
    public void A_commit_without_a_sha_or_a_subject_is_dropped(string commitJson)
    {
        var commits = ReadCommits($"[{commitJson}]");

        Assert.Empty(commits);
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public void Valid_commits_are_kept_in_order_around_the_dropped_ones_without_logging()
    {
        var commits = ReadCommits("""
            [
              { "sha": "aaaaaaa", "subject": "First" },
              null,
              { "subject": "No sha" },
              { "sha": "bbbbbbb", "subject": "Second", "body": "Why.", "author": "Ada Example" }
            ]
            """);

        Assert.Collection(
            commits,
            first =>
            {
                Assert.Equal("aaaaaaa", first.Sha);
                Assert.Equal("First", first.Subject);
            },
            second =>
            {
                Assert.Equal("bbbbbbb", second.Sha);
                Assert.Equal("Second", second.Subject);
                Assert.Equal("Why.", second.Body);
                Assert.Equal("Ada Example", second.Author);
            });
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public void A_missing_file_is_no_build_and_logs_nothing()
    {
        var build = PipelineBuildReader.Read(_path, _logger);

        Assert.Null(build);
        Assert.Empty(_logger.Entries);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("""{"buildNumber":"42"}""")]
    [InlineData("""{"commit":"abc"}""")]
    [InlineData("""{"buildNumber":"42","commit":"  "}""")]
    [InlineData("""{"buildNumber":"42","commit":"abc","changes":[]}""")]
    [InlineData("""{"buildNumber":"42","commit":"abc","builtAtUtc":123}""")]
    public void A_broken_or_incomplete_file_is_no_build_and_logs_one_warning(string content)
    {
        File.WriteAllText(_path, content);

        var build = PipelineBuildReader.Read(_path, _logger);

        Assert.Null(build);
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(Path.GetFileName(_path), entry.Message);
    }

    [Fact]
    public void A_file_that_cannot_be_read_is_no_build_and_logs_one_warning()
    {
        File.WriteAllText(_path, """{"buildNumber":"42","commit":"abc"}""");
        using var exclusive = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var build = PipelineBuildReader.Read(_path, _logger);

        Assert.Null(build);
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
    }

    [Fact]
    public void Reads_a_file_saved_with_a_utf8_bom()
    {
        var json = Encoding.UTF8.GetBytes("""{"buildNumber":"42","commit":"abc"}""");
        File.WriteAllBytes(_path, [0xEF, 0xBB, 0xBF, .. json]);

        var build = PipelineBuildReader.Read(_path, _logger);

        Assert.NotNull(build);
        Assert.Equal("42", build.BuildNumber);
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public void Ignores_unknown_fields()
    {
        File.WriteAllText(_path, """
            { "buildNumber": "42", "commit": "abc", "somethingNew": { "a": 1 } }
            """);

        var build = PipelineBuildReader.Read(_path, _logger);

        Assert.NotNull(build);
        Assert.Equal("abc", build.Commit);
        Assert.Empty(_logger.Entries);
    }
}
