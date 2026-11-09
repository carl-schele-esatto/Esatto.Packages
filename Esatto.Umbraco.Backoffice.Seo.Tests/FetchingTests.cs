using System.IO.Compression;
using System.Net;
using System.Text;
using Esatto.Umbraco.Backoffice.Seo.Evaluation;
using Esatto.Umbraco.Backoffice.Seo.Scanning;
using Esatto.Umbraco.Backoffice.Seo.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Seo.Tests;

public class PageFetcherTests
{
    [Fact]
    public async Task Follows_redirects_and_records_chain()
    {
        var http = new FakeHttp().Redirect("https://esatto.se/a", "/b").Html("https://esatto.se/b");
        var r = await http.Fetcher().FetchAsync("https://esatto.se/a", "sv", CancellationToken.None);
        Assert.Equal(200, r.StatusCode);
        Assert.Equal("https://esatto.se/b", r.FinalUrl);
        Assert.Equal(["https://esatto.se/a"], r.RedirectChain);
        Assert.Contains("<title>t</title>", r.Body);
        Assert.Null(r.Error);
    }

    [Fact]
    public async Task Redirect_loop_stops_and_reports()
    {
        var http = new FakeHttp().Redirect("https://esatto.se/a", "https://esatto.se/b").Redirect("https://esatto.se/b", "https://esatto.se/a");
        var r = await http.Fetcher().FetchAsync("https://esatto.se/a", null, CancellationToken.None);
        Assert.Equal("Redirect loop", r.Error);
        Assert.Equal(1, http.Count("https://esatto.se/a"));
    }

    [Fact]
    public async Task Too_many_redirects_stop()
    {
        var http = new FakeHttp();
        for (var i = 0; i < 12; i++) http.Redirect($"https://esatto.se/{i}", $"https://esatto.se/{i + 1}");
        var r = await http.Fetcher().FetchAsync("https://esatto.se/0", null, CancellationToken.None);
        Assert.Equal("Too many redirects", r.Error);
        Assert.Equal(PageFetcher.MaxRedirects, r.RedirectChain.Count);
    }

    [Fact]
    public async Task Caps_body_but_counts_bytes()
    {
        var big = "<html><body>" + new string('x', 6 * 1024 * 1024) + "</body></html>";
        var r = await new FakeHttp().Html("https://esatto.se/big", big).Fetcher().FetchAsync("https://esatto.se/big", null, CancellationToken.None);
        Assert.True(r.Body!.Length <= PageFetcher.MaxBodyBytes);
        Assert.True(r.Bytes >= big.Length);
    }

    [Fact]
    public async Task Sends_user_agent_language_and_shared_secret()
    {
        var http = new FakeHttp().Html("https://esatto.se/");
        await http.Fetcher(new() { ["Umbraco:CMS:BasicAuth:SharedSecret:Value"] = "s3" })
            .FetchAsync("https://esatto.se/", "sv-SE", CancellationToken.None, trustedHosts: new HashSet<string> { "esatto.se" });
        var req = http.Requests.Single();
        Assert.Equal("s3", req.Headers.GetValues("X-Authentication-Shared-Secret").Single());
        Assert.StartsWith("EsattoSeoHealth/", req.Headers.UserAgent.ToString());
        Assert.Equal("sv-SE", req.Headers.AcceptLanguage.Single().Value);
    }

