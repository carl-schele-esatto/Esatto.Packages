using System.Diagnostics;
using Esatto.Umbraco.Backoffice.Seo.Checks;
using Esatto.Umbraco.Backoffice.Seo.Evaluation;
using Esatto.Umbraco.Backoffice.Seo.Facts;
using Esatto.Umbraco.Backoffice.Seo.Pages;
using Esatto.Umbraco.Backoffice.Seo.Persistence;
using Esatto.Umbraco.Backoffice.Seo.Reporting;
using Esatto.Umbraco.Backoffice.Seo.Scanning;
using Esatto.Umbraco.Backoffice.Seo.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Seo.Tests;

/// <summary>The minor findings of the final review, fixed after the first build. Numbers are the ledger's.</summary>
public class DeferredMinorsTests
{
    // #8 ReportCache: one build per key at a time, no stale generations kept, never refuses new entries.

    [Fact]
    public async Task Report_cache_builds_once_for_concurrent_requests()
    {
        var cache = new ReportCache();
        var calls = 0;
        var gate = new TaskCompletionSource();
        async Task<string> Create() { Interlocked.Increment(ref calls); await gate.Task; return "v"; }

        var a = cache.GetOrAddAsync("k", Create);
        var b = cache.GetOrAddAsync("k", Create);
        gate.SetResult();
        await Task.WhenAll(a, b);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Report_cache_drops_everything_on_invalidate()
    {
        var cache = new ReportCache();
        await cache.GetOrAddAsync("k", () => Task.FromResult("v"));
        cache.Invalidate();
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public async Task Report_cache_keeps_caching_past_its_limit()
    {
        var cache = new ReportCache();
        var calls = 0;
        for (var i = 0; i < 3 * ReportCache.MaxEntries; i++)
        {
            var key = $"k{i}";
            await cache.GetOrAddAsync(key, () => { calls++; return Task.FromResult(key); });
            await cache.GetOrAddAsync(key, () => { calls++; return Task.FromResult(key); });
        }

        Assert.Equal(3 * ReportCache.MaxEntries, calls);
        Assert.True(cache.Count <= ReportCache.MaxEntries);
    }

    [Fact]
    public async Task Report_cache_does_not_keep_a_failed_build()
    {
        var cache = new ReportCache();
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrAddAsync<string>("k", () => throw new InvalidOperationException()));
        Assert.Equal("v", await cache.GetOrAddAsync("k", () => Task.FromResult("v")));
    }

    [Fact]
    public async Task Report_cache_entries_expire_when_unused()
    {
        var time = new FakeTimeProvider();
        var cache = new ReportCache(time);
        var calls = 0;
        await cache.GetOrAddAsync("k", () => { calls++; return Task.FromResult("v"); });
        time.Advance(ReportCache.Expiry + TimeSpan.FromSeconds(1));
        await cache.GetOrAddAsync("k", () => { calls++; return Task.FromResult("v"); });
        Assert.Equal(2, calls);
    }

    // #10 The "delay between requests" setting also spaces link checks and site probes.

    [Fact]
    public async Task Delay_applies_to_link_checks_and_probes()
    {
        var links = string.Concat(Enumerable.Range(1, 3).Select(i => $"<a href=\"/gone{i}\">x</a>"));
        var rig = new Rig().Page("/", Snap.Good("/", body: "<p>text</p>" + links), root: true);
        rig.Config["Esatto:Seo:DelayMs"] = "100";

        var watch = Stopwatch.StartNew();
        await rig.RunAsync();

        // 1 page + 3 links + at least 4 probe requests (robots, sitemap, http, www) = 8 waits of 100 ms.
        Assert.True(watch.ElapsedMilliseconds >= 700, $"took {watch.ElapsedMilliseconds} ms");
    }

    // #11 A scan interrupted by the site shutting down is "incomplete", not "failed" or "cancelled".

    [Fact]
    public async Task Shutdown_during_scan_leaves_it_incomplete()
    {
        var (rig, reached) = HangingSite();
        using var stopping = new CancellationTokenSource();
        var run = rig.Runner().RunAsync(Rig.Site, "manual", "Carl", null, null, stopping.Token, stopping.Token);
        await reached.Task;
        await stopping.CancelAsync();
        await run;
        Assert.Equal(ScanStatus.Incomplete, rig.Store.Scans.Single().Status);
    }

    [Fact]
    public async Task Coordinator_stops_scans_when_the_application_stops()
    {
        var (rig, reached) = HangingSite();
        using var stopping = new CancellationTokenSource();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(stopping.Token);
        var services = new ServiceCollection().AddScoped(_ => rig.Runner()).BuildServiceProvider();
        var coordinator = new ScanCoordinator(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<ScanCoordinator>.Instance, lifetime);

        Assert.True(coordinator.TryStart(Rig.Site, ScanTrigger.Manual, "Carl", null));
        await reached.Task;
        await stopping.CancelAsync();
        for (var i = 0; i < 100 && coordinator.IsRunning(Rig.Site); i++) await Task.Delay(20);

        Assert.False(coordinator.IsRunning(Rig.Site));
        Assert.Equal(ScanStatus.Incomplete, rig.Store.Scans.Single().Status);
    }

    private static (Rig, TaskCompletionSource) HangingSite()
    {
        var rig = new Rig().Page("/", root: true);
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Http.OnAsync(Snap.Host + "/", async (_, ct) =>
        {
            reached.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage();
        });
        return (rig, reached);
    }

    // #13 Re-check and ignore don't overwrite each other's update of the scan row, and a re-check that is
    // cancelled (the editor navigates away) leaves nothing half-written.

    [Fact]
    public async Task Ignore_waits_for_a_recheck_that_is_writing()
    {
        var key = Guid.NewGuid();
        var rig = new Rig().Page("/", root: true, key: key);
        await rig.RunAsync();

        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Store.BeforeUpdatePage = async () => { reached.TrySetResult(); await gate.Task; };

        var recheck = rig.Recheck().RecheckAsync(Rig.Site, key, "en", CancellationToken.None);
        await reached.Task;
        var ignore = ReportServiceTests.Ignores(rig).AddAsync(Rig.Site, CheckIds.TitleMissing, null, null, "We know", "Carl");
        await Task.Delay(200);
        Assert.False(ignore.IsCompleted, "the ignore re-scored the scan while the re-check was writing it");

        gate.SetResult();
        await Task.WhenAll(recheck, ignore);
    }

    [Fact]
    public async Task Cancelled_recheck_changes_nothing()
    {
        var key = Guid.NewGuid();
        var rig = new Rig().Page("/", root: true, key: key);
        await rig.RunAsync();
        var before = rig.Store.Pages.Single().FactsJson;

        // The page now links somewhere slow; the editor leaves while that link is checked.
        rig.Http.Html(Snap.Host + "/", Snap.Good("/", body: "<p>text</p><a href=\"/slow\">slow</a>"));
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Http.OnAsync(Snap.Host + "/slow", async (_, ct) =>
        {
            reached.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage();
        });

        using var cts = new CancellationTokenSource();
        var recheck = rig.Recheck().RecheckAsync(Rig.Site, key, "en", cts.Token);
        await reached.Task;
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => recheck);

        Assert.Equal(before, rig.Store.Pages.Single().FactsJson);
    }

