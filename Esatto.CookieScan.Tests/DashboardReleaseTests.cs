using System.Text;

using Esatto.CookieScan.Cli;

namespace Esatto.CookieScan.Tests;

/// <summary>
/// Every rule the dashboard launcher has, decided without touching GitHub or the disk.
/// </summary>
public class DashboardReleaseTests
{
    private const string Prefix = DashboardRelease.TagPrefix;

    [Fact]
    public void The_highest_stable_dashboard_tag_wins()
    {
        string? highest = DashboardRelease.Highest([
            $"{Prefix}1.1.0",
            $"{Prefix}1.2.0",
            $"{Prefix}1.1.9",
        ]);

        Assert.Equal("1.2.0", highest);
    }

    // The tag list comes off a public API and holds every release in the repository, package tags
    // included. Anything that is not a dashboard release is skipped rather than throwing - and this
    // is also why the launcher does not use GitHub's "latest release" endpoint, which would happily
    // hand back Esatto.CookieScan.Engine-1.1.0.
    [Fact]
    public void Tags_for_other_packages_are_ignored()
    {
        string? highest = DashboardRelease.Highest([
            "Esatto.CookieScan.Engine-9.9.9",
            "Esatto.Umbraco.Backoffice.CookieScan-4.0.0",
            $"{Prefix}1.2.0",
        ]);

        Assert.Equal("1.2.0", highest);
    }

    // An operator running the command must not be auto-updated onto a preview. Version.TryParse
    // refuses a label, which is what enforces it.
    [Fact]
    public void A_prerelease_never_wins_an_automatic_update()
    {
        string? highest = DashboardRelease.Highest([
            $"{Prefix}1.2.0",
            $"{Prefix}1.3.0-preview.1",
        ]);

        Assert.Equal("1.2.0", highest);
    }

    [Fact]
    public void No_dashboard_tags_at_all_is_null_rather_than_a_guess()
    {
        Assert.Null(DashboardRelease.Highest([]));
        Assert.Null(DashboardRelease.Highest(["Esatto.CookieScan.Cli-1.1.0"]));
        Assert.Null(DashboardRelease.Highest([$"{Prefix}not-a-version"]));
    }

    [Fact]
    public void Version_ordering_is_numeric_and_not_alphabetical()
    {
        // "1.10.0" sorts before "1.9.0" as text, which is the bug this asserts against.
        Assert.Equal("1.10.0", DashboardRelease.Highest([$"{Prefix}1.9.0", $"{Prefix}1.10.0"]));
        Assert.Equal("1.10.0", DashboardRelease.Newest(["1.9.0", "1.10.0"]));
    }

    [Fact]
    public void The_asset_and_hash_urls_name_the_release_tag()
    {
        Assert.Equal(
            $"https://github.com/{DashboardRelease.Repository}/releases/download/{Prefix}1.2.0/esatto-cookiescan-ui.exe",
            DashboardRelease.AssetUrl("1.2.0"));

        Assert.Equal($"{DashboardRelease.AssetUrl("1.2.0")}.sha256", DashboardRelease.HashUrl("1.2.0"));
    }

    [Fact]
    public void A_cached_exe_lives_in_a_folder_named_for_its_version()
        => Assert.Equal(
            Path.Combine("C:", "cache", "1.2.0", "esatto-cookiescan-ui.exe"),
            DashboardRelease.CachedExe(Path.Combine("C:", "cache"), "1.2.0"));

    // ---------------------------------------------------------------- the decision

    [Fact]
    public void The_published_version_is_fetched_when_nothing_is_cached()
        => Assert.Equal(
            new FetchThenLaunch("1.2.0"),
            DashboardRelease.Decide(latest: "1.2.0", cached: []));

    [Fact]
    public void A_cached_copy_of_the_published_version_is_launched_without_downloading()
        => Assert.Equal(
            new LaunchCached("1.2.0"),
            DashboardRelease.Decide(latest: "1.2.0", cached: ["1.2.0"]));

    [Fact]
    public void A_newer_published_version_is_fetched_even_though_something_is_cached()
        => Assert.Equal(
            new FetchThenLaunch("1.3.0"),
            DashboardRelease.Decide(latest: "1.3.0", cached: ["1.1.0", "1.2.0"]));

    /// <summary>
    /// Offline, with something cached, opens the window rather than refusing.
    /// </summary>
    /// <remarks>
    /// The rule that matters most. Once this tool has been used once it has to keep working on a
    /// train; a launcher that would not open an exe it already had because it could not ask about a
    /// newer one would be worse than no launcher at all.
    /// </remarks>
    [Fact]
    public void Offline_with_a_cached_copy_launches_the_newest_cached_copy()
        => Assert.Equal(
            new LaunchCached("1.2.0"),
            DashboardRelease.Decide(latest: null, cached: ["1.1.0", "1.2.0"], reachedGitHub: false));

    [Fact]
    public void Offline_with_nothing_cached_says_why_it_cannot_open()
    {
        CannotLaunch decision = Assert.IsType<CannotLaunch>(
            DashboardRelease.Decide(latest: null, cached: [], reachedGitHub: false));

        Assert.Contains("GitHub could not be reached", decision.Reason);
    }

