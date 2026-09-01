using System.Diagnostics;
using System.Text.Json;

using Esatto.CookieScan.Engine;

namespace Esatto.CookieScan.Cli;

/// <summary>
/// Fetches the dashboard if it is missing or out of date, then opens it.
/// </summary>
/// <remarks>
/// This is what makes "install the tool, run one command" possible for a window. The dashboard is a
/// WinForms exe and so cannot be a dotnet tool at all - the SDK refuses it outright with NETSDK1146,
/// "PackAsTool does not support UseWindowsForms" - so it ships as a GitHub release asset and this
/// console tool, which IS a dotnet tool, installs it on the operator's behalf.
/// <para>
/// Every decision it makes lives in <see cref="DashboardRelease"/> and is tested without a network
/// or a disk. What is left here is only carrying one out: an HTTP call, a file write, a process
/// start. Nothing is thrown out of <see cref="RunAsync"/> - the caller is a console entry point, and
/// a stack trace is not an answer to "the window did not open".
/// </para>
/// </remarks>
public sealed class DashboardLauncher(IScanLog log, HttpClient? client = null)
{
    /// <summary>
    /// The verb that reaches here.
    /// </summary>
    /// <remarks>
    /// Intercepted in Program before <see cref="ScanOptions.Parse"/> ever sees the arguments. Parse
    /// ignores anything not starting with "--", so a bare verb would otherwise fall straight through
    /// it and be reported as a missing --url.
    /// </remarks>
    public const string Verb = "ui";

    /// <summary>
    /// How long to wait on GitHub before giving up and using whatever is cached.
    /// </summary>
    /// <remarks>
    /// Short on purpose for the version check: it is a courtesy, not the point of the command, and an
    /// operator on a bad connection should get their window rather than a spinner. The download
    /// itself gets its own, longer allowance - see <see cref="DownloadTimeout"/> - because 90MB over
    /// a hotel connection is slow but still worth finishing.
    /// </remarks>
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(15);

    // GitHub rejects an API request with no User-Agent. The value is arbitrary; being identifiable
    // is the courtesy.
    private readonly HttpClient http = client ?? new HttpClient
    {
        DefaultRequestHeaders = { { "User-Agent", "esatto-cookiescan" } },
        Timeout = DownloadTimeout,
    };

    /// <summary>Opens the dashboard, installing or updating it first if it needs to be.</summary>
    /// <returns>0 when a window was opened, <see cref="ScanReportWriter.ExitError"/> otherwise.</returns>
    public async Task<int> RunAsync(string[] args, CancellationToken token = default)
    {
        if (OperatingSystem.IsWindows() is false)
        {
            log.Warning("The dashboard is a Windows application, so it cannot be opened on this machine.");

            return ScanReportWriter.ExitError;
        }

        string? pinned = Flag(args, "ui-version");
        bool skipUpdateCheck = args.Contains("--no-update", StringComparer.OrdinalIgnoreCase);

        string cacheRoot = DashboardRelease.CacheRoot();
        IReadOnlyList<string> cached = CachedVersions(cacheRoot);

        // Not asked for at all when a pin or --no-update makes the answer irrelevant, so those two
        // paths cost no network and no waiting. Reported as reached in that case, because nothing
        // failed - the question was simply not asked.
        ReleaseCheck check = pinned is null && (skipUpdateCheck is false || cached.Count == 0)
            ? await LatestAsync(token)
            : new ReleaseCheck(null, Reached: true);

        LaunchDecision decision = DashboardRelease.Decide(
            check.Latest, cached, pinned, skipUpdateCheck, check.Reached);

        string version;

        switch (decision)
        {
            case CannotLaunch cannot:
                log.Warning(cannot.Reason);

                return ScanReportWriter.ExitError;

            case LaunchCached launch:
                version = launch.Version;

                // Said only when it is news, and only when the check actually failed. A launch that
                // is simply up to date should be silent - this command is run to open a window, not
                // to read a report about versions.
                if (check.Reached is false && pinned is null)
                {
                    log.Warning(
                        $"Could not reach GitHub to check for a newer dashboard, so opening the "
                        + $"cached {version}.");
                }

                break;

            case FetchThenLaunch fetch:
                version = fetch.Version;

                if (await FetchAsync(cacheRoot, version, token) is false)
                {
                    return ScanReportWriter.ExitError;
                }

                break;

            default:
                log.Warning("The dashboard could not be opened.");

                return ScanReportWriter.ExitError;
        }

        string exe = DashboardRelease.CachedExe(cacheRoot, version);

        // A cache folder can be there with a half-written or hand-deleted exe inside it. Checked
        // before the launch rather than trusting the folder listing that produced the decision.
        if (File.Exists(exe) is false)
        {
            log.Warning(
                $"The cached dashboard {version} is missing its program file. Delete "
                + $"{Path.Combine(cacheRoot, version)} and run this again.");

            return ScanReportWriter.ExitError;
        }

        // After the decision and before the launch: Prunable never returns the version in use, so
        // this cannot delete what is about to run.
        Prune(cacheRoot, cached, version);

        try
        {
            // UseShellExecute so the window is a detached GUI process rather than a child holding
            // this console open. The command returns as soon as the window exists; an operator who
            // ran it from a terminal gets their prompt back.
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        }
        catch (Exception error)
        {
            log.Warning($"The dashboard could not be started: {error.Message}");

            return ScanReportWriter.ExitError;
        }

        log.Info($"Opened the cookie scanner dashboard {version}.");

        return 0;
    }

