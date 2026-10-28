using System.Globalization;
using System.Text;
using Esatto.Umbraco.Backoffice.Accessibility.Knowledge;
using Esatto.Umbraco.Backoffice.Accessibility.Manual;
using Esatto.Umbraco.Backoffice.Accessibility.Persistence;
using Esatto.Umbraco.Backoffice.Accessibility.Public;
using Esatto.Umbraco.Backoffice.Accessibility.Reporting;
using Esatto.Umbraco.Backoffice.Accessibility.Scanning;
using Esatto.Umbraco.Backoffice.Accessibility.Settings;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Accessibility.Tests;

/// <summary>Regression tests for the final review's minor findings.</summary>
public class MinorFixTests
{
    private static readonly Guid Site = Guid.NewGuid();
    private static readonly Guid Content = Guid.NewGuid();

    private static PageResult Page(string url = "https://a.se/1", int viewport = 1280, params NodeResult[] nodes)
        => new(Content, "P", "", url, viewport, "ok", null, [],
            nodes.Length == 0 ? [] : [new RuleResult("image-alt", "violation", "critical", "h", "https://x", ["wcag111"], nodes)], []);

    [Fact]
    public async Task Starting_a_scan_closes_the_sites_abandoned_scan_as_incomplete()
    {
        var (scans, store) = TestStore.CreateScanService();
        var abandoned = await scans.StartAsync(Site, false, 2, "A");
        await scans.AddPageAsync(abandoned, Page());
        var otherSite = await scans.StartAsync(Guid.NewGuid(), false, 1, "A");

        await scans.StartAsync(Site, false, 2, "B");

        var closed = store.Scans.Single(s => s.Id == abandoned);
        Assert.Equal(ScanStatus.Incomplete, closed.Status);
        Assert.Equal(1, closed.PagesDone);
        Assert.Equal(ScanStatus.Running, store.Scans.Single(s => s.Id == otherSite).Status);
    }

    [Fact]
    public void An_unfinished_scan_is_reported_when_there_is_nothing_better()
    {
        var newestFirst = new List<ScanEntity> { new() { Id = 2, Status = ScanStatus.Running } };
        Assert.Equal(2, ReportService.ScanForReport(newestFirst)?.Id);
    }

    [Fact]
    public async Task Posting_the_same_page_twice_keeps_one_result()
    {
        var (scans, store) = TestStore.CreateScanService();
        var id = await scans.StartAsync(Site, false, 1, "A");
        var page = Page(nodes: new NodeResult("img", "<img>"));

        await scans.AddPageAsync(id, page);
        await scans.AddPageAsync(id, page); // the client retried a post that had in fact succeeded

        Assert.Single(store.Pages);
        Assert.Single(store.Findings);
    }

    [Fact]
    public async Task Recheck_updates_the_scan_the_report_shows_even_when_it_is_incomplete()
    {
        var (scans, store) = TestStore.CreateScanService();
        var id = await scans.StartAsync(Site, false, 1, "A");
        await scans.AddPageAsync(id, Page(nodes: new NodeResult("img", "<img>")));
        await scans.FinishAsync(id, complete: false);

        Assert.Equal(id, await scans.RecheckAsync(Site, Page()));
        Assert.Empty(store.Findings);
    }

    [Fact]
    public async Task Recheck_never_writes_into_a_scan_older_than_the_report_window()
    {
        var (scans, store) = TestStore.CreateScanService();
        var old = await scans.StartAsync(Site, false, 1, "A");
        await scans.FinishAsync(old, complete: true);
        var newest = 0;
        for (var i = 0; i < Retention.Keep; i++)
        {
            newest = await scans.StartAsync(Site, false, 1, "A");
            await scans.AddPageAsync(newest, Page());
            await scans.FinishAsync(newest, complete: false);
        }

        Assert.Equal(newest, await scans.RecheckAsync(Site, Page()));
        Assert.DoesNotContain(store.Pages, p => p.ScanId == old);
    }

    [Fact]
    public async Task Manual_note_longer_than_its_column_is_rejected()
    {
        var store = new TestStore();
        var manual = new ManualService(store, new KnowledgeBase(), TestStore.Settings(store), TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(() => manual.SetAsync(Site, "1.1.1", "pass", new string('a', 2049), "A"));
    }

    [Fact]
    public async Task False_positive_note_longer_than_its_column_is_rejected()
    {
        var falsePositives = new FalsePositiveService(new TestStore(), TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(() => falsePositives.AddAsync(Site, "image-alt", Content, null, new string('a', 1025), "A"));
    }

    [Fact]
    public async Task An_empty_configured_list_pins_no_exclusions()
    {
        var store = new TestStore { SettingsJson = """{"scan":{"excludePaths":["/search"]}}""" };
        var json = """{"Esatto":{"Accessibility":{"Scan":{"ExcludePaths":[]}}}}""";
        var config = new ConfigurationBuilder().AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json))).Build();

