using System.Net;
using Esatto.Umbraco.Backoffice.Seo.Persistence;
using Esatto.Umbraco.Backoffice.Seo.Tests.Support;
using NSubstitute;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Seo.Tests;

public class ScanRunnerTests
{
    private static Rig Healthy()
    {
        var rig = new Rig().Page("/", root: true).Page("/a");
        rig.Http.Text("https://esatto.se/robots.txt", "User-agent: *\nDisallow:")
            .Text("https://esatto.se/sitemap.xml", "<urlset><url><loc>https://esatto.se/</loc></url><url><loc>https://esatto.se/a</loc></url></urlset>", "application/xml")
            .Redirect("http://esatto.se/", "https://esatto.se/");
        rig.Http.Down.Add("www.esatto.se");
        return rig;
    }

    [Fact]
    public async Task Run_stores_pages_and_score()
    {
        var rig = Healthy();
        var id = await rig.RunAsync();
        var scan = rig.Store.Scans.Single(s => s.Id == id);

        Assert.Equal(ScanStatus.Complete, scan.Status);
        Assert.Equal(100, scan.Score);
        Assert.Equal(2, scan.PagesTotal);
        Assert.Equal(2, scan.PagesDone);
        Assert.Equal("Carl", scan.UserName);
        Assert.NotNull(scan.FinishedUtc);
        Assert.NotNull(scan.ProbeJson);
        Assert.All(rig.Store.Pages, p => Assert.NotNull(p.FactsJson));
    }

    [Fact]
    public async Task Findings_are_counted_by_severity()
    {
        var rig = Healthy().Page("/b", "<html><body><main>x</main></body></html>");
        var scanId = await rig.RunAsync();
        var scan = rig.Store.Scans.Single(s => s.Id == scanId);
        Assert.True(scan.Critical >= 1); // title-missing
        Assert.True(scan.Warning >= 1);  // description-missing, h1-missing …
        Assert.True(scan.Score < 100);
    }

    [Fact]
    public async Task Broken_internal_link_is_found()
    {
        var rig = Healthy().Page("/c", Snap.Good("/c", body: """<a href="/gone">x</a>""" + "<p>" + string.Join(' ', Enumerable.Repeat("w", 220)) + "</p>"));
        await rig.RunAsync();
        var loader = new Esatto.Umbraco.Backoffice.Seo.Scanning.SnapshotLoader(rig.Store, new(rig.Configuration));
        var snapshot = await loader.LoadAsync(rig.Store.Scans.Single().Id);
        Assert.True(snapshot!.Links["https://esatto.se/gone"].Broken);
    }

    [Fact]
    public async Task Skipped_pages_are_stored_with_reason()
    {
        var rig = Healthy();
        rig.Candidates.Add(new(Guid.NewGuid(), "Search", "en", "https://esatto.se/sok", false, true, false, false, []));
        rig.Config["Esatto:Seo:ExcludePaths:0"] = "/sok";
        var scanId = await rig.RunAsync();
        var scan = rig.Store.Scans.Single(s => s.Id == scanId);
        Assert.Equal(1, scan.PagesSkipped);
        Assert.Equal("excluded", rig.Store.Pages.Single(p => p.Url.EndsWith("/sok")).Reason);
    }

    [Fact]
    public async Task No_base_url_records_message_and_fetches_nothing()
    {
        var rig = new Rig().Page("/", url: "/", root: true);
        var scanId = await rig.RunAsync();
        var scan = rig.Store.Scans.Single(s => s.Id == scanId);
        Assert.Equal(ScanStatus.Failed, scan.Status);
        Assert.Contains("base URL", scan.Message);
        Assert.Empty(rig.Http.Requests);
    }

    [Fact]
    public async Task Relative_urls_use_configured_base_url()
    {
        var rig = new Rig().Page("/", url: "/", root: true);
        rig.Http.Html("https://esatto.se/", Snap.Good("/"));
        rig.Config["Esatto:Seo:BaseUrl"] = "https://esatto.se";
        var scanId = await rig.RunAsync();
        var scan = rig.Store.Scans.Single(s => s.Id == scanId);
        Assert.Equal(ScanStatus.Complete, scan.Status);
        Assert.Equal(1, rig.Http.Count("https://esatto.se/", HttpMethod.Get));
    }

    [Fact]
    public async Task Ten_consecutive_failures_abort_with_message()
    {
        var rig = new Rig();
        for (var i = 0; i < 15; i++) rig.Page($"/p{i}");
        rig.Http.Down.Add("esatto.se");
        var scanId = await rig.RunAsync();
        var scan = rig.Store.Scans.Single(s => s.Id == scanId);
        Assert.Equal(ScanStatus.Failed, scan.Status);
        Assert.Contains("couldn't be loaded", scan.Message);
        Assert.True(rig.Store.Pages.Count < 15);
    }