    [Fact]
    public async Task Timeout_is_an_error_not_an_exception()
    {
        var http = new FakeHttp().OnAsync("https://esatto.se/slow", async (_, ct) =>
        {
            await Task.Delay(5000, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var fetcher = http.Fetcher();
        fetcher.Timeout = TimeSpan.FromMilliseconds(50);
        var r = await fetcher.FetchAsync("https://esatto.se/slow", null, CancellationToken.None);
        Assert.Equal(0, r.StatusCode);
        Assert.StartsWith("Timed out", r.Error);
    }

    [Fact]
    public async Task Cancellation_still_throws()
    {
        var http = new FakeHttp().Html("https://esatto.se/");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => http.Fetcher().FetchAsync("https://esatto.se/", null, new CancellationToken(true)));
    }

    [Fact]
    public async Task Connection_failure_is_an_error()
    {
        var http = new FakeHttp();
        http.Down.Add("nowhere.se");
        var r = await http.Fetcher().FetchAsync("https://nowhere.se/", null, CancellationToken.None);
        Assert.Equal(0, r.StatusCode);
        Assert.Contains("No such host", r.Error);
    }

    [Fact]
    public async Task Head_405_falls_back_to_get()
    {
        var http = new FakeHttp().On("https://esatto.se/x", req => new HttpResponseMessage(req.Method == HttpMethod.Head ? HttpStatusCode.MethodNotAllowed : HttpStatusCode.OK));
        var r = await http.Fetcher().FetchAsync("https://esatto.se/x", null, CancellationToken.None, headOnly: true);
        Assert.Equal(200, r.StatusCode);
        Assert.Equal(1, http.Count("https://esatto.se/x", HttpMethod.Get));
    }

    [Fact]
    public async Task Gzip_file_is_decompressed()
    {
        var r = await new FakeHttp().Bytes("https://esatto.se/s.xml.gz", Gzip("<urlset/>"), "application/x-gzip").Fetcher()
            .FetchAsync("https://esatto.se/s.xml.gz", null, CancellationToken.None);
        Assert.Equal("<urlset/>", r.Body);
    }

    [Fact]
    public async Task Latin1_charset_is_decoded()
    {
        var http = new FakeHttp().Bytes("https://esatto.se/l", Encoding.Latin1.GetBytes("<title>Åäö</title>"), "text/html; charset=iso-8859-1");
        var r = await http.Fetcher().FetchAsync("https://esatto.se/l", null, CancellationToken.None);
        Assert.Contains("Åäö", r.Body);
    }

    internal static byte[] Gzip(string text)
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Fastest)) gz.Write(Encoding.UTF8.GetBytes(text));
        return ms.ToArray();
    }
}

public class LinkCheckerTests
{
    [Fact]
    public async Task Reuses_known_and_checks_each_url_once()
    {
        var http = new FakeHttp().Html("https://esatto.se/ok").Redirect("https://esatto.se/old", "https://esatto.se/ok");
        var known = new Dictionary<string, LinkStatus> { ["https://esatto.se/page"] = new(200, "https://esatto.se/page", 0, null) };
        var result = await new LinkChecker(http.Fetcher()).CheckAsync(
            ["https://esatto.se/ok", "https://esatto.se/ok", "https://esatto.se/old", "https://esatto.se/gone", "https://esatto.se/page"],
            known, 4, CancellationToken.None);

        Assert.Equal(200, result["https://esatto.se/ok"].StatusCode);
        Assert.True(result["https://esatto.se/old"].Redirected);
        Assert.True(result["https://esatto.se/gone"].Broken);
        Assert.Equal(0, http.Count("https://esatto.se/page"));
        Assert.Equal(2, http.Count("https://esatto.se/ok", HttpMethod.Head)); // once directly, once as the redirect target
    }
}

public class SiteProberTests
{
    private static readonly Uri Esatto = new("https://esatto.se/");

    private static FakeHttp Basic() => new FakeHttp()
        .Text("https://esatto.se/robots.txt", "User-agent: *\nDisallow:")
        .Redirect("http://esatto.se/", "https://esatto.se/")
        .Html("https://esatto.se/");

