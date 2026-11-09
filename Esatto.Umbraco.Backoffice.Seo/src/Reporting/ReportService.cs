using Esatto.Umbraco.Backoffice.Seo.Checks;
using Esatto.Umbraco.Backoffice.Seo.Evaluation;
using Esatto.Umbraco.Backoffice.Seo.Persistence;
using Esatto.Umbraco.Backoffice.Seo.Scanning;
using Esatto.Umbraco.Backoffice.Seo.Settings;

namespace Esatto.Umbraco.Backoffice.Seo.Reporting;

/// <summary>
/// Caches the evaluated latest scan per site, so the Overview and Issues tabs don't re-read every page's
/// facts on each request. Anything that changes results (ignore, re-check, new scan) calls <see cref="Invalidate"/>,
/// which drops every entry. Requests for the same key while it's being built share that one build.
/// </summary>
public sealed class ReportCache(TimeProvider? time = null)
{
    /// <summary>Entries kept at most (each is a whole evaluated scan); the least recently used goes first.</summary>
    public const int MaxEntries = 8;

    /// <summary>An entry not asked for this long is built again.</summary>
    public static readonly TimeSpan Expiry = TimeSpan.FromMinutes(5);

    private sealed class Entry(Task<object?> value, long now)
    {
        public Task<object?> Value { get; } = value;
        public long LastUsed { get; set; } = now;
    }

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public int Count
    {
        get { lock (_lock) return _entries.Count; }
    }

    public void Invalidate()
    {
        lock (_lock) _entries.Clear();
    }

    public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> create)
    {
        Entry entry;
        lock (_lock)
        {
            var now = _time.GetTimestamp();
            if (!_entries.TryGetValue(key, out entry!) || _time.GetElapsedTime(entry.LastUsed, now) > Expiry)
            {
                if (_entries.Count >= MaxEntries && !_entries.ContainsKey(key))
                    _entries.Remove(_entries.MinBy(e => e.Value.LastUsed).Key);
                entry = new Entry(Build(create), now);
                _entries[key] = entry;
            }

            entry.LastUsed = now;
        }

        try
        {
            return (T)(await entry.Value)!;
        }
        catch
        {
            lock (_lock)
                if (_entries.TryGetValue(key, out var current) && ReferenceEquals(current, entry)) _entries.Remove(key); // don't keep a failure
            throw;
        }
    }

    private static async Task<object?> Build<T>(Func<Task<T>> create) => await create();
}

/// <summary>Builds the Overview and Issues from the latest complete scan, evaluated with the current ignores.</summary>
public sealed class ReportService(ISeoStore store, ScanCoordinator coordinator, SeoEnvironment environment, ReportCache cache)
{
    public const int TopCount = 5;
    public const int TrendLength = 10;

    private sealed record Loaded(ScanEntity Scan, ScanSnapshot Snapshot, Evaluation.Evaluation Evaluation, IReadOnlyList<IgnoreEntity> Ignores, IReadOnlyList<PageEntity> Skipped);

