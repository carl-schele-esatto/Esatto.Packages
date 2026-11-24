using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>
/// In-memory <see cref="ISeoReportStore"/> behaving like the tables: one row per primary key, replaced on save, the same
/// argument limits, and the same lock rules (free, expired or own; renewing extends; only the owner releases). Time comes
/// from the injected <see cref="TimeProvider"/>, so a test moves it with a <c>FakeTimeProvider</c>. Rows are copied in and
/// out, so changing an entity after saving or reading it never changes what is stored.
/// </summary>
internal sealed class InMemoryStore(TimeProvider? timeProvider = null) : ISeoReportStore
{
    private readonly object _gate = new();
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly Dictionary<Period, ReportEntity> _reports = [];
    private readonly Dictionary<Period, string> _notes = [];
    private readonly Dictionary<string, string> _documents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Owner, DateTimeOffset Expires)> _locks = new(StringComparer.Ordinal);

    /// <summary>A snapshot of the documents by key, for asserting what was (or was never) written.</summary>
    public IReadOnlyDictionary<string, string> Documents
    {
        get { lock (_gate) return new Dictionary<string, string>(_documents); }
    }

    public Task<ReportEntity?> GetReportAsync(Period period)
    {
        lock (_gate) return Task.FromResult(_reports.TryGetValue(period, out var row) ? Copy(row) : null);
    }

    public Task SaveReportAsync(ReportEntity report)
    {
        var period = report.ToPeriod();
        lock (_gate) _reports[period] = Copy(report);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Period>> ListPeriodsAsync()
    {
        lock (_gate) return Task.FromResult<IReadOnlyList<Period>>(_reports.Keys.Order().ToList());
    }

    public Task<string?> GetNoteAsync(Period period)
    {
        lock (_gate) return Task.FromResult(_notes.GetValueOrDefault(period));
    }

    public Task SaveNoteAsync(Period period, string? text)
    {
        if (!string.IsNullOrEmpty(text)) StoreGuards.Note(text);

        lock (_gate)
        {
            if (string.IsNullOrEmpty(text)) _notes.Remove(period);
            else _notes[period] = text;
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetDocumentAsync(string key)
    {
        StoreGuards.DocumentKey(key);
        lock (_gate) return Task.FromResult(_documents.GetValueOrDefault(key));
    }

    public Task SaveDocumentAsync(string key, string json)
    {
        StoreGuards.DocumentKey(key);
        lock (_gate) _documents[key] = json;
        return Task.CompletedTask;
    }

    public Task DeleteDocumentAsync(string key)
    {
        StoreGuards.DocumentKey(key);
        lock (_gate) _documents.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> TryAcquireLockAsync(string name, string owner, TimeSpan ttl)
    {
        StoreGuards.Lock(name, owner);
        StoreGuards.Lifetime(ttl);

        lock (_gate)
        {
            var now = _time.GetUtcNow();
            // The row is taken when there is none, when it lapsed (an expiry of exactly now counts), or when it is ours.
            if (_locks.TryGetValue(name, out var held) && held.Owner != owner && held.Expires > now)
                return Task.FromResult(false);

            _locks[name] = (owner, now + ttl);
            return Task.FromResult(true);
        }
    }

    public Task ReleaseLockAsync(string name, string owner)
    {
        StoreGuards.Lock(name, owner);

        lock (_gate)
        {
            if (_locks.TryGetValue(name, out var held) && held.Owner == owner) _locks.Remove(name);
        }

        return Task.CompletedTask;
    }

    private static ReportEntity Copy(ReportEntity row)
        => new() { Period = row.Period, Json = row.Json, FetchedUtc = row.FetchedUtc, ContractVersion = row.ContractVersion };
}
