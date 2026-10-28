using Esatto.Umbraco.Backoffice.Accessibility.Knowledge;
using Esatto.Umbraco.Backoffice.Accessibility.Persistence;
using Esatto.Umbraco.Backoffice.Accessibility.Reporting;
using Esatto.Umbraco.Backoffice.Accessibility.Scanning;
using Esatto.Umbraco.Backoffice.Accessibility.Settings;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Umbraco.Cms.Core.Services;

namespace Esatto.Umbraco.Backoffice.Accessibility.Tests;

/// <summary>An in-memory <see cref="IAccessibilityStore"/> that behaves like the tables.</summary>
public sealed class TestStore : IAccessibilityStore
{
    private int _nextId = 1;
    private DateTime _clock = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    public string? SettingsJson { get; set; }
    public List<ScanEntity> Scans { get; } = [];
    public List<ScanPageEntity> Pages { get; } = [];
    public List<FindingEntity> Findings { get; } = [];
    public List<FalsePositiveEntity> FalsePositives { get; } = [];
    public List<ManualResultEntity> ManualResults { get; } = [];
    public List<int> Pruned { get; } = [];

    public static (ScanService Service, TestStore Store) CreateScanService(IMediaService? media = null, TestStore? store = null)
    {
        store ??= new TestStore();
        var settings = Settings(store);
        var report = new ReportService(store, new KnowledgeBase(), settings);
        var service = new ScanService(store, settings, new MediaResolver(media ?? Substitute.For<IMediaService>()), report, TimeProvider.System);
        return (service, store);
    }

    public static SettingsService Settings(TestStore store, Dictionary<string, string?>? config = null)
        => new(store, new ConfigurationBuilder().AddInMemoryCollection(config ?? new()).Build(), TimeProvider.System);

    public Task<string?> GetSettingsJsonAsync() => Task.FromResult(SettingsJson);

    public Task SaveSettingsJsonAsync(string json)
    {
        SettingsJson = json;
        return Task.CompletedTask;
    }

    public Task<int> CreateScanAsync(ScanEntity scan)
    {
        scan.Id = _nextId++;
        // Distinct, increasing start times so "newest" is well defined even when tests start scans in a loop.
        _clock = _clock.AddMinutes(1);
        scan.StartedUtc = _clock;
        Scans.Add(scan);
        return Task.FromResult(scan.Id);
    }

    public Task<ScanEntity?> GetScanAsync(int id) => Task.FromResult(Scans.SingleOrDefault(s => s.Id == id));

    public Task<IReadOnlyList<ScanEntity>> GetScansAsync(Guid siteKey, int take)
        => Task.FromResult<IReadOnlyList<ScanEntity>>(Scans.Where(s => s.SiteKey == siteKey).OrderByDescending(s => s.StartedUtc).Take(take).ToList());

    public Task UpdateScanAsync(ScanEntity scan) => Task.CompletedTask;

    public Task SavePageAsync(ScanPageEntity page, IReadOnlyList<FindingEntity> findings)
    {
        var old = Pages.Where(p => p.ScanId == page.ScanId && p.ContentKey == page.ContentKey && p.Culture == page.Culture && p.Viewport == page.Viewport).ToList();
        foreach (var o in old)
        {
            Findings.RemoveAll(f => f.ScanPageId == o.Id);
            Pages.Remove(o);
        }

        page.Id = _nextId++;
        Pages.Add(page);
        foreach (var f in findings)
        {
            f.Id = _nextId++;
            f.ScanPageId = page.Id;
            f.ScanId = page.ScanId;
            Findings.Add(f);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ScanPageEntity>> GetPagesAsync(int scanId)
        => Task.FromResult<IReadOnlyList<ScanPageEntity>>(Pages.Where(p => p.ScanId == scanId).ToList());

    /// <summary>How many times the full findings (with HTML) were read.</summary>
    public int FullFindingReads { get; set; }

    public Task<IReadOnlyList<FindingEntity>> GetFindingsAsync(int scanId)
    {
        FullFindingReads++;
        return Task.FromResult<IReadOnlyList<FindingEntity>>(Findings.Where(f => f.ScanId == scanId).ToList());
    }

    public Task<IReadOnlyList<FindingEntity>> GetFindingSummariesAsync(int scanId)
        => Task.FromResult<IReadOnlyList<FindingEntity>>(Findings.Where(f => f.ScanId == scanId)
            .Select(f => new FindingEntity { Id = f.Id, ScanId = f.ScanId, ScanPageId = f.ScanPageId, RuleId = f.RuleId, Help = f.Help, HelpUrl = f.HelpUrl, Criteria = f.Criteria, Impact = f.Impact, Kind = f.Kind, Selector = f.Selector, MediaKey = f.MediaKey, Html = string.Empty })
            .ToList());

    public Task PruneScansAsync(IEnumerable<int> scanIds)
    {
        foreach (var id in scanIds)
        {
            Pruned.Add(id);
            Findings.RemoveAll(f => f.ScanId == id);
            Pages.RemoveAll(p => p.ScanId == id);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FalsePositiveEntity>> GetFalsePositivesAsync(Guid siteKey)
        => Task.FromResult<IReadOnlyList<FalsePositiveEntity>>(FalsePositives.Where(f => f.SiteKey == siteKey).ToList());

    public Task<int> AddFalsePositiveAsync(FalsePositiveEntity falsePositive)
    {
        falsePositive.Id = _nextId++;
        FalsePositives.Add(falsePositive);
        return Task.FromResult(falsePositive.Id);
    }

    public Task DeleteFalsePositiveAsync(int id)
    {
        FalsePositives.RemoveAll(f => f.Id == id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ManualResultEntity>> GetManualResultsAsync(Guid siteKey)
        => Task.FromResult<IReadOnlyList<ManualResultEntity>>(ManualResults.Where(m => m.SiteKey == siteKey).ToList());

    public Task UpsertManualResultAsync(ManualResultEntity result)
    {
        ManualResults.RemoveAll(m => m.SiteKey == result.SiteKey && m.CriterionId == result.CriterionId);
        result.Id = _nextId++;
        ManualResults.Add(result);
        return Task.CompletedTask;
    }

    public Task DeleteManualResultAsync(Guid siteKey, string criterionId)
    {
        ManualResults.RemoveAll(m => m.SiteKey == siteKey && m.CriterionId == criterionId);
        return Task.CompletedTask;
    }
}