    public async Task<Overview> GetOverviewAsync(Guid siteKey)
    {
        var scans = await store.GetScansAsync(siteKey, 30);
        var running = coordinator.GetProgress(siteKey);
        var latestAttempt = scans.FirstOrDefault(s => s.Status != ScanStatus.Running);
        var loaded = await LoadAsync(siteKey, scans);
        if (loaded is null)
        {
            return new Overview(null, latestAttempt is null ? null : ScanInfo.From(latestAttempt), running, null, [], [],
                new SeverityCounts(0, 0, 0), [], [], [], false, environment.IntentionallyHidden);
        }

        var (scan, snapshot, evaluation, _, skipped) = loaded;
        var failures = evaluation.Failures.Select(f => CheckCatalog.Info[f.CheckId].Severity).ToList();
        var siteOutcomes = evaluation.Outcomes.Where(o => CheckCatalog.Info[o.CheckId].Scope == CheckScope.Site).ToDictionary(o => o.CheckId);

        return new Overview(
            ScanInfo.From(scan),
            latestAttempt is null || latestAttempt.Id == scan.Id ? null : ScanInfo.From(latestAttempt),
            running,
            ScoreCalculator.Calculate(snapshot, evaluation, CheckCatalog.Info),
            scans.Where(s => s is { Status: ScanStatus.Complete, Score: not null })
                .Take(TrendLength).Reverse()
                .Select(s => new TrendPoint(s.Id, s.StartedUtc, s.Score!.Value, s.PagesDone)).ToList(),
            Priority.Rank(evaluation, CheckCatalog.Info).Take(TopCount).ToList(),
            new SeverityCounts(failures.Count(s => s == Severity.Critical), failures.Count(s => s == Severity.Warning), failures.Count(s => s == Severity.Notice)),
            CheckCatalog.All.Where(c => c.Info.Scope == CheckScope.Site)
                .Select(c => siteOutcomes.TryGetValue(c.Info.Id, out var o)
                    ? new SiteCheckRow(c.Info.Id, c.Info.Severity, o.Passed, o.Value)
                    : new SiteCheckRow(c.Info.Id, c.Info.Severity, null, null))
                .ToList(),
            snapshot.Pages.Where(p => p.Fetched && p.Hidden)
                .Select(p => new PageRow(p.Page.ContentKey, p.Page.Name, p.Page.Culture, p.Page.Url, HomeHidden.Reason(p))).ToList(),
            skipped.Select(p => new PageRow(p.ContentKey, p.Name, p.Culture, p.Url, p.Reason ?? string.Empty)).ToList(),
            snapshot.SiteHidden,
            environment.IntentionallyHidden);
    }

    public async Task<IReadOnlyList<IssueGroup>> GetIssuesAsync(Guid siteKey, IssueFilter filter)
    {
        var loaded = await LoadAsync(siteKey, await store.GetScansAsync(siteKey, 30));
        if (loaded is null) return [];

        var rules = ScanResults.ToRules(loaded.Ignores);
        var source = filter.Ignored ? loaded.Evaluation.Ignored.Where(o => !o.Passed) : loaded.Evaluation.Failures;
        var names = loaded.Snapshot.Pages.ToDictionary(p => (p.Page.ContentKey, p.Page.Culture), p => p.Page.Name);

        var selected = source.Where(o =>
        {
            var info = CheckCatalog.Info[o.CheckId];
            return (filter.Severity is null || info.Severity == filter.Severity)
                && (filter.Owner is null || info.Owner == filter.Owner)
                && (filter.CheckId is null || o.CheckId == filter.CheckId)
                && (string.IsNullOrEmpty(filter.Culture) || string.Equals(o.Culture, filter.Culture, StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrWhiteSpace(filter.Search) || Matches(o, Name(o), filter.Search.Trim()));
        }).ToList();

        string Name(Outcome o) => o.ContentKey is { } key && names.TryGetValue((key, o.Culture), out var n) ? n : o.Url ?? "Whole site";

        return Priority.Rank(selected, CheckCatalog.Info)
            .Select(summary => new IssueGroup(summary, selected
                .Where(o => o.CheckId == summary.CheckId)
                .OrderBy(o => o.Url, StringComparer.Ordinal)
                .Select(o => new IssueOccurrence(o.ContentKey, Name(o), o.Culture, o.Url, o.Value, filter.Ignored, rules.FirstOrDefault(r => r.Matches(o))?.Id))
                .ToList()))
            .ToList();
    }

    private static bool Matches(Outcome o, string name, string search)
        => (o.Url?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) || name.Contains(search, StringComparison.OrdinalIgnoreCase);

    private async Task<Loaded?> LoadAsync(Guid siteKey, IReadOnlyList<ScanEntity> scans)
    {
        var summary = scans.FirstOrDefault(s => s.Status == ScanStatus.Complete);
        if (summary is null) return null;

        var ignores = await store.GetIgnoresAsync(siteKey);
        var key = $"{summary.Id}:{string.Join(',', ignores.Select(i => i.Id))}";
        return await cache.GetOrAddAsync(key, async () =>
        {
            var scan = await store.GetScanAsync(summary.Id) ?? summary; // the full row: probe and links
            var pages = await store.GetPagesAsync(scan.Id);
            var snapshot = SnapshotLoader.Build(scan, pages, environment.IntentionallyHidden);
            var evaluation = Evaluator.Evaluate(snapshot, CheckCatalog.All, ScanResults.ToRules(ignores));
            return new Loaded(scan, snapshot, evaluation, ignores, pages.Where(p => p.Status == PageStatus.Skipped).ToList());
        });
    }
}
