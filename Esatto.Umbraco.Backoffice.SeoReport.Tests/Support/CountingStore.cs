using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>
/// An <see cref="ISeoReportStore"/> that forwards to another one and counts the reads and writes of reports, so a test can prove
/// that a cache answered (no read) or that nothing reached the store (no read and no write).
/// </summary>
internal sealed class CountingStore(ISeoReportStore inner) : ISeoReportStore
{
    private int _reportReads;
    private int _reportWrites;
    private int _periodListReads;

    public int ReportReads => Volatile.Read(ref _reportReads);

    public int ReportWrites => Volatile.Read(ref _reportWrites);

    public int PeriodListReads => Volatile.Read(ref _periodListReads);

    public Task<ReportEntity?> GetReportAsync(Period period)
    {
        Interlocked.Increment(ref _reportReads);
        return inner.GetReportAsync(period);
    }

    public Task SaveReportAsync(ReportEntity report)
    {
        Interlocked.Increment(ref _reportWrites);
        return inner.SaveReportAsync(report);
    }

    public Task<IReadOnlyList<Period>> ListPeriodsAsync()
    {
        Interlocked.Increment(ref _periodListReads);
        return inner.ListPeriodsAsync();
    }

    public Task<string?> GetNoteAsync(Period period) => inner.GetNoteAsync(period);

    public Task SaveNoteAsync(Period period, string? text) => inner.SaveNoteAsync(period, text);

    public Task<string?> GetDocumentAsync(string key) => inner.GetDocumentAsync(key);

    public Task SaveDocumentAsync(string key, string json) => inner.SaveDocumentAsync(key, json);

    public Task DeleteDocumentAsync(string key) => inner.DeleteDocumentAsync(key);

    public Task<bool> TryAcquireLockAsync(string name, string owner, TimeSpan ttl) => inner.TryAcquireLockAsync(name, owner, ttl);

    public Task ReleaseLockAsync(string name, string owner) => inner.ReleaseLockAsync(name, owner);
}
