using Esatto.Umbraco.Backoffice.Accessibility.Knowledge;
using Esatto.Umbraco.Backoffice.Accessibility.Persistence;
using Esatto.Umbraco.Backoffice.Accessibility.Reporting;
using Esatto.Umbraco.Backoffice.Accessibility.Scanning;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Accessibility.Tests;

public class ReportServiceTests
{
    private static readonly Guid Site = Guid.NewGuid();
    private static readonly KnowledgeBase Kb = new();

    private static RuleResult Violation(string rule, string[] tags, string impact = "serious", params string[] selectors)
        => new(rule, "violation", impact, rule + " help", "https://dequeuniversity.com/rules/axe/4.14/" + rule, tags,
            selectors.Select(s => new NodeResult(s, $"<x class=\"{s}\">")).ToList());

    private static PageResult Page(Guid key, string name, params RuleResult[] rules)
        => new(key, name, "sv", $"https://a.se/{name}", 1280, "ok", null, [], rules, []);

    /// <summary>Runs a complete scan of the given pages and returns the report service over the same store.</summary>
    private static async Task<(ReportService Report, TestStore Store, int ScanId)> Scanned(params PageResult[] pages)
    {
        var store = new TestStore();
        var (scans, _) = TestStore.CreateScanService(store: store);
        var id = await scans.StartAsync(Site, false, pages.Length, "Anna");
        foreach (var p in pages) await scans.AddPageAsync(id, p);
        await scans.FinishAsync(id, complete: true);
        return (new ReportService(store, Kb, TestStore.Settings(store)), store, id);
    }

    private static readonly IssueFilter All = new(null, null, null, null, false, null);

    [Fact]
    public async Task Issues_group_by_rule_with_all_criteria()
    {
        var (report, _, _) = await Scanned(Page(Guid.NewGuid(), "a", Violation("link-name", ["wcag2a", "wcag244", "wcag412"], selectors: "a.x")));

        var group = Assert.Single(await report.GetIssuesAsync(Site, All));
        Assert.Equal("link-name", group.RuleId);
        Assert.Equal(["2.4.4", "4.1.2"], group.Criteria);
        Assert.Equal(Owner.Both, group.Owner); // from the axe-rules override
    }

    [Fact]
    public async Task Groups_count_pages_and_elements_and_sort_by_impact_then_pages()
    {
        var (report, _, _) = await Scanned(
            Page(Guid.NewGuid(), "a", Violation("heading-order", ["wcag2a", "wcag131"], "moderate", "h4"), Violation("image-alt", ["wcag2a", "wcag111"], "critical", "img.1", "img.2")),
            Page(Guid.NewGuid(), "b", Violation("heading-order", ["wcag2a", "wcag131"], "moderate", "h4")));

        var groups = await report.GetIssuesAsync(Site, All);

        Assert.Equal(["image-alt", "heading-order"], groups.Select(g => g.RuleId));
        Assert.Equal((1, 2), (groups[0].PageCount, groups[0].ElementCount));
        Assert.Equal((2, 2), (groups[1].PageCount, groups[1].ElementCount));
    }

    [Fact]
    public async Task False_positive_for_whole_page_closes_all_nodes_of_rule_on_that_page()
    {
        var key = Guid.NewGuid();
        var (report, store, _) = await Scanned(Page(key, "a", Violation("image-alt", ["wcag111"], selectors: ["img.1", "img.2"])));
        store.FalsePositives.Add(new FalsePositiveEntity { Id = 99, SiteKey = Site, RuleId = "image-alt", ContentKey = key, Selector = null, Note = "decorative" });

        Assert.Empty(await report.GetIssuesAsync(Site, All));
        var fp = Assert.Single(await report.GetIssuesAsync(Site, All with { FalsePositives = true }, withNodes: true));
        Assert.All(fp.Pages.Single().Nodes, n => Assert.Equal(99, n.FalsePositiveId));
    }

    [Fact]
    public async Task False_positive_with_selector_closes_only_that_node()
    {
        var key = Guid.NewGuid();
        var (report, store, _) = await Scanned(Page(key, "a", Violation("image-alt", ["wcag111"], selectors: ["img.1", "img.2"])));
        store.FalsePositives.Add(new FalsePositiveEntity { Id = 7, SiteKey = Site, RuleId = "image-alt", ContentKey = key, Selector = "img.1", Note = "x" });

        var group = Assert.Single(await report.GetIssuesAsync(Site, All, withNodes: true));
        Assert.Equal(["img.2"], group.Pages.Single().Nodes.Select(n => n.Selector));
    }

