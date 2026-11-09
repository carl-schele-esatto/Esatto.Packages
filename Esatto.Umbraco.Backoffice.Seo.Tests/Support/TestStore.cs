using Esatto.Umbraco.Backoffice.Seo.Persistence;

namespace Esatto.Umbraco.Backoffice.Seo.Tests.Support;

/// <summary>In-memory <see cref="ISeoStore"/>, behaving like the tables (ids assigned on insert).</summary>
internal sealed class TestStore : ISeoStore
{
    private readonly object _lock = new();
    private string? _settings;
    private int _nextId = 1;

    public List<ScanEntity> Scans { get; } = [];
    public List<PageEntity> Pages { get; } = [];
    public List<IgnoreEntity> Ignores { get; } = [];

    public Task<string?> GetSettingsJsonAsync() => Task.FromResult(_settings);

    public Task SaveSettingsJsonAsync(string json) { _settings = json; return Task.CompletedTask; }

    public Task<int> CreateScanAsync(ScanEntity scan) { lock (_lock) { scan.Id = _nextId++; Scans.Add(scan); } return Task.FromResult(scan.Id); }

    public Task UpdateScanAsync(ScanEntity scan)
    {
        lock (_lock) { Scans.RemoveAll(s => s.Id == scan.Id); Scans.Add(scan); }
        return Task.CompletedTask;
    }

    public Task<ScanEntity?> GetScanAsync(int id) { lock (_lock) return Task.FromResult(Scans.FirstOrDefault(s => s.Id == id)); }

    public Task<IReadOnlyList<ScanEntity>> GetScansAsync(Guid siteKey, int take)
    {
        // Like the real store: summaries only (no JSON columns), as copies.
        lock (_lock)
            return Task.FromResult<IReadOnlyList<ScanEntity>>(Scans.Where(s => s.SiteKey == siteKey)
                .OrderByDescending(s => s.StartedUtc).ThenByDescending(s => s.Id).Take(take)
                .Select(Summary).ToList());
    }

    public Task<int> AddPageAsync(PageEntity page) { lock (_lock) { page.Id = _nextId++; Pages.Add(page); } return Task.FromResult(page.Id); }

    /// <summary>Runs before a page row is updated, so a test can hold a writer there.</summary>
    public Func<Task>? BeforeUpdatePage { get; set; }

    public async Task UpdatePageAsync(PageEntity page)
    {
        if (BeforeUpdatePage is { } before) await before();
        lock (_lock) { Pages.RemoveAll(p => p.Id == page.Id); Pages.Add(page); }
    }

    public Task<IReadOnlyList<PageEntity>> GetPagesAsync(int scanId)
    {
        lock (_lock) return Task.FromResult<IReadOnlyList<PageEntity>>(Pages.Where(p => p.ScanId == scanId).OrderBy(p => p.Id).ToList());
    }

    public Task PruneScansAsync(IEnumerable<int> scanIds)
    {
        var ids = scanIds.ToHashSet();
        lock (_lock)
        {
            Pages.RemoveAll(p => ids.Contains(p.ScanId));
            foreach (var s in Scans.Where(s => ids.Contains(s.Id)))
            {
                s.ProbeJson = null;
                s.LinksJson = null;
            }
        }

        return Task.CompletedTask;
    }

    private static ScanEntity Summary(ScanEntity s) => new()
    {
        Id = s.Id, SiteKey = s.SiteKey, StartedUtc = s.StartedUtc, FinishedUtc = s.FinishedUtc, Trigger = s.Trigger, UserName = s.UserName,
        Status = s.Status, PagesTotal = s.PagesTotal, PagesDone = s.PagesDone, PagesFailed = s.PagesFailed, PagesSkipped = s.PagesSkipped,
        Score = s.Score, ScoreVersion = s.ScoreVersion, Critical = s.Critical, Warning = s.Warning, Notice = s.Notice,
        Cultures = s.Cultures, TrustedHosts = s.TrustedHosts, Message = s.Message,
    };

    public Task<IReadOnlyList<IgnoreEntity>> GetIgnoresAsync(Guid siteKey)
    {
        lock (_lock) return Task.FromResult<IReadOnlyList<IgnoreEntity>>(Ignores.Where(i => i.SiteKey == siteKey).ToList());
    }

    public Task<int> AddIgnoreAsync(IgnoreEntity ignore) { lock (_lock) { ignore.Id = _nextId++; Ignores.Add(ignore); } return Task.FromResult(ignore.Id); }

    public Task<IgnoreEntity?> GetIgnoreAsync(int id) { lock (_lock) return Task.FromResult(Ignores.FirstOrDefault(i => i.Id == id)); }

    public Task RemoveIgnoreAsync(int id) { lock (_lock) Ignores.RemoveAll(i => i.Id == id); return Task.CompletedTask; }

    public Task MarkRunningAsIncompleteAsync()
    {
        lock (_lock) foreach (var s in Scans.Where(s => s.Status == ScanStatus.Running)) s.Status = ScanStatus.Incomplete;
        return Task.CompletedTask;
    }
}
