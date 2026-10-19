using Microsoft.Playwright;
using Esatto.CookieScan.Core;

namespace Esatto.CookieScan.Engine;

/// <summary>One thing found in the browser, before a pass is attributed to it.</summary>
public sealed record CapturedEntry(string Name, StorageKind Storage, DateTimeOffset? Expires);

/// <summary>What one page visit produced.</summary>
public sealed record PageObservation(IReadOnlyList<CapturedEntry> Entries, IReadOnlySet<string> Hosts);

/// <summary>
/// Visits one page and reads back everything the browser now holds.
/// </summary>
public static class PageCapture
{
    public static async Task<PageObservation> VisitAsync(IPage page, Uri url, ISet<string> hosts, IScanLog log)
    {
        try
        {
            // NetworkIdle rather than DOMContentLoaded: a third-party tag that sets a cookie
            // usually loads after the document is parsed, and stopping earlier would miss exactly
            // the cookies this tool exists to find.
            await page.GotoAsync(
                url.ToString(),
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30_000 });
        }
        catch (PlaywrightException error)
        {
            // Nothing is read on a failed navigation, deliberately. The page is still sitting on
            // the PREVIOUS url, so a storage read here is same-origin, does not throw, and returns
            // the previous page's keys labelled as this one's. A cookie would be misattributed the
            // same way - the caller records the url an entry was first seen at, and that would name
            // a page which never loaded. Anything genuinely set by the previous page's late
            // resources is still in the context and gets picked up by the next successful visit.
            log.Warning(
                $"  skipped {url} - it did not load ({error.Message.Split('\n')[0]})");

            return new PageObservation([], new HashSet<string>(hosts, StringComparer.OrdinalIgnoreCase));
        }

        await RevealEmbedsAsync(page, log);

        List<CapturedEntry> entries = [];

        // Read from the context, not the page: a cookie set for the whole site by an earlier page
        // in this pass belongs to this pass, and reading per-page would keep re-finding it.
        foreach (BrowserContextCookiesResult cookie in await page.Context.CookiesAsync())
        {
            entries.Add(new CapturedEntry(
                cookie.Name,
                StorageKind.Cookie,
                // Playwright reports -1 for a session cookie.
                cookie.Expires < 0 ? null : DateTimeOffset.FromUnixTimeSeconds((long)cookie.Expires)));
        }

        // Every frame, not only the page: an embed's player or map keeps its storage in its own
        // origin's frame, where the page's own localStorage never shows it.
        foreach (IFrame frame in page.Frames)
        {
            entries.AddRange(await KeysAsync(frame, "localStorage", StorageKind.LocalStorage, log));
            entries.AddRange(await KeysAsync(frame, "sessionStorage", StorageKind.SessionStorage, log));
        }

        return new PageObservation(
            [.. entries.DistinctBy(entry => (entry.Name, entry.Storage))],
            new HashSet<string>(hosts, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The click-to-load video facades the scanner activates. Nothing else on a page is ever clicked.
    /// </summary>
    /// <remarks>
    /// A facade is a poster standing in for a player until the visitor presses play, so a plain
    /// page load never shows what the player stores. These are the common open-source facades plus
    /// the data-youtube-id convention the Esatto sites use. Pressing one is what a visitor does to
    /// watch a video; it submits nothing. On a site that gates the player on consent, pressing it
    /// without that consent should open the banner and load nothing - which is exactly what the
    /// reject passes then prove.
    /// </remarks>
    public const string VideoFacades = "[data-youtube-id], lite-youtube, lite-vimeo";

    // A page that keeps growing as it is scrolled would otherwise be scrolled for ever.
    private const int MaxScrollSteps = 60;

    private static readonly TimeSpan QuietFor = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Scrolls the page top to bottom, so lazy-loaded embeds (a map, an iframe with
    /// loading="lazy") load, then presses every <see cref="VideoFacades"/> match, then waits for the
    /// network to go quiet again.
    /// </summary>
    private static async Task RevealEmbedsAsync(IPage page, IScanLog log)
    {
        int inFlight = 0;
        DateTime lastActivity = DateTime.UtcNow;
        void Started(object? sender, IRequest request) { Interlocked.Increment(ref inFlight); lastActivity = DateTime.UtcNow; }
        void Ended(object? sender, IRequest request) { Interlocked.Decrement(ref inFlight); lastActivity = DateTime.UtcNow; }

        page.Request += Started;
        page.RequestFinished += Ended;
        page.RequestFailed += Ended;
        try
        {
            await page.EvaluateAsync(
                """
                async ([maxSteps, facades]) => {
                  const step = Math.max(200, Math.floor(innerHeight * 0.8));
                  for (let i = 0; i < maxSteps && scrollY + innerHeight < document.documentElement.scrollHeight; i++) {
                    scrollBy(0, step);
                    await new Promise((resolve) => setTimeout(resolve, 100));
                  }
                  // A script click rather than a pointer click: a consent dialog left open by an
                  // undecided pass covers the page, and a pointer click would wait on it forever.
                  document.querySelectorAll(facades).forEach((facade) => facade.click());
                }
                """,
                new object[] { MaxScrollSteps, VideoFacades });
        }
        catch (PlaywrightException error)
        {
            // Not fatal: the page has loaded and is still read. Logged, because a page that could
            // not be scrolled may hold embeds this scan then never saw.
            log.Warning($"  could not scroll or press play on {page.Url} ({error.Message.Split('\n')[0]})");
        }

        // NetworkIdle has already been reached for this navigation, so waiting on it again returns at
        // once; what is wanted here is quiet after the requests the scroll and the clicks just started.
        DateTime deadline = DateTime.UtcNow + MaxWait;
        while (DateTime.UtcNow < deadline
               && (Volatile.Read(ref inFlight) > 0 || DateTime.UtcNow - lastActivity < QuietFor))
        {
            await Task.Delay(100);
        }

        page.Request -= Started;
        page.RequestFinished -= Ended;
        page.RequestFailed -= Ended;
    }

    private static async Task<IReadOnlyList<CapturedEntry>> KeysAsync(
        IFrame frame, string store, StorageKind kind, IScanLog log)
    {
        try
        {
            string[] keys = await frame.EvaluateAsync<string[]>($"() => Object.keys({store})");

            // Neither store has an expiry; DurationFormatter decides the wording from the kind.
            return keys.Select(key => new CapturedEntry(key, kind, null)).ToArray();
        }
        catch (PlaywrightException error)
        {
            // Storage access legitimately throws on an opaque origin and on an error page, so this
            // is not fatal. It is still logged: the same exception type covers "execution context
            // was destroyed" and a closed target, and an unlogged catch here would make a real
            // capture failure look identical to a page that simply stores nothing - which is
            // under-reporting with no trace, the one outcome this tool must never produce.
            log.Warning(
                $"  could not read {store} on {frame.Url} ({error.Message.Split('\n')[0]})");

            return [];
        }
    }

    /// <summary>
    /// Records the host of every request the page makes, for the report's third-party section.
    /// Attach once per context, before any navigation.
    /// </summary>
    public static void RecordHosts(IPage page, ISet<string> hosts, Uri root)
    {
        page.Request += (_, request) =>
        {
            if (Uri.TryCreate(request.Url, UriKind.Absolute, out Uri? uri)
                && uri.Host.Equals(root.Host, StringComparison.OrdinalIgnoreCase) is false)
            {
                hosts.Add(uri.Host);
            }
        };
    }
}
