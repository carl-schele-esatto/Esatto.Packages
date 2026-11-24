using Esatto.Umbraco.Backoffice.SeoReport.Reporting;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>
/// An <see cref="ISeoReportService"/> for the backfill tests: it knows which months are archived, remembers every refresh it was
/// asked for (in order), and lets a test decide what a refresh does (wait on a gate, throw, change the settings, move the clock)
/// through <see cref="OnRefresh"/>. A refresh that returns archives its month, as the real one does.
/// </summary>
internal sealed class FakeReportService : ISeoReportService
{
    private readonly object _gate = new();
    private readonly HashSet<Period> _archived = [];
    private readonly List<Period> _refreshed = [];
    private readonly Dictionary<Period, (Report Report, DateTimeOffset Fetched)> _reports = [];
    private int _reportReads;

    public FakeReportService(string expectedLatest = "2026-07") => ExpectedLatestPeriod = Period.Parse(expectedLatest);

    public Period ExpectedLatestPeriod { get; set; }

    /// <summary>What a refresh does before it archives its month; the default does nothing.</summary>
    public Func<Period, CancellationToken, Task> OnRefresh { get; set; } = (_, _) => Task.CompletedTask;

    /// <summary>When set, <see cref="NeedsFetchAsync"/> throws it (a store that cannot be read).</summary>
    public Exception? NeedsFetchFails { get; set; }

    /// <summary>The months a refresh was asked for, in the order they were asked, whether or not they succeeded.</summary>
    public IReadOnlyList<Period> Refreshed
    {
        get { lock (_gate) return _refreshed.ToList(); }
    }

    public void Archive(params string[] periods)
    {
        lock (_gate)
        {
            foreach (var period in periods) _archived.Add(Period.Parse(period));
        }
    }

    public Task<bool> NeedsFetchAsync(Period period, CancellationToken ct = default)
    {
        if (NeedsFetchFails is { } failure) return Task.FromException<bool>(failure);

        lock (_gate) return Task.FromResult(!_archived.Contains(period));
    }

    public async Task<Report> RefreshAsync(Period period, CancellationToken ct = default)
    {
        lock (_gate) _refreshed.Add(period);

        await OnRefresh(period, ct);

        lock (_gate)
        {
            _archived.Add(period);
            return _reports.TryGetValue(period, out var kept) ? kept.Report : TestReports.With(period.ToString());
        }
    }

    /// <summary>
    /// Archives <paramref name="report"/> under its own month, so a read returns it (by that month, or as the newest) and a refresh of
    /// the month returns it. The very instance is handed out, as the real service's cache does, so a test can prove that a reader
    /// never changed it.
    /// </summary>
    public void ArchiveReport(Report report, DateTimeOffset? fetchedUtc = null)
    {
        var period = Period.Parse(report.ReportPeriod);
        lock (_gate)
        {
            _archived.Add(period);
            _reports[period] = (report, fetchedUtc ?? new DateTimeOffset(2026, 8, 1, 6, 0, 0, TimeSpan.Zero));
        }
    }

    /// <summary>How many times a report or its fetch time was read: nothing is read for a period that is refused first.</summary>
    public int ReportReads => Volatile.Read(ref _reportReads);

    public Task<Report?> GetReportAsync(string? period = null, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _reportReads);
        return Task.FromResult(Find(period)?.Report);
    }

    public Task<DateTimeOffset?> GetFetchedUtcAsync(string? period = null, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _reportReads);
        return Task.FromResult(Find(period)?.Fetched);
    }

    // As the real service: blank is the newest, anything that is not exactly YYYY-MM is none.
    private (Report Report, DateTimeOffset Fetched)? Find(string? text)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(text)) return _reports.Count == 0 ? null : _reports.OrderBy(r => r.Key).Last().Value;

            return Period.TryParse(text, out var period) && _reports.TryGetValue(period, out var found) ? found : null;
        }
    }

    public async Task<bool> IsUpToDateAsync(CancellationToken ct = default) => !await NeedsFetchAsync(ExpectedLatestPeriod, ct);

    public Task<IReadOnlyList<string>> GetAvailablePeriodsAsync(CancellationToken ct = default)
    {
        lock (_gate) return Task.FromResult<IReadOnlyList<string>>(_archived.Order().Select(p => p.ToString()).ToList());
    }
}