    /// <summary>
    /// Reached GitHub and found no release is a different sentence from could not reach GitHub.
    /// </summary>
    /// <remarks>
    /// This is the state the very first run sees, before any dashboard has been released. Telling
    /// that person to check their connection would send them to debug a network that is working.
    /// </remarks>
    [Fact]
    public void No_release_published_yet_is_not_reported_as_a_connection_problem()
    {
        CannotLaunch decision = Assert.IsType<CannotLaunch>(
            DashboardRelease.Decide(latest: null, cached: [], reachedGitHub: true));

        Assert.Contains("No dashboard release has been published", decision.Reason);
        Assert.DoesNotContain("could not be reached", decision.Reason);
    }

    [Fact]
    public void Skipping_the_update_check_launches_what_is_already_there()
        => Assert.Equal(
            new LaunchCached("1.1.0"),
            DashboardRelease.Decide(latest: "1.3.0", cached: ["1.1.0"], skipUpdateCheck: true));

    // --no-update means "do not look for a newer one", not "do not install". With nothing cached
    // there is nothing to run, so the fetch happens regardless.
    [Fact]
    public void Skipping_the_update_check_still_installs_when_nothing_is_cached()
        => Assert.Equal(
            new FetchThenLaunch("1.3.0"),
            DashboardRelease.Decide(latest: "1.3.0", cached: [], skipUpdateCheck: true));

    [Fact]
    public void A_pinned_version_beats_both_the_published_and_the_newest_cached_one()
    {
        Assert.Equal(
            new LaunchCached("1.1.0"),
            DashboardRelease.Decide(latest: "1.3.0", cached: ["1.1.0", "1.2.0"], pinned: "1.1.0"));

        Assert.Equal(
            new FetchThenLaunch("0.9.0"),
            DashboardRelease.Decide(latest: "1.3.0", cached: ["1.2.0"], pinned: "0.9.0"));
    }

    [Fact]
    public void A_pinned_prerelease_is_honoured_even_though_it_would_never_win_automatically()
        => Assert.Equal(
            new FetchThenLaunch("1.3.0-preview.1"),
            DashboardRelease.Decide(latest: "1.2.0", cached: [], pinned: "  1.3.0-preview.1  "));

    // ---------------------------------------------------------------- pruning

    [Fact]
    public void Pruning_keeps_the_two_newest()
    {
        IReadOnlyList<string> prunable = DashboardRelease.Prunable(
            ["1.0.0", "1.1.0", "1.2.0", "1.3.0"], inUse: "1.3.0");

        Assert.Equal(["1.1.0", "1.0.0"], prunable.OrderByDescending(v => v).ToArray());
    }

    [Fact]
    public void Nothing_is_prunable_while_only_two_are_cached()
        => Assert.Empty(DashboardRelease.Prunable(["1.1.0", "1.2.0"], inUse: "1.2.0"));

    /// <summary>
    /// The version about to run is never deleted, even when it falls outside the kept window.
    /// </summary>
    /// <remarks>
    /// Reachable through --ui-version: pinning an old build puts it in use while two newer ones sit
    /// above it in the cache. Deleting the exe that is being launched from is the one mistake this
    /// must not make.
    /// </remarks>
    [Fact]
    public void The_version_in_use_survives_pruning_even_when_it_is_old()
    {
        IReadOnlyList<string> prunable = DashboardRelease.Prunable(
            ["1.0.0", "1.1.0", "1.2.0", "1.3.0"], inUse: "1.0.0");

        Assert.DoesNotContain("1.0.0", prunable);
        Assert.Contains("1.1.0", prunable);
    }

    [Fact]
    public void Unparseable_cache_folders_are_left_alone_rather_than_deleted()
        => Assert.Empty(DashboardRelease.Prunable(["not-a-version", "scratch"], inUse: null));

    // ---------------------------------------------------------------- the hash

    [Fact]
    public void A_download_matching_its_published_hash_verifies()
    {
        byte[] content = Encoding.UTF8.GetBytes("pretend this is 90MB of exe");

        Assert.True(DashboardRelease.HashMatches(content, DashboardRelease.Sha256Hex(content)));
    }

    [Fact]
    public void A_tampered_download_does_not_verify()
    {
        byte[] content = Encoding.UTF8.GetBytes("pretend this is 90MB of exe");
        string hash = DashboardRelease.Sha256Hex(content);

        Assert.False(DashboardRelease.HashMatches(Encoding.UTF8.GetBytes("something else"), hash));
    }

    // Whatever produced the file - certutil, sha256sum, Get-FileHash - may have written upper case,
    // a trailing newline, or the filename after the digest. Being strict about a hash file's shape
    // would fail verification for a reason unrelated to whether the bytes are right.
    [Theory]
    [InlineData("{0}")]
    [InlineData("{0}\n")]
    [InlineData("  {0}  \r\n")]
    [InlineData("{0}  esatto-cookiescan-ui.exe")]
    [InlineData("{0} *esatto-cookiescan-ui.exe\n")]
    public void The_published_hash_is_read_in_whatever_shape_it_was_written(string format)
    {
        byte[] content = Encoding.UTF8.GetBytes("pretend this is 90MB of exe");
        string published = string.Format(format, DashboardRelease.Sha256Hex(content).ToUpperInvariant());

        Assert.True(DashboardRelease.HashMatches(content, published));
    }

    [Fact]
    public void A_missing_hash_never_counts_as_verified()
    {
        byte[] content = Encoding.UTF8.GetBytes("pretend this is 90MB of exe");

        Assert.False(DashboardRelease.HashMatches(content, null));
        Assert.False(DashboardRelease.HashMatches(content, "   "));
    }
}
