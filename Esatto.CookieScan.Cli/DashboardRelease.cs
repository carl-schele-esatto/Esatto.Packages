using System.Security.Cryptography;

namespace Esatto.CookieScan.Cli;

/// <summary>What the launcher should do, once it knows what is published and what is cached.</summary>
/// <remarks>
/// A decision rather than a branch inside the launcher, so every rule this thing has - which version
/// wins, when a cached copy is good enough, what happens with no network - is decided by a pure
/// function and tested without touching GitHub or the disk. <see cref="DashboardLauncher"/> then only
/// has to carry it out.
/// </remarks>
public abstract record LaunchDecision;

/// <summary>The cached copy of this version is the one to run.</summary>
public sealed record LaunchCached(string Version) : LaunchDecision;

/// <summary>Nothing cached will do; fetch this version, then run it.</summary>
public sealed record FetchThenLaunch(string Version) : LaunchDecision;

/// <summary>Nothing can be run, and this is why.</summary>
public sealed record CannotLaunch(string Reason) : LaunchDecision;

/// <summary>
/// What asking GitHub for the published version came back with.
/// </summary>
/// <remarks>
/// Two members rather than one nullable version, because "I could not ask" and "I asked and there is
/// none" are different things to tell an operator and have different fixes - check your connection,
/// versus wait for a release. Collapsing them sends the first person to run this before any release
/// exists off to debug their network.
/// </remarks>
public sealed record ReleaseCheck(string? Latest, bool Reached)
{
    public static ReleaseCheck Unreachable => new(null, false);
}

/// <summary>
/// Where the dashboard is published, how its versions are named, and which one to run.
/// </summary>
/// <remarks>
/// The dashboard is the one half of the scanner that is not a NuGet package - a WinForms exe cannot
/// be a dotnet tool at all (the SDK refuses it: NETSDK1146, PackAsTool does not support
/// UseWindowsForms) - so it ships as a GitHub release asset and this console tool fetches it. That
/// is what makes "install the tool, run one command" possible for a window.
/// </remarks>
public static class DashboardRelease
{
    /// <summary>The repository whose releases carry the dashboard.</summary>
    public const string Repository = "carl-schele-esatto/Esatto.Packages";

    /// <summary>
    /// The tag prefix a dashboard release uses.
    /// </summary>
    /// <remarks>
    /// The same prefix MinVer reads to stamp the exe - see MinVerTagPrefix in the desktop project -
    /// so the version in a tag name, the version inside the exe and the folder it is cached under are
    /// necessarily the same string. Releases for the NuGet packages are tagged with their own package
    /// ids and are filtered out by this prefix, which is why the launcher does not use GitHub's
    /// "latest release" endpoint: that would happily hand back a package tag.
    /// </remarks>
    public const string TagPrefix = "Esatto.CookieScan.Desktop-";

    /// <summary>The asset name inside a release, and the filename on disk.</summary>
    public const string AssetName = "esatto-cookiescan-ui.exe";

    /// <summary>How many cached versions are kept. The current one and the one before it.</summary>
    /// <remarks>
    /// Two rather than one, so an update that turns out to be bad leaves something to fall back to,
    /// and rather than many, because each is roughly 90MB.
    /// </remarks>
    public const int Keep = 2;

    /// <summary>The highest STABLE version among a list of tag names, or null if there is none.</summary>
    /// <remarks>
    /// Prereleases are excluded deliberately, and <see cref="Version.TryParse"/> does it for free by
    /// refusing anything with a label: an operator running the command must not be auto-updated onto
    /// a preview. A tag can still be pinned by hand with --ui-version.
    /// <para>
    /// Tags that are not dashboard releases, and dashboard tags this build cannot parse, are skipped
    /// rather than throwing. The list comes off a public API and will contain every package release
    /// in the repository.
    /// </para>
    /// </remarks>
    public static string? Highest(IEnumerable<string> tagNames)
    {
        Version? best = null;

        foreach (string tag in tagNames)
        {
            if (tag is null || tag.StartsWith(TagPrefix, StringComparison.Ordinal) is false)
            {
                continue;
            }

            if (Version.TryParse(tag[TagPrefix.Length..], out Version? parsed) is false)
            {
                continue;
            }

            if (best is null || parsed > best)
            {
                best = parsed;
            }
        }

        return best?.ToString();
    }

    /// <summary>The highest of a set of version strings, or null for an empty or unparseable set.</summary>
    public static string? Newest(IEnumerable<string> versions)
    {
        Version? best = null;

        foreach (string version in versions)
        {
            if (Version.TryParse(version, out Version? parsed) is false)
            {
                continue;
            }

            if (best is null || parsed > best)
            {
                best = parsed;
            }
        }

        return best?.ToString();
    }

    /// <summary>Where a release's exe is downloaded from.</summary>
    public static string AssetUrl(string version)
        => $"https://github.com/{Repository}/releases/download/{TagPrefix}{version}/{AssetName}";

    /// <summary>
    /// Where the SHA-256 of that exe is published.
    /// </summary>
    /// <remarks>
    /// Beside the asset in the same release, uploaded by release-dashboard.ps1. The launcher
    /// downloads a binary and then executes it, so it verifies what it got - HTTPS says it came from
    /// GitHub, not that GitHub holds what the release intended.
    /// </remarks>
    public static string HashUrl(string version) => $"{AssetUrl(version)}.sha256";