    [Fact]
    public async Task Single_failing_page_does_not_abort()
    {
        var rig = Healthy();
        rig.Http.Html("https://esatto.se/a", "boom", HttpStatusCode.InternalServerError);
        var scanId = await rig.RunAsync();
        var scan = rig.Store.Scans.Single(s => s.Id == scanId);
        Assert.Equal(ScanStatus.Complete, scan.Status);
        Assert.Equal(1, scan.PagesFailed);
        Assert.Equal(PageStatus.Failed, rig.Store.Pages.Single(p => p.Url.EndsWith("/a")).Status);
    }

    [Fact]
    public async Task Unexpected_error_is_recorded_as_failed_scan()
    {
        var rig = Healthy();
        var source = rig.Source();
        source.GetCandidatesAsync(Rig.Site).Returns<Task<IReadOnlyList<Esatto.Umbraco.Backoffice.Seo.Pages.PageCandidate>>>(_ => throw new InvalidOperationException("Wasn't able to get an UmbracoContext"));
        var runner = new Esatto.Umbraco.Backoffice.Seo.Scanning.ScanRunner(source, rig.Settings(), new(rig.Configuration), rig.Configuration, rig.Store,
            rig.Http.Fetcher(), new(rig.Http.Fetcher()), new(rig.Http.Fetcher()), Microsoft.Extensions.Logging.Abstractions.NullLogger<Esatto.Umbraco.Backoffice.Seo.Scanning.ScanRunner>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(Rig.Site, "manual", "Carl", null, null, CancellationToken.None));
        var scan = Assert.Single(rig.Store.Scans);
        Assert.Equal(ScanStatus.Failed, scan.Status);
        Assert.Contains("UmbracoContext", scan.Message);
        Assert.NotNull(scan.FinishedUtc);
    }

    [Fact]
    public async Task Cancel_marks_cancelled()
    {
        var rig = Healthy();
        using var cts = new CancellationTokenSource();
        rig.Http.OnAsync("https://esatto.se/a", async (_, ct) =>
        {
            await cts.CancelAsync();
            await Task.Delay(5000, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var id = await rig.RunAsync(cts.Token);
        Assert.Equal(ScanStatus.Cancelled, rig.Store.Scans.Single(s => s.Id == id).Status);
    }

    [Fact]
    public async Task Retention_keeps_facts_for_last_ten_scans()
    {
        var rig = Healthy();
        var runner = rig.Runner();
        var ids = new List<int>();
        for (var i = 0; i < 12; i++) ids.Add(await runner.RunAsync(Rig.Site, "manual", "Carl", null, null, CancellationToken.None));

        Assert.Equal(12, rig.Store.Scans.Count);
        Assert.DoesNotContain(rig.Store.Pages, p => p.ScanId == ids[0] || p.ScanId == ids[1]);
        Assert.Contains(rig.Store.Pages, p => p.ScanId == ids[2]);
    }
}

public class RecheckServiceTests
{
    [Fact]
    public async Task Recheck_updates_score_in_place()
    {
        var key = Guid.NewGuid();
        var rig = new Rig().Page("/", root: true).Page("/a", "<html><body></body></html>", key: key);
        var id = await rig.RunAsync();
        var before = rig.Store.Scans.Single(s => s.Id == id).Score;

        rig.Http.Html("https://esatto.se/a", Snap.Good("/a"));
        await rig.Recheck().RecheckAsync(Rig.Site, key, "en", CancellationToken.None);

        var after = rig.Store.Scans.Single(s => s.Id == id).Score;
        Assert.True(after > before, $"{before} → {after}");
        Assert.Single(rig.Store.Scans);
    }

    [Fact]
    public async Task Recheck_refreshes_cached_reports()
    {
        var key = Guid.NewGuid();
        var rig = new Rig().Page("/", root: true).Page("/a", "<html><body></body></html>", key: key);
        await rig.RunAsync();
        var reports = ReportServiceTests.Reports(rig);
        var before = (await reports.GetOverviewAsync(Rig.Site)).Score;

        rig.Http.Html("https://esatto.se/a", Snap.Good("/a"));
        await rig.Recheck().RecheckAsync(Rig.Site, key, "en", CancellationToken.None);

        var after = (await reports.GetOverviewAsync(Rig.Site)).Score;
        Assert.True(after > before, $"{before} → {after}");
    }

    [Fact]
    public async Task Recheck_unknown_page_throws_key_not_found()
    {
        var rig = new Rig().Page("/", root: true);
        await rig.RunAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => rig.Recheck().RecheckAsync(Rig.Site, Guid.NewGuid(), "en", CancellationToken.None));
    }

    [Fact]
    public async Task Recheck_without_finished_scan_throws()
        => await Assert.ThrowsAsync<InvalidOperationException>(() => new Rig().Recheck().RecheckAsync(Rig.Site, Guid.NewGuid(), "en", CancellationToken.None));
}