    [Fact]
    public async Task Findings_outside_target_are_ignored()
    {
        // 2.4.13 Focus Appearance is AAA; the default target is 2.2 AA.
        var (report, _, _) = await Scanned(Page(Guid.NewGuid(), "a", Violation("focus-appearance-test", ["wcag22aaa", "wcag2413"], selectors: "a")));

        Assert.Empty(await report.GetIssuesAsync(Site, All));
        var overview = await report.GetOverviewAsync(Site);
        Assert.Equal(0, overview.Score.Fail);
    }

    [Fact]
    public async Task Best_practice_findings_are_listed_but_never_scored()
    {
        var (report, _, _) = await Scanned(Page(Guid.NewGuid(), "a", Violation("region", ["best-practice"], "moderate", "div")));

        var group = Assert.Single(await report.GetIssuesAsync(Site, All));
        Assert.True(group.BestPractice);
        Assert.Equal(0, (await report.GetOverviewAsync(Site)).Score.Fail);
    }

    [Fact]
    public async Task Filter_by_owner_and_principle()
    {
        var (report, _, _) = await Scanned(Page(Guid.NewGuid(), "a",
            Violation("image-alt", ["wcag111"], selectors: "img"),
            Violation("color-contrast", ["wcag2aa", "wcag143"], selectors: "p")));

        Assert.Equal(["image-alt"], (await report.GetIssuesAsync(Site, All with { Owner = Owner.Editor })).Select(g => g.RuleId));
        Assert.Equal(2, (await report.GetIssuesAsync(Site, All with { Principle = 1 })).Count);
        Assert.Empty(await report.GetIssuesAsync(Site, All with { Principle = 2 }));
        Assert.Equal(["color-contrast"], (await report.GetIssuesAsync(Site, All with { Criterion = "1.4.3" })).Select(g => g.RuleId));
    }

    [Fact]
    public async Task Overview_scores_tiles_and_manual_counts()
    {
        var (report, store, _) = await Scanned(Page(Guid.NewGuid(), "a", Violation("image-alt", ["wcag111"], selectors: "img")));
        store.ManualResults.Add(new ManualResultEntity { SiteKey = Site, CriterionId = "2.1.1", Status = "pass", UserName = "Anna", UpdatedUtc = DateTime.UtcNow });

        var overview = await report.GetOverviewAsync(Site);

        Assert.Equal(1, overview.Score.Fail);
        Assert.Equal(1, overview.Score.Pass);
        Assert.Equal(1, overview.Principles.Single(p => p.Principle == 1).Fail);
        Assert.Equal(1, overview.ManualChecked);
        Assert.Equal(54, overview.ManualTotal); // 55 in 2.2 AA minus the fully automatable 3.1.1
        Assert.NotNull(overview.LastScan);
        Assert.Single(overview.History);
    }

    [Fact]
    public async Task Overview_without_any_scan_still_counts_manual_results()
    {
        var store = new TestStore();
        store.ManualResults.Add(new ManualResultEntity { SiteKey = Site, CriterionId = "2.1.1", Status = "pass" });
        var overview = await new ReportService(store, Kb, TestStore.Settings(store)).GetOverviewAsync(Site);

        Assert.Null(overview.LastScan);
        Assert.Equal(1, overview.Score.Pass);
    }

    [Fact]
    public async Task Finish_stores_a_score_snapshot()
    {
        var (_, store, id) = await Scanned(Page(Guid.NewGuid(), "a", Violation("image-alt", ["wcag111"], selectors: "img")));
        Assert.NotNull(store.Scans.Single(s => s.Id == id).Score);
        Assert.Equal(1, store.Scans.Single(s => s.Id == id).FailCount);
    }
}

public class CsvWriterTests
{
    [Fact]
    public void Csv_quotes_commas_quotes_and_newlines()
    {
        var group = new IssueGroup("image-alt", "Images must have alt", ["1.1.1"], WcagLevel.A, Owner.Editor, "critical", "violation", false,
            1, 1, "w", "h", "c", "https://x", [new IssuePage(Guid.NewGuid(), "Om oss, \"vi\"", "sv", "https://a.se/om",
                [new IssueNode(1, "img", "<a title=\"a, b\">\n", null, null)], [1280])]);

        var csv = CsvWriter.Write([group]);
        var lines = csv.TrimStart('﻿').Split("\r\n");

        Assert.Equal("Rule,Criteria,Level,Owner,Impact,Kind,Page,Url,Selector,Html", lines[0]);
        Assert.Equal("image-alt,1.1.1,A,Editor,critical,violation,\"Om oss, \"\"vi\"\"\",https://a.se/om,img,\"<a title=\"\"a, b\"\">\n\"", lines[1]);
    }

    [Fact]
    public void Csv_starts_with_a_bom_for_excel() => Assert.StartsWith("﻿", CsvWriter.Write([]));
}
