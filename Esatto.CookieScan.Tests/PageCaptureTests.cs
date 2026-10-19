using Microsoft.Playwright;
using Esatto.CookieScan.Core;
using Esatto.CookieScan.Engine;

namespace Esatto.CookieScan.Tests;

/// <summary>
/// Drives a real Chromium against a site served entirely from routes, so nothing leaves the machine.
/// The page holds the two embeds a plain page load never triggers: a map iframe lazy-loaded far below
/// the fold, and a click-to-load video facade. Each embed only writes to its own frame's storage.
/// </summary>
public sealed class PageCaptureTests : IAsyncLifetime
{
    private const string Site = "https://site.test/";

    private const string SitePage = """
        <!doctype html>
        <html><body>
          <button id="other" onclick="localStorage.setItem('clicked-other', '1')">Not a video</button>
          <button type="button" data-youtube-id="abc123">Play</button>
          <div style="height: 8000px"></div>
          <iframe loading="lazy" src="https://map.test/embed" title="Map"></iframe>
          <script>
            document.addEventListener('click', (event) => {
              const poster = event.target.closest('[data-youtube-id]');
              if (!poster) return;
              const player = document.createElement('iframe');
              player.src = 'https://video.test/embed/' + poster.dataset.youtubeId;
              poster.replaceWith(player);
            });
          </script>
        </body></html>
        """;

    private static string Embed(string key) =>
        $"<!doctype html><script>localStorage.setItem('{key}', '1');</script>";

    private IPlaywright? playwright;
    private IBrowser? browser;

    public async Task InitializeAsync()
    {
        BrowserBootstrap.EnsureChromium(new NullLog());
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync();
    }

    public async Task DisposeAsync()
    {
        if (browser is not null) await browser.DisposeAsync();
        playwright?.Dispose();
    }

    private async Task<IReadOnlyList<CapturedEntry>> VisitAsync()
    {
        IBrowserContext context = await browser!.NewContextAsync();
        await context.RouteAsync("**/*", route =>
        {
            string url = route.Request.Url;
            string body = url.StartsWith(Site, StringComparison.Ordinal) ? SitePage
                : url.StartsWith("https://map.test/", StringComparison.Ordinal) ? Embed("map-key")
                : url.StartsWith("https://video.test/", StringComparison.Ordinal) ? Embed("yt-remote-device-id")
                : "";
            return route.FulfillAsync(new RouteFulfillOptions { ContentType = "text/html", Body = body });
        });

        IPage page = await context.NewPageAsync();
        PageObservation observation = await PageCapture.VisitAsync(page, new Uri(Site), new HashSet<string>(), new NullLog());
        await context.CloseAsync();
        return observation.Entries;
    }

    [Fact]
    public async Task Finds_storage_set_by_an_iframe_lazy_loaded_below_the_fold()
    {
        IReadOnlyList<CapturedEntry> entries = await VisitAsync();

        Assert.Contains(entries, entry => entry.Name == "map-key" && entry.Storage == StorageKind.LocalStorage);
    }

    [Fact]
    public async Task Finds_storage_set_by_a_click_to_load_video_once_its_facade_is_activated()
    {
        IReadOnlyList<CapturedEntry> entries = await VisitAsync();

        Assert.Contains(entries, entry => entry.Name == "yt-remote-device-id" && entry.Storage == StorageKind.LocalStorage);
    }

    [Fact]
    public async Task Clicks_nothing_but_video_facades()
    {
        IReadOnlyList<CapturedEntry> entries = await VisitAsync();

        Assert.DoesNotContain(entries, entry => entry.Name == "clicked-other");
    }

    private sealed class NullLog : IScanLog
    {
        public void Info(string message) { }

        public void Warning(string message) { }
    }
}
