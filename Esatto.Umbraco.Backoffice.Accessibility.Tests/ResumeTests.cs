using Esatto.Umbraco.Backoffice.Accessibility.Knowledge;
using Esatto.Umbraco.Backoffice.Accessibility.Reporting;
using Esatto.Umbraco.Backoffice.Accessibility.Scanning;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Accessibility.Tests;

/// <summary>Resuming an interrupted scan (the editor left the dashboard or closed the tab).</summary>
public class ResumeTests
{
    private static readonly Guid Site = Guid.NewGuid();

    private static PageResult Page(string status, string url = "https://a.se/x")
        => new(Guid.NewGuid(), "P", "", url, 1280, status, status == "ok" ? null : status, [], [], []);

    [Fact]
    public async Task Failed_pages_are_scanned_again_on_resume()
    {
        var (scans, _) = TestStore.CreateScanService();
        var id = await scans.StartAsync(Site, false, 3, "A");
        var ok = Page("ok");
        var skipped = Page("skipped");
        await scans.AddPageAsync(id, ok);
        await scans.AddPageAsync(id, skipped);
        await scans.AddPageAsync(id, Page("failed"));

        var done = await scans.DoneAsync(id);

        Assert.Equal([ok.ContentKey, skipped.ContentKey], done.Select(d => d.ContentKey));
    }

    [Fact]
    public async Task An_unfinished_scan_shows_the_pages_recorded_so_far()
    {
        var store = new TestStore();
        var (scans, _) = TestStore.CreateScanService(store: store);
        var id = await scans.StartAsync(Site, false, 4, "A");
        await scans.AddPageAsync(id, Page("ok", "https://a.se/1"));
        await scans.AddPageAsync(id, Page("ok", "https://a.se/2"));
        await scans.AddPageAsync(id, Page("skipped"));

        var overview = await new ReportService(store, new KnowledgeBase(), TestStore.Settings(store)).GetOverviewAsync(Site);

        Assert.Equal((2, 1), (overview.LastScan!.PagesDone, overview.LastScan.PagesSkipped));
    }
}
