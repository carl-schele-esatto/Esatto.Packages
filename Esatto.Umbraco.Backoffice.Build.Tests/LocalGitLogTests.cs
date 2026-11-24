using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Build.Tests;

public sealed class LocalGitLogTests
{
    [Fact]
    public void Parse_matches_the_shared_fixture()
    {
        var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "git-log-sample.json"))).RootElement;
        var expected = fixture.GetProperty("commits").EnumerateArray().ToList();

        var commits = LocalGitLog.Parse(fixture.GetProperty("output").GetString()!);

        Assert.Equal(3, expected.Count);
        Assert.Equal(expected.Count, commits.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].GetProperty("sha").GetString(), commits[i].Sha);
            Assert.Equal(expected[i].GetProperty("subject").GetString(), commits[i].Subject);
            Assert.Equal(expected[i].GetProperty("body").GetString(), commits[i].Body);
            Assert.Equal(expected[i].GetProperty("author").GetString(), commits[i].Author);
            Assert.Equal(DateTimeOffset.Parse(expected[i].GetProperty("dateUtc").GetString()!), commits[i].DateUtc);
            Assert.Null(commits[i].Url);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    public void Parse_skips_blank_and_short_records(string output)
    {
        Assert.Empty(LocalGitLog.Parse(output));
    }

    [Fact]
    public void Read_returns_null_outside_a_repository()
    {
        var directory = Directory.CreateTempSubdirectory("local-git-log-");
        try
        {
            Assert.Null(LocalGitLog.Read(directory.FullName, NullLogger.Instance));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Read_lists_this_repositorys_latest_commits()
    {
        // The tests run from bin/ inside this repository (a worktree, where .git is a file).
        var changes = LocalGitLog.Read(AppContext.BaseDirectory, NullLogger.Instance);

        Assert.NotNull(changes);
        Assert.NotEmpty(changes.Commits);
        Assert.True(changes.Commits.Count <= LocalGitLog.CommitCount);
        Assert.True(changes.UncommittedFiles >= 0);
    }

    [Fact]
    public void Read_leaves_the_index_alone_so_it_cannot_collide_with_the_developers_own_git_command()
    {
        var directory = Directory.CreateTempSubdirectory("local-git-log-index-");
        try
        {
            RunGit(directory.FullName, "init", "-b", "main");
            var file = Path.Combine(directory.FullName, "page.txt");
            File.WriteAllText(file, "first");
            RunGit(directory.FullName, "add", "page.txt");
            RunGit(directory.FullName, "commit", "-m", "First change");

            // Same content, new modification time: a plain `git status` refreshes the index entry, which rewrites
            // .git/index under index.lock. With --no-optional-locks it does not, so a `git commit` typed at the
            // same moment cannot fail on the lock.
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddHours(-3));
            var index = Path.Combine(directory.FullName, ".git", "index");
            var before = File.ReadAllBytes(index);

            var changes = LocalGitLog.Read(directory.FullName, NullLogger.Instance);

            Assert.NotNull(changes);
            Assert.Equal(0, changes.UncommittedFiles);
            Assert.Equal(before, File.ReadAllBytes(index));
        }
        finally
        {
            DeleteRepository(directory);
        }
    }

    [Fact]
    public void A_child_that_keeps_the_output_pipe_open_cannot_hold_the_call_past_the_timeout()
    {
        // The process exits at once but leaves a child behind that inherited its stdout, so the pipe stays open for
        // about 6 s (a git helper or daemon can do the same). The call must give up at the timeout, not at the pipe.
        var start = new ProcessStartInfo(OperatingSystem.IsWindows() ? "cmd.exe" : "sh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add(OperatingSystem.IsWindows() ? "/c" : "-c");
        start.ArgumentList.Add(OperatingSystem.IsWindows() ? "start /b ping -n 7 127.0.0.1" : "sleep 6 &");

        var stopwatch = Stopwatch.StartNew();
        var failure = Assert.Throws<InvalidOperationException>(() => LocalGitLog.Run(start, TimeSpan.FromMilliseconds(500)));
        stopwatch.Stop();

        Assert.Contains("did not answer within", failure.Message);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(4), $"it waited {stopwatch.Elapsed.TotalSeconds:0.0} s");
    }

    private static void RunGit(string directory, params string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in new[] { "-c", "user.name=Ada Example", "-c", "user.email=ada@example.com", "-c", "commit.gpgsign=false" }.Concat(args))
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start)!;
        var stderr = process.StandardError.ReadToEndAsync();
        _ = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', args)} failed: {stderr.Result}");
    }

    /// <summary>Git writes its objects read-only, which Windows will not delete without clearing the attribute first.</summary>
    private static void DeleteRepository(DirectoryInfo directory)
    {
        foreach (var file in directory.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            file.Attributes = FileAttributes.Normal;
        }

        directory.Delete(recursive: true);
    }
}
