using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>
/// An <see cref="ISeoReportStore"/> that forwards to another one until a test says that one operation fails: the way to prove that a
/// background run still ends cleanly when the database does not answer. Each failure is an exception the test chose, returned as a
/// faulted task (not thrown at the call), which is how an async database call fails.
/// </summary>
internal sealed class FlakyStore(ISeoReportStore inner) : ISeoReportStore
{
    public Exception? GetDocumentFails { get; set; }

    public Exception? SaveDocumentFails { get; set; }

    public Exception? AcquireLockFails { get; set; }

    public Exception? ReleaseLockFails { get; set; }

    public Task<ReportEntity?> GetReportAsync(Period period) => inner.GetReportAsync(period);

    public Task SaveReportAsync(ReportEntity report) => inner.SaveReportAsync(report);

    public Task<IReadOnlyList<Period>> ListPeriodsAsync() => inner.ListPeriodsAsync();

    public Task<string?> GetNoteAsync(Period period) => inner.GetNoteAsync(period);

    public Task SaveNoteAsync(Period period, string? text) => inner.SaveNoteAsync(period, text);

    public Task<string?> GetDocumentAsync(string key)
        => GetDocumentFails is { } failure ? Task.FromException<string?>(failure) : inner.GetDocumentAsync(key);

    public Task SaveDocumentAsync(string key, string json)
        => SaveDocumentFails is { } failure ? Task.FromException(failure) : inner.SaveDocumentAsync(key, json);

    public Task DeleteDocumentAsync(string key) => inner.DeleteDocumentAsync(key);

    public Task<bool> TryAcquireLockAsync(string name, string owner, TimeSpan ttl)
        => AcquireLockFails is { } failure ? Task.FromException<bool>(failure) : inner.TryAcquireLockAsync(name, owner, ttl);

    public Task ReleaseLockAsync(string name, string owner)
        => ReleaseLockFails is { } failure ? Task.FromException(failure) : inner.ReleaseLockAsync(name, owner);
}
