using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Esatto.Umbraco.Backoffice.Build;

/// <summary>What the CI pipeline recorded about one deployment (the contents of <c>build-info.json</c>).</summary>
public sealed record PipelineBuild(
    string BuildNumber,
    string Commit,
    string? BuildUrl,
    string? CommitMessage,
    string? CommitUrl,
    string? Branch,
    string? TargetEnvironment,
    DateTimeOffset? BuiltAtUtc,
    PipelineChanges? Changes,
    int? SchemaVersion = null);

/// <summary>The commits that went live in this deployment, relative to the previous one.</summary>
public sealed record PipelineChanges(
    string? SinceCommit,
    string? SinceBuildNumber,
    string? SinceBuildUrl,
    bool Truncated,
    IReadOnlyList<PipelineCommit> Commits)
{
    // A stamp may leave the commit list out (or send null); this way consumers can always iterate it.
    public IReadOnlyList<PipelineCommit> Commits { get; init; } = Commits ?? [];
}

public sealed record PipelineCommit(
    string Sha,
    string Subject,
    string? Body,
    string? Author,
    DateTimeOffset? DateUtc,
    string? Url);

/// <summary>Reads the build stamp the pipeline wrote next to the site. Never throws.</summary>
internal static class PipelineBuildReader
{
    public const string DefaultFileName = "build-info.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The stamp at <paramref name="path"/>, or null when there is none. A missing file is normal (the site
    /// was not deployed by the pipeline) and logs nothing; an unreadable or incomplete one logs one warning.
    /// </summary>
    public static PipelineBuild? Read(string path, ILogger logger)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            // ReadAllText detects and drops a UTF-8 byte order mark (PowerShell's Out-File writes one).
            var build = JsonSerializer.Deserialize<PipelineBuild>(File.ReadAllText(path), Options);
            if (build is null || string.IsNullOrWhiteSpace(build.BuildNumber) || string.IsNullOrWhiteSpace(build.Commit))
            {
                logger.LogWarning("Ignoring the build stamp {Path}: it needs a buildNumber and a commit.", path);
                return null;
            }

            return WithoutUnusableCommits(build);
        }
        catch (JsonException ex)
        {
            logger.LogWarning("Ignoring the build stamp {Path}: it is not valid JSON for a build stamp ({Reason}).", path, ex.Message);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("Ignoring the build stamp {Path}: it could not be read ({Reason}).", path, ex.Message);
            return null;
        }
    }

    // Only buildNumber and commit are required, so a commit entry can arrive as null or without a sha or subject. The dashboard
    // shows every entry, so drop what it cannot show here, in one place. The stamp as a whole is still good, so nothing is logged.
    private static PipelineBuild WithoutUnusableCommits(PipelineBuild build) =>
        build.Changes is { } changes
            ? build with { Changes = changes with { Commits = changes.Commits.Where(IsUsable).ToList() } }
            : build;

    private static bool IsUsable(PipelineCommit? commit) =>
        commit is not null && !string.IsNullOrWhiteSpace(commit.Sha) && !string.IsNullOrWhiteSpace(commit.Subject);
}