    // #14 A robots.txt pattern that makes regexes backtrack can't break a scan or a report.

    [Fact]
    public void Pathological_robots_pattern_is_answered()
    {
        var pattern = "/" + string.Concat(Enumerable.Repeat("*a", 40)) + "b";
        var robots = RobotsTxt.Parse($"User-agent: *\nDisallow: {pattern}\n");
        var watch = Stopwatch.StartNew();
        Assert.True(robots.IsAllowed("/" + new string('a', 20_000)));
        Assert.True(watch.ElapsedMilliseconds < 1000, $"took {watch.ElapsedMilliseconds} ms");
    }

    [Theory]
    [InlineData("/private", "/private/x", false)]
    [InlineData("/private", "/privatex", false)]
    [InlineData("/private$", "/private", false)]
    [InlineData("/private$", "/private/x", true)]
    [InlineData("/*.pdf$", "/files/a.pdf", false)]
    [InlineData("/*.pdf$", "/files/a.pdf?x=1", true)]
    [InlineData("/a*b*c", "/a-x-b-y-c-z", false)]
    [InlineData("/a*b*c", "/a-x-c-y-b", true)]
    [InlineData("/$x", "/$x", false)]
    [InlineData("/A", "/a", true)]
    public void Robots_wildcards_match_like_googlebot(string disallow, string path, bool allowed)
        => Assert.Equal(allowed, RobotsTxt.Parse($"User-agent: *\nDisallow: {disallow}\n").IsAllowed(path));

    // #15 Sitemaps up to the protocol's 50 MB are read whole; an index with more children than are read
    // (or a child that doesn't load) makes the sitemap partial, and then no page is called missing from it.

    [Fact]
    public async Task Sitemap_over_5_mb_is_read_whole()
    {
        const int count = 120_000;
        var xml = "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">"
            + string.Concat(Enumerable.Range(0, count).Select(i => $"<url><loc>https://site.se/page-number-{i:D6}/</loc></url>"))
            + "</urlset>";
        Assert.True(xml.Length > PageFetcher.MaxBodyBytes);
        var http = new FakeHttp().Text("https://site.se/sitemap.xml", xml, "application/xml");

        var p = await new SiteProber(http.Fetcher()).ProbeAsync([new Uri("https://site.se/")], CancellationToken.None);

        Assert.True(p.SitemapFound);
        Assert.Equal(count, p.SitemapUrls.Count);
        Assert.False(p.SitemapPartial);
    }

