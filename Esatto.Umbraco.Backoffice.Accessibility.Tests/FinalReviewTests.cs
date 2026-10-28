using Esatto.Umbraco.Backoffice.Accessibility.Persistence;
using Esatto.Umbraco.Backoffice.Accessibility.Reporting;
using Esatto.Umbraco.Backoffice.Accessibility.Scanning;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Accessibility.Tests;

/// <summary>Regression tests for the final review's findings.</summary>
public class FinalReviewTests
{
    private static readonly Guid Site = Guid.NewGuid();

    private static PageResult Page(string url, string helpUrl = "https://dequeuniversity.com/rules/axe/4.14/region")
        => new(Guid.NewGuid(), "P", "", url, 1280, "ok", null, [],
            [new RuleResult("region", "violation", "moderate", "h", helpUrl, ["best-practice"], [new NodeResult("div", "<div>")])], []);

    // C2: a Content user must not be able to store javascript: links that an admin later clicks.
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,x")]
    [InlineData("/relative")]
    public async Task Page_url_must_be_absolute_http(string url)
    {
        var (service, _) = TestStore.CreateScanService();
        await Assert.ThrowsAsync<ArgumentException>(() => service.AddPageAsync(1, Page(url)));
    }

    [Fact]
    public async Task Non_http_help_url_is_blanked()
    {
        var (service, store) = TestStore.CreateScanService();
        await service.AddPageAsync(1, Page("https://a.se/x", helpUrl: "javascript:alert(1)"));
        Assert.Equal(string.Empty, store.Findings.Single().HelpUrl);
    }

    // I3: a root-relative src is a path on every OS (Unix parses "/media/…" as a file: URI).
    [Theory]
    [InlineData("/media/abc/x.jpg?width=600", "/media/abc/x.jpg")]
    [InlineData("https://a.se/media/abc/x.jpg", "/media/abc/x.jpg")]
    [InlineData("file:///media/abc/x.jpg", null)]
    [InlineData("//cdn.example.com/x.jpg", null)]
    public void Media_path_of_src(string src, string? expected) => Assert.Equal(expected, MediaResolver.MediaPathOf(src));

    // I7: the Issues list carries counts, not every element's HTML; one group's nodes load on demand.
    [Fact]
    public async Task Issue_list_omits_nodes_and_a_single_group_includes_them()
    {
        var store = new TestStore();
        var (scans, _) = TestStore.CreateScanService(store: store);
        var id = await scans.StartAsync(Site, false, 1, "A");
        await scans.AddPageAsync(id, new PageResult(Guid.NewGuid(), "P", "", "https://a.se/x", 1280, "ok", null, [],
            [new RuleResult("image-alt", "violation", "critical", "h", "https://x", ["wcag111"], [new NodeResult("img.a", "<img a>"), new NodeResult("img.b", "<img b>")])], []));
        await scans.FinishAsync(id, true);
        var report = new ReportService(store, new Knowledge.KnowledgeBase(), TestStore.Settings(store));

        var list = Assert.Single(await report.GetIssuesAsync(Site, new IssueFilter(null, null, null, null, false, null)));
        Assert.Equal((1, 2), (list.PageCount, list.ElementCount));
        Assert.Empty(list.Pages);

        var one = Assert.Single(await report.GetIssuesAsync(Site, new IssueFilter(null, null, null, null, false, null, RuleId: "image-alt", Kind: "violation")));
        Assert.Equal(["<img a>", "<img b>"], one.Pages.Single().Nodes.Select(n => n.Html));
    }

    [Fact]
    public async Task Score_and_overview_read_findings_without_html()
    {
        var store = new TestStore();
        var (scans, _) = TestStore.CreateScanService(store: store);
        var id = await scans.StartAsync(Site, false, 1, "A");
        await scans.AddPageAsync(id, Page("https://a.se/x"));
        await scans.FinishAsync(id, true);
        store.FullFindingReads = 0;

        await new ReportService(store, new Knowledge.KnowledgeBase(), TestStore.Settings(store)).GetOverviewAsync(Site);

        Assert.Equal(0, store.FullFindingReads);
    }

    // Re-graded: a typo in appsettings must not take every public page down.
    [Fact]
    public async Task Invalid_config_value_falls_back_instead_of_throwing()
    {
        var store = new TestStore();
        var settings = TestStore.Settings(store, new() { ["Esatto:Accessibility:Level"] = "AA+", ["Esatto:Accessibility:Visitor:Panel:Enabled"] = "true" });

        var effective = await settings.GetAsync();

        Assert.Equal(Knowledge.WcagLevel.AA, effective.Settings.Level);
    }
}
