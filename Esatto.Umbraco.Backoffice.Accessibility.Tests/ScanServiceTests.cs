using Esatto.Umbraco.Backoffice.Accessibility.Persistence;
using Esatto.Umbraco.Backoffice.Accessibility.Scanning;
using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Accessibility.Tests;

public class ScanServiceTests
{
    private static readonly Guid Site = Guid.NewGuid();

    private static PageResult Result(params RuleResult[] rules) => new(
        Guid.NewGuid(), "Page", "sv", "https://a.se/x", 1280, "ok", null, [new PassedRule("document-title", ["wcag2a", "wcag242"])], rules, []);

    private static RuleResult Rule(string id, string[] tags, params NodeResult[] nodes)
        => new(id, "violation", "serious", "Help", "https://dequeuniversity.com/rules/axe/4.14/" + id, tags, nodes);

    [Fact]
    public async Task Finding_html_is_truncated_to_1024()
    {
        var (service, store) = TestStore.CreateScanService();
        await service.AddPageAsync(1, Result(Rule("label", ["wcag2a", "wcag412"], new NodeResult("#x", new string('a', 5000)))));

        var finding = store.Findings.Single();
        Assert.Equal(1024, finding.Html.Length);
    }

    [Fact]
    public async Task Finding_criteria_come_from_wcag_tags()
    {
        var (service, store) = TestStore.CreateScanService();
        await service.AddPageAsync(1, Result(Rule("link-name", ["wcag2a", "wcag244", "wcag412"], new NodeResult("a", "<a></a>"))));

        Assert.Equal("2.4.4,4.1.2", store.Findings.Single().Criteria);
    }

    [Fact]
    public async Task One_finding_per_node()
    {
        var (service, store) = TestStore.CreateScanService();
        await service.AddPageAsync(1, Result(Rule("image-alt", ["wcag111"], new NodeResult("img.a", "<img>"), new NodeResult("img.b", "<img>"))));

        Assert.Equal(["img.a", "img.b"], store.Findings.Select(f => f.Selector));
    }

    [Fact]
    public async Task Media_key_is_resolved_for_image_rules_only()
    {
        var key = Guid.NewGuid();
        var media = Substitute.For<IMedia>();
        media.Key.Returns(key);
        var mediaService = Substitute.For<IMediaService>();
        mediaService.GetMediaByPath("/media/a/x.jpg").Returns(media);
        var (service, store) = TestStore.CreateScanService(mediaService);

        await service.AddPageAsync(1, Result(
            Rule("image-alt", ["wcag111"], new NodeResult("img", """<img src="/media/a/x.jpg">""")),
            Rule("color-contrast", ["wcag143"], new NodeResult("p", """<p><img src="/media/a/x.jpg"></p>"""))));

        Assert.Equal(key, store.Findings.Single(f => f.RuleId == "image-alt").MediaKey);
        Assert.Null(store.Findings.Single(f => f.RuleId == "color-contrast").MediaKey);
    }

    [Fact]
    public async Task Finish_counts_pages_and_merges_document_links()
    {
        var (service, store) = TestStore.CreateScanService();
        var id = await service.StartAsync(Site, mobile: false, pagesTotal: 3, userName: "Anna");
        await service.AddPageAsync(id, Result() with { DocumentLinks = ["https://a.se/media/1/r.pdf"] });
        await service.AddPageAsync(id, Result() with { Status = "skipped", Reason = "protected", DocumentLinks = ["https://a.se/media/1/r.pdf", "https://a.se/media/2/s.docx"] });
        await service.AddPageAsync(id, Result() with { Status = "failed", Reason = "timeout" });

        var summary = await service.FinishAsync(id, complete: true);

        Assert.Equal((1, 1, 1), (summary.PagesDone, summary.PagesSkipped, summary.PagesFailed));
        Assert.Equal(ScanStatus.Complete, summary.Status);
        Assert.Equal("https://a.se/media/1/r.pdf\nhttps://a.se/media/2/s.docx", store.Scans.Single().DocumentLinks);
    }

    [Fact]
    public async Task Finish_prunes_findings_of_scans_beyond_ten()
    {
        var (service, store) = TestStore.CreateScanService();
        for (var i = 0; i < 11; i++) await service.StartAsync(Site, false, 1, "Anna");
        var newest = await service.StartAsync(Site, false, 1, "Anna");

        await service.FinishAsync(newest, complete: true);

        var oldestTwo = store.Scans.OrderBy(s => s.StartedUtc).Take(2).Select(s => s.Id);
        Assert.Equal(oldestTwo, store.Pruned.Distinct().Order()); // closing an abandoned scan prunes too; pruning is idempotent
    }

    [Fact]
    public async Task Done_lists_recorded_pages_for_resume()
    {
        var (service, _) = TestStore.CreateScanService();
        var id = await service.StartAsync(Site, false, 2, "Anna");
        var page = Result();
        await service.AddPageAsync(id, page);

        var done = await service.DoneAsync(id);

        Assert.Equal([new DonePage(page.ContentKey, "sv", 1280)], done);
    }
}

public class RetentionTests
{
    [Fact]
    public void Keeps_newest_ten()
    {
        var start = new DateTime(2026, 1, 1);
        var scans = Enumerable.Range(1, 12).Select(i => (Id: i, Started: start.AddDays(i)));
        Assert.Equal([1, 2], Retention.ScansToPrune(scans).Order());
    }

    [Fact]
    public void Nothing_to_prune_with_ten_or_fewer() => Assert.Empty(Retention.ScansToPrune([(1, DateTime.UtcNow)]));
}

public class PassedCriteriaTests
{
    [Fact]
    public async Task Passed_rules_are_stored_as_criteria()
    {
        var (service, store) = TestStore.CreateScanService();
        await service.AddPageAsync(1, new PageResult(Guid.NewGuid(), "P", "sv", "https://a.se/", 1280, "ok", null,
            [new PassedRule("html-has-lang", ["wcag2a", "wcag311"]), new PassedRule("region", ["best-practice"])], [], []));

        Assert.Equal("3.1.1", store.Pages.Single().PassedCriteria);
    }
}