    [Fact]
    public async Task Index_with_too_many_children_is_partial()
    {
        var children = Enumerable.Range(0, SiteProber.MaxChildSitemaps + 1).Select(i => $"https://site.se/sitemap-{i}.xml").ToList();
        var http = new FakeHttp().Text("https://site.se/sitemap.xml",
            "<sitemapindex>" + string.Concat(children.Select(c => $"<sitemap><loc>{c}</loc></sitemap>")) + "</sitemapindex>", "application/xml");
        foreach (var c in children) http.Text(c, $"<urlset><url><loc>{c}.page</loc></url></urlset>", "application/xml");

        var p = await new SiteProber(http.Fetcher()).ProbeAsync([new Uri("https://site.se/")], CancellationToken.None);

        Assert.True(p.SitemapFound);
        Assert.True(p.SitemapPartial);
    }

    [Fact]
    public async Task Index_with_a_child_that_does_not_load_is_partial()
    {
        var http = new FakeHttp()
            .Text("https://site.se/sitemap.xml", "<sitemapindex><sitemap><loc>https://site.se/a.xml</loc></sitemap><sitemap><loc>https://site.se/gone.xml</loc></sitemap></sitemapindex>", "application/xml")
            .Text("https://site.se/a.xml", "<urlset><url><loc>https://site.se/a</loc></url></urlset>", "application/xml");

        var p = await new SiteProber(http.Fetcher()).ProbeAsync([new Uri("https://site.se/")], CancellationToken.None);

        Assert.True(p.SitemapPartial);
        Assert.Equal(["https://site.se/a"], p.SitemapUrls);
    }

    [Fact]
    public void Partial_sitemap_calls_no_page_missing()
    {
        var s = Snap.Site().Page("/", root: true).Page("/a").Sitemap("/").SitemapPartial().Build();
        Assert.False(Snap.Applies(new SitemapMissingPage(), s, "/a"));
    }

    // #16 Pages in windows-1252 are decoded as such.

    [Fact]
    public async Task Windows_1252_page_is_decoded()
    {
        byte[] html = [.. "<html><body>Caf"u8, 0xE9, (byte)' ', 0x80, .. "</body></html>"u8];
        var http = new FakeHttp().Bytes("https://site.se/", html, "text/html; charset=windows-1252");
        var r = await http.Fetcher().FetchAsync("https://site.se/", null, CancellationToken.None);
        Assert.Contains("Café €", r.Body);
    }

    // #17 A 200 page that isn't HTML has one explanation, on the page row and in the check.

    [Fact]
    public async Task Non_html_page_row_says_not_an_html_page()
    {
        var rig = new Rig().Page("/", root: true);
        rig.Candidates.Add(new PageCandidate(Guid.NewGuid(), "File", "en", Snap.Host + "/file", false, true, false, false, []));
        rig.Http.Text(Snap.Host + "/file", "just text");
        await rig.RunAsync();
        Assert.Equal("Not an HTML page", rig.Store.Pages.Single(p => p.Url.EndsWith("/file")).Reason);
    }

    // #19 Umbraco builds a domain bound without a scheme as http:// outside a request (verified on 18.3.1).
    // A page whose only redirect is its own https upgrade is recorded at its https address.

    [Fact]
    public async Task Page_reached_only_through_its_https_upgrade_is_recorded_at_https()
    {
        var rig = new Rig();
        rig.Candidates.Add(new PageCandidate(Guid.NewGuid(), "Home", "en", "http://esatto.se/om/", false, true, true, false, []));
        rig.Http.Redirect("http://esatto.se/om/", "https://esatto.se/om/").Html("https://esatto.se/om/", Snap.Good("/om/"));
        await rig.RunAsync();

        var row = rig.Store.Pages.Single();
        Assert.Equal("https://esatto.se/om/", row.Url);
        Assert.Empty(SnapshotLoader.Read<PageFacts>(row.FactsJson)!.RedirectChain);
    }

    [Fact]
    public async Task Page_redirecting_to_another_address_keeps_its_own()
    {
        var rig = new Rig();
        rig.Candidates.Add(new PageCandidate(Guid.NewGuid(), "Home", "en", "http://esatto.se/a", false, true, true, false, []));
        rig.Http.Redirect("http://esatto.se/a", "https://esatto.se/b").Html("https://esatto.se/b", Snap.Good("/b"));
        await rig.RunAsync();

        var row = rig.Store.Pages.Single();
        Assert.Equal("http://esatto.se/a", row.Url);
        Assert.Single(SnapshotLoader.Read<PageFacts>(row.FactsJson)!.RedirectChain);
    }
}
