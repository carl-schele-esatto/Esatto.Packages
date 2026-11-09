using System.Net;
using Esatto.Umbraco.Backoffice.Seo.Checks;
using Esatto.Umbraco.Backoffice.Seo.Evaluation;
using Esatto.Umbraco.Backoffice.Seo.Persistence;
using Esatto.Umbraco.Backoffice.Seo.Scanning;
using Esatto.Umbraco.Backoffice.Seo.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Seo.Tests;

/// <summary>Findings of the final whole-package review, each pinned by a test.</summary>
public class FinalReviewTests
{
    private static readonly Dictionary<string, string?> Secret = new() { ["Umbraco:CMS:BasicAuth:SharedSecret:Value"] = "s3cret" };
    private static readonly IReadOnlySet<string> SiteHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "site.se" };

    private static bool SentSecret(FakeHttp http, string host)
        => http.Requests.Where(r => r.RequestUri!.Host == host).Any(r => r.Headers.Contains("X-Authentication-Shared-Secret"));

    // Critical 1: configured headers (the basic-auth secret) only go to the scan's own trusted hosts.

    [Fact]
    public async Task Secret_not_sent_after_cross_host_redirect()
    {
        var http = new FakeHttp().Redirect("https://site.se/a", "https://evil.example/").Html("https://evil.example/");
        await http.Fetcher(Secret).FetchAsync("https://site.se/a", null, CancellationToken.None, trustedHosts: SiteHosts);
        Assert.True(SentSecret(http, "site.se"));
        Assert.False(SentSecret(http, "evil.example"));
    }

    [Fact]
    public async Task Secret_not_sent_without_trusted_hosts()
    {
        var http = new FakeHttp().Html("https://site.se/");
        await http.Fetcher(Secret).FetchAsync("https://site.se/", null, CancellationToken.None);
        Assert.False(SentSecret(http, "site.se"));
    }

    [Fact]
    public async Task Scan_does_not_send_secret_to_host_taken_from_the_request()
    {
        var rig = new Rig().Page("/", url: "/", root: true);
        foreach (var (k, v) in Secret) rig.Config[k] = v;
        rig.Http.Html("https://esatto.se/", Snap.Good("/"));
        await rig.Runner().RunAsync(Rig.Site, "manual", "Carl", new Uri("https://esatto.se/"), null, CancellationToken.None);
        Assert.Contains(rig.Http.Requests, r => r.RequestUri!.ToString() == "https://esatto.se/");
        Assert.False(SentSecret(rig.Http, "esatto.se"));
    }

    [Fact]
    public async Task Scan_sends_secret_to_configured_base_url_host()
    {
        var rig = new Rig().Page("/", url: "/", root: true);
        foreach (var (k, v) in Secret) rig.Config[k] = v;
        rig.Config["Esatto:Seo:BaseUrl"] = "https://esatto.se";
        rig.Http.Html("https://esatto.se/", Snap.Good("/"));
        await rig.RunAsync();
        Assert.True(SentSecret(rig.Http, "esatto.se"));
    }

    // Critical 2: odd URLs never throw out of FetchAsync, and the prober resolves relative sitemap addresses.

    [Theory]
    [InlineData("/sitemap.xml")]
    [InlineData("ftp://site.se/x")]
    [InlineData("not a url")]
    public async Task Fetch_of_non_http_url_is_an_error(string url)
    {
        var r = await new FakeHttp().Fetcher().FetchAsync(url, null, CancellationToken.None);
        Assert.Equal(0, r.StatusCode);
        Assert.NotNull(r.Error);
    }

    [Fact]
    public async Task Redirect_to_non_http_is_an_error()
    {
        var r = await new FakeHttp().Redirect("https://site.se/a", "mailto:a@b.se").Fetcher().FetchAsync("https://site.se/a", null, CancellationToken.None);
        Assert.NotNull(r.Error);
    }

    [Fact]
    public async Task Handler_exception_is_an_error_not_a_crash()
    {
        var http = new FakeHttp().On("https://site.se/x", _ => throw new NotSupportedException("boom"));
        var r = await http.Fetcher().FetchAsync("https://site.se/x", null, CancellationToken.None);
        Assert.Equal("boom", r.Error);
    }

    [Fact]
    public async Task Relative_sitemap_in_robots_and_relative_index_child_are_resolved()
    {
        var http = new FakeHttp()
            .Text("https://site.se/robots.txt", "Sitemap: /map/index.xml")
            .Text("https://site.se/map/index.xml", "<sitemapindex><sitemap><loc>part-1.xml</loc></sitemap></sitemapindex>", "application/xml")
            .Text("https://site.se/map/part-1.xml", "<urlset><url><loc>https://site.se/a</loc></url></urlset>", "application/xml");
        var p = await new SiteProber(http.Fetcher()).ProbeAsync([new Uri("https://site.se/")], CancellationToken.None);
        Assert.Equal(["https://site.se/a"], p.SitemapUrls);
    }

    // Important 3: a trailing-slash redirect is a redirect, not a loop.

    [Fact]
    public async Task Trailing_slash_redirect_is_not_a_loop()
    {
        var http = new FakeHttp().Redirect("https://site.se/om", "https://site.se/om/").Html("https://site.se/om/");
        var r = await http.Fetcher().FetchAsync("https://site.se/om", null, CancellationToken.None);
        Assert.Null(r.Error);
        Assert.Equal(200, r.StatusCode);
        Assert.Equal(["https://site.se/om"], r.RedirectChain);
    }

    // Important 4: real page errors after a success never abort the scan.

    [Fact]
    public async Task Many_failing_pages_after_a_success_do_not_abort()
    {
        var rig = new Rig().Page("/", root: true);
        for (var i = 0; i < 12; i++) rig.Page($"/personer/p{i}", "boom");
        for (var i = 0; i < 12; i++) rig.Http.Html($"https://esatto.se/personer/p{i}", "boom", HttpStatusCode.InternalServerError);
        rig.Config["Esatto:Seo:Parallelism"] = "1";
        var id = await rig.RunAsync();
        var scan = rig.Store.Scans.Single(s => s.Id == id);
        Assert.Equal(ScanStatus.Complete, scan.Status);
        Assert.Equal(12, scan.PagesFailed);
    }

    // Important 5: the sitemap check stays fast on big sites.

    [Fact]
    public void Sitemap_check_is_linear_on_5000_pages()
    {
        var snap = Snap.Site();
        var html = Snap.Good("/x");
        var paths = Enumerable.Range(0, 5000).Select(i => $"/p{i}").ToArray();
        foreach (var p in paths) snap.Page(p, html);
        var s = snap.Sitemap(paths).Build();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var outcomes = new SitemapMissingPage().Evaluate(s).ToList();
        Assert.Equal(5000, outcomes.Count);
        Assert.True(watch.ElapsedMilliseconds < 1500, $"{watch.ElapsedMilliseconds} ms");
    }

    // Important 6: old scans keep only their summary, list reads are summaries, and writes keep the JSON.

    [Fact]
    public async Task Pruned_scans_lose_their_json()
    {
        var rig = new Rig().Page("/", root: true);
        var runner = rig.Runner();
        var ids = new List<int>();
        for (var i = 0; i < 11; i++) ids.Add(await runner.RunAsync(Rig.Site, "manual", "Carl", null, null, CancellationToken.None));
        var oldest = await rig.Store.GetScanAsync(ids[0]);
        Assert.Null(oldest!.ProbeJson);
        Assert.Null(oldest.LinksJson);
        Assert.NotNull((await rig.Store.GetScanAsync(ids[^1]))!.LinksJson);
    }

    [Fact]
    public async Task Ignore_and_recheck_keep_the_scan_json()
    {
        var key = Guid.NewGuid();
        var rig = new Rig().Page("/", root: true).Page("/a", "<html><body></body></html>", key: key);
        var id = await rig.RunAsync();
        await ReportServiceTests.Ignores(rig).AddAsync(Rig.Site, CheckIds.TitleMissing, key, null, "note", "Carl");
        Assert.NotNull((await rig.Store.GetScanAsync(id))!.LinksJson);
        Assert.NotNull((await rig.Store.GetScanAsync(id))!.ProbeJson);
        await rig.Recheck().RecheckAsync(Rig.Site, key, "en", CancellationToken.None);
        Assert.NotNull((await rig.Store.GetScanAsync(id))!.ProbeJson);
    }

    [Fact]
    public async Task Failed_scan_without_fetchable_pages_stores_no_page_rows()
    {
        var rig = new Rig().Page("/", url: "/", root: true).Page("/a", url: "/a");
        await rig.RunAsync();
        Assert.Empty(rig.Store.Pages);
    }

    // Minor 7 (promoted): long messages are truncated to the column length.

    [Fact]
    public async Task Long_error_message_is_truncated()
    {
        var rig = new Rig().Page("/", root: true);
        var source = rig.Source();
        NSubstitute.SubstituteExtensions.Returns(source.GetCandidatesAsync(Rig.Site),
            _ => Task.FromException<IReadOnlyList<Esatto.Umbraco.Backoffice.Seo.Pages.PageCandidate>>(new InvalidOperationException(new string('x', 5000))));
        var runner = new ScanRunner(source, rig.Settings(), new(rig.Configuration), rig.Configuration, rig.Store, rig.Http.Fetcher(),
            new(rig.Http.Fetcher()), new(rig.Http.Fetcher()), Microsoft.Extensions.Logging.Abstractions.NullLogger<ScanRunner>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(Rig.Site, "manual", "Carl", null, null, CancellationToken.None));
        Assert.True(rig.Store.Scans.Single().Message!.Length <= 1024);
    }

    // Minor 9 (promoted): no answer on port 80 is not a failed https redirect.

    [Fact]
    public async Task Port_80_not_answering_is_not_applicable()
    {
        var http = new FakeHttp().Html("https://site.se/").On("http://site.se/", _ => throw new HttpRequestException("refused"));
        var p = await new SiteProber(http.Fetcher()).ProbeAsync([new Uri("https://site.se/")], CancellationToken.None);
        Assert.Null(p.HttpsRedirects);
    }
}
