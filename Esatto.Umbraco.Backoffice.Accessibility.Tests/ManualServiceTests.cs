using Esatto.Umbraco.Backoffice.Accessibility.Knowledge;
using Esatto.Umbraco.Backoffice.Accessibility.Manual;
using Esatto.Umbraco.Backoffice.Accessibility.Persistence;
using Esatto.Umbraco.Backoffice.Accessibility.Scanning;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Accessibility.Tests;

public class ManualServiceTests
{
    private static readonly Guid Site = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private static (ManualService Service, TestStore Store) Create(TestStore? store = null, Dictionary<string, string?>? config = null)
    {
        store ??= new TestStore();
        return (new ManualService(store, new KnowledgeBase(), TestStore.Settings(store, config), new FakeTimeProvider(Now)), store);
    }

    [Fact]
    public async Task Checklist_excludes_fully_automatable_criteria()
    {
        var items = await Create().Service.GetChecklistAsync(Site);
        Assert.DoesNotContain(items, i => i.Criterion.Id == "3.1.1");
        Assert.Equal(54, items.Count); // 2.2 AA = 55, minus 3.1.1
    }

    [Fact]
    public async Task Result_older_than_recheck_interval_needs_recheck()
    {
        var (service, store) = Create();
        store.ManualResults.Add(new ManualResultEntity { SiteKey = Site, CriterionId = "2.1.1", Status = "pass", UpdatedUtc = Now.AddMonths(-7).UtcDateTime });
        store.ManualResults.Add(new ManualResultEntity { SiteKey = Site, CriterionId = "2.4.3", Status = "pass", UpdatedUtc = Now.AddMonths(-5).UtcDateTime });

        var items = await service.GetChecklistAsync(Site);

        Assert.True(items.Single(i => i.Criterion.Id == "2.1.1").NeedsRecheck);
        Assert.False(items.Single(i => i.Criterion.Id == "2.4.3").NeedsRecheck);
    }

    [Fact]
    public async Task Changing_target_keeps_results_and_new_criteria_start_unchecked()
    {
        var store = new TestStore();
        store.ManualResults.Add(new ManualResultEntity { SiteKey = Site, CriterionId = "1.1.1", Status = "pass", UpdatedUtc = Now.UtcDateTime });
        var (service, _) = Create(store, new() { ["Esatto:Accessibility:Version"] = "V22", ["Esatto:Accessibility:Level"] = "AA" });

        var items = await service.GetChecklistAsync(Site);

        Assert.Equal("pass", items.Single(i => i.Criterion.Id == "1.1.1").Status);
        Assert.Null(items.Single(i => i.Criterion.Id == "2.5.8").Status);
    }

    [Fact]
    public async Task Set_saves_status_note_user_and_date()
    {
        var (service, store) = Create();
        await service.SetAsync(Site, "2.1.1", "pass", "Tabbed through menu", "Anna");

        var saved = store.ManualResults.Single();
        Assert.Equal(("2.1.1", "pass", "Tabbed through menu", "Anna", Now.UtcDateTime), (saved.CriterionId, saved.Status, saved.Note, saved.UserName, saved.UpdatedUtc));
    }

    [Theory]
    [InlineData("2.1.1", "maybe")]
    [InlineData("9.9.9", "pass")]
    public async Task Invalid_status_or_criterion_is_rejected(string criterion, string status)
        => await Assert.ThrowsAsync<ArgumentException>(() => Create().Service.SetAsync(Site, criterion, status, null, "Anna"));

    [Fact]
    public async Task Automated_findings_and_document_links_come_from_the_latest_scan()
    {
        var store = new TestStore();
        var (scans, _) = TestStore.CreateScanService(store: store);
        var id = await scans.StartAsync(Site, false, 1, "Anna");
        await scans.AddPageAsync(id, new PageResult(Guid.NewGuid(), "a", "sv", "https://a.se/a", 1280, "ok", null, [],
            [new RuleResult("image-alt", "incomplete", "serious", "h", "u", ["wcag111"], [new NodeResult("img", "<img>")])],
            ["https://a.se/media/1/r.pdf", "https://a.se/media/2/s.pdf"]));
        await scans.FinishAsync(id, complete: true);

        var items = await Create(store).Service.GetChecklistAsync(Site);

        var nonText = items.Single(i => i.Criterion.Id == "1.1.1");
        Assert.Equal((1, 2), (nonText.AutomatedFindings, nonText.DocumentLinks));
        Assert.Equal(0, items.Single(i => i.Criterion.Id == "2.1.1").DocumentLinks);
    }
}

public class ScannerCheckedTests
{
    private static readonly Guid Site = Guid.NewGuid();

    [Fact]
    public async Task Criterion_the_scanner_passed_without_violations_is_scanner_checked()
    {
        var store = new TestStore();
        var (scans, _) = TestStore.CreateScanService(store: store);
        var id = await scans.StartAsync(Site, false, 1, "Anna");
        await scans.AddPageAsync(id, new PageResult(Guid.NewGuid(), "a", "sv", "https://a.se/a", 1280, "ok", null,
            [new PassedRule("image-alt", ["wcag2a", "wcag111"]), new PassedRule("color-contrast", ["wcag2aa", "wcag143"])],
            [new RuleResult("color-contrast", "violation", "serious", "h", "u", ["wcag2aa", "wcag143"], [new NodeResult("p", "<p>")])],
            []));
        await scans.FinishAsync(id, complete: true);

        var items = await new ManualService(store, new KnowledgeBase(), TestStore.Settings(store), TimeProvider.System).GetChecklistAsync(Site);

        Assert.True(items.Single(i => i.Criterion.Id == "1.1.1").ScannerChecked);
        Assert.False(items.Single(i => i.Criterion.Id == "1.4.3").ScannerChecked); // passed somewhere, but has a violation
        Assert.False(items.Single(i => i.Criterion.Id == "2.1.1").ScannerChecked); // the scanner has no rule for it
    }
}

public class FalsePositiveServiceTests
{
    [Fact]
    public async Task False_positive_requires_a_note()
    {
        var service = new FalsePositiveService(new TestStore(), TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(() => service.AddAsync(Guid.NewGuid(), "image-alt", Guid.NewGuid(), null, " ", "Anna"));
    }

    [Fact]
    public async Task Add_and_remove()
    {
        var store = new TestStore();
        var service = new FalsePositiveService(store, TimeProvider.System);

        var id = await service.AddAsync(Guid.NewGuid(), "image-alt", Guid.NewGuid(), "img.logo", "Decorative", "Anna");
        Assert.Equal("img.logo", store.FalsePositives.Single().Selector);

        await service.RemoveAsync(id);
        Assert.Empty(store.FalsePositives);
    }
}