    [Fact]
    public async Task Robots_and_default_sitemap()
    {
        var http = Basic().Text("https://esatto.se/sitemap.xml", "<urlset><url><loc>https://esatto.se/</loc></url></urlset>", "application/xml");
        http.Down.Add("www.esatto.se");
        var p = await new SiteProber(http.Fetcher()).ProbeAsync([Esatto], CancellationToken.None);
        Assert.True(p.RobotsFound["esatto.se"]);
        Assert.Contains("Disallow", p.RobotsText["esatto.se"]);
        Assert.True(p.SitemapFound);
        Assert.Equal(["https://esatto.se/"], p.SitemapUrls);
        Assert.True(p.HttpsRedirects);
        Assert.False(p.HostDuplicate);
    }

    [Fact]
    public async Task Sitemap_index_followed_and_gz_read()
    {
        var http = Basic()
            .Text("https://esatto.se/sitemap.xml", "<sitemapindex><sitemap><loc>https://esatto.se/s1.xml.gz</loc></sitemap></sitemapindex>", "application/xml")
            .Bytes("https://esatto.se/s1.xml.gz", PageFetcherTests.Gzip("<urlset><url><loc>https://esatto.se/a</loc></url></urlset>"), "application/gzip");
        var p = await new SiteProber(http.Fetcher()).ProbeAsync([Esatto], CancellationToken.None);
        Assert.Equal(["https://esatto.se/a"], p.SitemapUrls);
    }

    [Fact]
    public async Task Sitemap_from_robots_on_other_host()
    {
        var http = new FakeHttp()
            .Text("https://esatto.se/robots.txt", "Sitemap: https://cdn.esatto.se/map.xml")
            .Text("https://cdn.esatto.se/map.xml", "<urlset><url><loc>https://esatto.se/b</loc></url></urlset>", "text/xml");
        var p = await new SiteProber(http.Fetcher()).ProbeAsync([Esatto], CancellationToken.None);
        Assert.Equal(["https://esatto.se/b"], p.SitemapUrls);
        Assert.Equal(0, http.Count("https://esatto.se/sitemap.xml"));
    }

    [Fact]
    public async Task Sitemap_404_and_robots_404_are_not_found()
    {
        var p = await new SiteProber(new FakeHttp().Fetcher()).ProbeAsync([Esatto], CancellationToken.None);
        Assert.False(p.SitemapFound);
        Assert.False(p.RobotsFound["esatto.se"]);
        Assert.Null(p.RobotsText["esatto.se"]);
    }

    [Fact]
    public async Task No_https_redirect_and_duplicate_www()
    {
        var http = new FakeHttp().Html("http://esatto.se/").Html("https://esatto.se/").Html("https://www.esatto.se/");
        var p = await new SiteProber(http.Fetcher()).ProbeAsync([Esatto], CancellationToken.None);
        Assert.False(p.HttpsRedirects);
        Assert.True(p.HostDuplicate);
    }

    [Fact]
    public async Task Www_redirecting_to_main_host_is_not_duplicate()
    {
        var http = Basic().Redirect("https://www.esatto.se/", "https://esatto.se/");
        Assert.False((await new SiteProber(http.Fetcher()).ProbeAsync([Esatto], CancellationToken.None)).HostDuplicate);
    }

    [Fact]
    public async Task Host_that_redirects_to_its_twin_is_not_duplicate()
    {
        // Scanning from www, which forwards to the bare host: one site, not two copies.
        var http = new FakeHttp().Redirect("https://www.esatto.se/", "https://esatto.se/").Html("https://esatto.se/");
        var p = await new SiteProber(http.Fetcher()).ProbeAsync([new Uri("https://www.esatto.se/")], CancellationToken.None);
        Assert.False(p.HostDuplicate);
    }

    [Theory]
    [InlineData("https://localhost:44324/")]
    [InlineData("https://127.0.0.1/")]
    public async Task Localhost_https_and_host_not_applicable(string origin)
    {
        var p = await new SiteProber(new FakeHttp().Fetcher()).ProbeAsync([new Uri(origin)], CancellationToken.None);
        Assert.Null(p.HttpsRedirects);
        Assert.Null(p.HostDuplicate);
    }
}
