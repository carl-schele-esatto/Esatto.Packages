using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Esatto.Umbraco.Backoffice.Build;

/// <summary>
/// The local answer to "what's new": the latest commits in the working copy, which of them are not pushed,
/// and how many files are changed but not committed.
/// </summary>
public sealed record LocalChanges(
    string? Branch,
    int UncommittedFiles,
    IReadOnlyList<string> UnpushedCommits,
    IReadOnlyList<PipelineCommit> Commits);

/// <summary>
/// Reads git directly when there is no pipeline stamp. Local only: Umbraco Cloud builds from a zip without
/// .git, so no repository is found there and the result is null.
/// </summary>
internal static partial class LocalGitLog
{
    public const int CommitCount = 20;

    private const char Field = '\x1f';
    private const char Record = '\x1e';

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public static LocalChanges? Read(string contentRoot, ILogger logger)
    {
        var repository = FindRepository(contentRoot);
        if (repository is null)
        {
            return null;
        }

        try
        {
            var log = Git(repository, "log", "--no-merges", $"--max-count={CommitCount}",
                $"--format=%H{Field}%an{Field}%aI{Field}%s{Field}%b{Record}");
            if (log is null)
            {
                return null;
            }

            // No upstream (a new local branch) makes git exit with an error code: nothing is pushed yet, but we
            // can't tell for sure.
            var unpushed = Git(repository, "rev-list", "@{upstream}..HEAD");
            // --no-optional-locks: a plain `git status` refreshes the index under index.lock, which would make a
            // `git commit` the developer types at the same moment fail. This one is only here to count files.
            var status = Git(repository, "--no-optional-locks", "status", "--porcelain");

            return new LocalChanges(
                Git(repository, "branch", "--show-current")?.Trim() is { Length: > 0 } branch ? branch : null,
                status?.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length ?? 0,
                unpushed?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [],
                Parse(log));
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            // Win32Exception: git is not on PATH. InvalidOperationException: git did not start or timed out.
            logger.LogWarning(ex, "Could not read the git log for the Build dashboard.");
            return null;
        }
    }

    /// <summary>
    /// Parses <c>git log --format=%H␟%an␟%aI␟%s␟%b␞</c>. The same format and the same trailer filter as
    /// pipeline/write-build-info.mjs, so a local and a deployed build look alike.
    /// </summary>
    public static IReadOnlyList<PipelineCommit> Parse(string output) =>
        output.Split(Record)
            .Select(r => r.Trim())
            .Where(r => r.Length > 0)
            .Select(r => r.Split(Field))
            .Where(f => f.Length >= 5)
            .Select(f => new PipelineCommit(
                f[0],
                f[3],
                StripTrailers(f[4]),
                f[1],
                DateTimeOffset.TryParse(f[2], out var date) ? date.ToUniversalTime() : null,
                Url: null))
            .ToList();

    /// <summary>Trailers such as Co-Authored-By say nothing about what changed.</summary>
    private static string? StripTrailers(string body)
    {
        var lines = body.ReplaceLineEndings("\n").Split('\n').Where(line => !Trailer().IsMatch(line.Trim()));
        var text = string.Join('\n', lines).Trim();
        return text.Length > 0 ? text : null;
    }

    [GeneratedRegex(@"^[\w-]+: .+@.+$")]
    private static partial Regex Trailer();

    private static string? FindRepository(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            // .git is a folder in an ordinary repository and a file in a worktree.
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) || File.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }
        }

        return null;
    }

    /// <summary>Stdout, or null if git exited with an error code.</summary>
    private static string? Git(string repository, params string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = repository,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        return Run(start, Timeout);
    }

    /// <summary>
    /// Stdout of the process, or null if it exited with an error code. Throws <see cref="InvalidOperationException"/> when
    /// it does not start or does not finish within <paramref name="timeout"/>. (Internal so a test can pass a short timeout.)
    /// </summary>
    internal static string? Run(ProcessStartInfo start, TimeSpan timeout)
    {
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"{start.FileName} did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();

        // One deadline for both waits: a child that outlives the process and keeps its stdout pipe open (a git helper
        // or daemon can) must not hold us past it either, so the output is not read without a bound.
        var clock = Stopwatch.StartNew();
        if (!process.WaitForExit(timeout) || !stdout.Wait(Max(timeout - clock.Elapsed, TimeSpan.Zero)))
        {
            // Nothing to kill when it is the pipe that outlived the process.
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw new InvalidOperationException(
                $"{start.FileName} {string.Join(' ', start.ArgumentList)} did not answer within {timeout.TotalSeconds} s.");
        }

        return process.ExitCode == 0 ? stdout.Result : null;
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a >= b ? a : b;
}