        var effective = await new SettingsService(store, config, TimeProvider.System).GetAsync();

        Assert.Empty(effective.Settings.Scan.ExcludePaths);
        Assert.Contains("scan.excludePaths", effective.Locked);
    }

    [Fact]
    public async Task Region_text_override_matches_whatever_case_it_was_saved_in()
    {
        var store = new TestStore { SettingsJson = """{"visitor":{"panel":{"texts":{"sv-se":{"buttonLabel":"Hjälp att läsa"}}}}}""" };
        var panel = (await TestStore.Settings(store).GetAsync()).Settings.Visitor.Panel;

        Assert.Equal("Hjälp att läsa", PanelTextProvider.Get(panel, "ButtonLabel", new CultureInfo("sv-SE")));
    }

    [Fact]
    public async Task A_read_that_started_before_a_save_does_not_cache_the_old_settings()
    {
        var json = """{"level":"A"}""";
        var gate = new TaskCompletionSource();
        var reads = 0;
        var store = Substitute.For<IAccessibilityStore>();
        store.GetSettingsJsonAsync().Returns(_ => Read());
        store.SaveSettingsJsonAsync(Arg.Any<string>()).Returns(call => { json = call.Arg<string>(); return Task.CompletedTask; });
        var settings = new SettingsService(store, new ConfigurationBuilder().Build(), TimeProvider.System);

        var slowRead = settings.GetAsync(); // reads the old row, then stalls
        await settings.SaveAsync(new AccessibilitySettings { Level = WcagLevel.AAA });
        gate.SetResult();
        await slowRead;

        Assert.Equal(WcagLevel.AAA, (await settings.GetAsync()).Settings.Level);

        async Task<string?> Read()
        {
            var seen = json;
            if (reads++ == 0) await gate.Task;
            return seen;
        }
    }

    [Fact]
    public async Task There_is_no_score_before_anything_has_been_checked()
    {
        var store = new TestStore();
        var overview = await new ReportService(store, new KnowledgeBase(), TestStore.Settings(store)).GetOverviewAsync(Site);
        Assert.Null(overview.Score.Percent);
    }

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+1", "'+1")]
    [InlineData("-1", "'-1")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    public void Csv_neutralises_spreadsheet_formulas(string pageName, string expected)
    {
        var group = new IssueGroup("image-alt", "t", ["1.1.1"], WcagLevel.A, Owner.Editor, "critical", "violation", false,
            1, 1, "w", "h", "c", "https://x", [new IssuePage(Content, pageName, "sv", "https://a.se/", [new IssueNode(1, "img", "<img>", null, null)], [1280])]);

        var row = CsvWriter.Write([group]).Split("\r\n")[1];

        Assert.Contains("," + expected, row);
    }

    [Fact]
    public async Task An_issue_page_lists_every_width_it_was_scanned_at_so_recheck_covers_them_all()
    {
        var store = new TestStore();
        var (scans, _) = TestStore.CreateScanService(store: store);
        var id = await scans.StartAsync(Site, true, 2, "A");
        await scans.AddPageAsync(id, Page(viewport: 1280, nodes: new NodeResult("img", "<img>")));
        await scans.AddPageAsync(id, Page(viewport: 320));
        await scans.FinishAsync(id, true);
        var report = new ReportService(store, new KnowledgeBase(), TestStore.Settings(store));

        var group = Assert.Single(await report.GetIssuesAsync(Site, new IssueFilter(null, null, null, null, false, null, RuleId: "image-alt", Kind: "violation")));

        Assert.Equal([1280, 320], group.Pages.Single().Viewports);
    }

    [Fact]
    public void A_scan_summary_says_whether_it_scanned_the_mobile_width_so_resume_can_match_it()
        => Assert.True(ScanService.Summary(new ScanEntity { Version = "V22", Level = "AA", Mobile = true }).Mobile);

    [Fact]
    public async Task A_manual_result_can_be_cleared()
    {
        var store = new TestStore();
        var manual = new ManualService(store, new KnowledgeBase(), TestStore.Settings(store), TimeProvider.System);
        await manual.SetAsync(Site, "1.1.1", "pass", "ok", "A");

        await manual.ClearAsync(Site, "1.1.1");

        var item = (await manual.GetChecklistAsync(Site)).Single(i => i.Criterion.Id == "1.1.1");
        Assert.Null(item.Status);
        Assert.Null(item.Note);
    }
}