    /// <summary>The folder that holds every cached dashboard, one subfolder per version.</summary>
    /// <remarks>
    /// Beside settings.json, the reports and the scan history, which all live under the same
    /// Esatto.CookieScan folder in LOCALAPPDATA - one place to look, and one place to delete.
    /// </remarks>
    public static string CacheRoot() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Esatto.CookieScan",
        "ui");

    /// <summary>The exe for one version, under a cache root.</summary>
    public static string CachedExe(string cacheRoot, string version)
        => Path.Combine(cacheRoot, version, AssetName);

    /// <summary>
    /// Which cached versions to delete, newest kept first.
    /// </summary>
    /// <remarks>
    /// <paramref name="inUse"/> is never returned even if it falls outside the kept window, because
    /// the caller prunes AFTER deciding what to run: deleting the exe that is about to be launched -
    /// or worse, has just been launched from - is the one mistake this function must not make.
    /// </remarks>
    public static IReadOnlyList<string> Prunable(
        IEnumerable<string> cached, string? inUse, int keep = Keep)
    {
        List<(string Text, Version Parsed)> parsed = [];

        foreach (string version in cached)
        {
            if (Version.TryParse(version, out Version? value))
            {
                parsed.Add((version, value));
            }
        }

        return
        [
            .. parsed
                .OrderByDescending(entry => entry.Parsed)
                .Skip(Math.Max(keep, 1))
                .Select(entry => entry.Text)
                .Where(version => string.Equals(version, inUse, StringComparison.OrdinalIgnoreCase) is false),
        ];
    }

    /// <summary>
    /// What to run, given what is published, what is cached, and what the operator asked for.
    /// </summary>
    /// <param name="latest">The highest published version, or null when GitHub could not be reached.</param>
    /// <param name="cached">Versions already downloaded.</param>
    /// <param name="pinned">--ui-version, or null.</param>
    /// <param name="skipUpdateCheck">--no-update.</param>
    /// <remarks>
    /// The rule worth stating out loud is the offline one: a null <paramref name="latest"/> with
    /// something cached runs the cached copy rather than failing. Once this tool has been used once it
    /// has to keep working on a train, and a launcher that refused to open a window it already had on
    /// disk because it could not ask about a newer one would be worse than no launcher.
    /// <para>
    /// <paramref name="skipUpdateCheck"/> means "do not look for a newer one", not "do not install":
    /// with nothing cached there is nothing to run, so the fetch happens anyway. The flag exists to
    /// make a launch fast and quiet, not to make it impossible.
    /// </para>
    /// </remarks>
    public static LaunchDecision Decide(
        string? latest,
        IReadOnlyList<string> cached,
        string? pinned = null,
        bool skipUpdateCheck = false,
        bool reachedGitHub = true)
    {
        // A pinned version is an instruction, not a preference: it beats what is published and what
        // is newest on disk, and it is fetched if it is missing.
        if (string.IsNullOrWhiteSpace(pinned) is false)
        {
            string wanted = pinned.Trim();

            return cached.Any(version => string.Equals(version, wanted, StringComparison.OrdinalIgnoreCase))
                ? new LaunchCached(wanted)
                : new FetchThenLaunch(wanted);
        }

        string? newestCached = Newest(cached);

        if (skipUpdateCheck && newestCached is not null)
        {
            return new LaunchCached(newestCached);
        }

        if (latest is null)
        {
            if (newestCached is not null)
            {
                return new LaunchCached(newestCached);
            }

            // The two causes have different fixes, so they get different sentences. Telling the
            // first person to run this before any release exists to check their connection would
            // send them to debug a network that is working.
            return new CannotLaunch(reachedGitHub
                ? "No dashboard release has been published yet, so there is nothing to open. "
                    + $"Releases appear at https://github.com/{Repository}/releases"
                : "The dashboard has not been downloaded yet and GitHub could not be reached, so "
                    + "there is nothing to open. Connect and run this again.");
        }

        return cached.Any(version => string.Equals(version, latest, StringComparison.OrdinalIgnoreCase))
            ? new LaunchCached(latest)
            : new FetchThenLaunch(latest);
    }

    /// <summary>The lowercase hex SHA-256 of some bytes, in the form the published file holds.</summary>
    public static string Sha256Hex(byte[] content)
        => Convert.ToHexStringLower(SHA256.HashData(content));

    /// <summary>
    /// Whether a download matches its published hash.
    /// </summary>
    /// <remarks>
    /// The published file is whatever <c>certutil</c>, <c>sha256sum</c> or PowerShell's
    /// <c>Get-FileHash</c> produced, so it may carry a filename after the digest, upper case, or
    /// trailing whitespace. Only the first token is compared, and case-insensitively - being strict
    /// about the format of a hash file would fail the verification for a reason that has nothing to
    /// do with whether the bytes are right.
    /// </remarks>
    public static bool HashMatches(byte[] content, string? publishedHash)
    {
        if (string.IsNullOrWhiteSpace(publishedHash))
        {
            return false;
        }

        string expected = publishedHash.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];

        return string.Equals(expected, Sha256Hex(content), StringComparison.OrdinalIgnoreCase);
    }
}