    /// <summary>The highest published version, and whether GitHub could be asked at all.</summary>
    /// <remarks>
    /// Every failure - no network, a rate limit, a proxy, malformed JSON - reports as unreachable,
    /// because the caller does the same thing with all of them: run what is cached. What it does NOT
    /// collapse is "reached it, and there is no dashboard release": that has a different fix and
    /// gets a different sentence, and it is the state the very first run sees.
    /// <para>
    /// The releases list is read rather than the "latest release" endpoint, which returns the newest
    /// release of ANY tag in the repository and would happily answer with an
    /// Esatto.CookieScan.Engine package tag. <see cref="DashboardRelease.Highest"/> filters by the
    /// dashboard's own prefix instead.
    /// </para>
    /// </remarks>
    private async Task<ReleaseCheck> LatestAsync(CancellationToken token)
    {
        try
        {
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token);

            timeout.CancelAfter(CheckTimeout);

            string json = await http.GetStringAsync(
                $"https://api.github.com/repos/{DashboardRelease.Repository}/releases?per_page=100",
                timeout.Token);

            using JsonDocument document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind is not JsonValueKind.Array)
            {
                // An answer that is not a list of releases is GitHub telling us something else - a
                // rate-limit object, most likely - so it counts as not having asked.
                return ReleaseCheck.Unreachable;
            }

            List<string> tags = [];

            foreach (JsonElement release in document.RootElement.EnumerateArray())
            {
                if (release.TryGetProperty("tag_name", out JsonElement tag)
                    && tag.ValueKind is JsonValueKind.String
                    && tag.GetString() is string name)
                {
                    tags.Add(name);
                }
            }

            // Reached, whatever the answer was. A null version from here means "there is no
            // dashboard release", which is a fact rather than a failure.
            return new ReleaseCheck(DashboardRelease.Highest(tags), Reached: true);
        }
        catch (Exception)
        {
            return ReleaseCheck.Unreachable;
        }
    }

    /// <summary>Downloads one version into the cache, verifying it before it is kept.</summary>
    /// <remarks>
    /// Written to a temporary name and moved into place only once the hash matches, so an
    /// interrupted download cannot leave a folder that looks like a cached version and is not. The
    /// hash is fetched first: a release whose .sha256 is missing is refused rather than trusted,
    /// because this method's whole job is to put an executable on disk that something will then run.
    /// </remarks>
    private async Task<bool> FetchAsync(string cacheRoot, string version, CancellationToken token)
    {
        log.Info($"Downloading the cookie scanner dashboard {version}...");

        byte[] content;
        string? published;

        try
        {
            published = await http.GetStringAsync(DashboardRelease.HashUrl(version), token);
            content = await http.GetByteArrayAsync(DashboardRelease.AssetUrl(version), token);
        }
        catch (Exception error)
        {
            log.Warning(
                $"The dashboard {version} could not be downloaded: {error.Message}. "
                + $"Check {DashboardRelease.AssetUrl(version)} in a browser.");

            return false;
        }

        if (DashboardRelease.HashMatches(content, published) is false)
        {
            log.Warning(
                $"The downloaded dashboard {version} does not match the SHA-256 published with it, "
                + "so it was discarded. Nothing was installed and nothing was run.");

            return false;
        }

        try
        {
            string folder = Path.Combine(cacheRoot, version);

            Directory.CreateDirectory(folder);

            string exe = DashboardRelease.CachedExe(cacheRoot, version);
            string partial = $"{exe}.partial";

            await File.WriteAllBytesAsync(partial, content, token);

            File.Move(partial, exe, overwrite: true);
        }
        catch (Exception error)
        {
            log.Warning($"The dashboard {version} could not be saved: {error.Message}");

            return false;
        }

        return true;
    }

    /// <summary>Every version already downloaded, by the folders the cache holds.</summary>
    /// <remarks>
    /// An unreadable or absent cache root is an empty list rather than a failure: it means nothing
    /// has been installed yet, which is exactly the first-run state this command exists to fix.
    /// </remarks>
    private static IReadOnlyList<string> CachedVersions(string cacheRoot)
    {
        try
        {
            return Directory.Exists(cacheRoot)
                ? [.. Directory.EnumerateDirectories(cacheRoot).Select(Path.GetFileName).OfType<string>()]
                : [];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Deletes the cached versions that are neither recent nor in use.</summary>
    /// <remarks>
    /// Silent about what it removes and silent when it cannot: each copy is roughly 90MB, so the
    /// pruning is worth doing, and a folder that will not delete - open in Explorer, or running -
    /// costs disk space and nothing else. It must never cost the launch that is about to happen.
    /// </remarks>
    private static void Prune(string cacheRoot, IReadOnlyList<string> cached, string inUse)
    {
        foreach (string version in DashboardRelease.Prunable(cached, inUse))
        {
            try
            {
                Directory.Delete(Path.Combine(cacheRoot, version), recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Left for next time.
            }
        }
    }

    /// <summary>One "--name value" argument, or null.</summary>
    private static string? Flag(string[] args, string name)
    {
        int index = Array.FindIndex(
            args, arg => string.Equals(arg, $"--{name}", StringComparison.OrdinalIgnoreCase));

        return index >= 0 && index + 1 < args.Length && args[index + 1].StartsWith("--") is false
            ? args[index + 1]
            : null;
    }
}
