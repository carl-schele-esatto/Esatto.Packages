using Esatto.Umbraco.Backoffice.SeoReport.Reporting;

namespace Esatto.Umbraco.Backoffice.SeoReport.Persistence;

/// <summary>
/// The package's database access. One implementation over Umbraco's database (SQL Server and SQLite); an in-memory one in
/// tests. Periods arrive as <see cref="Period"/>, never as text, so nothing unvalidated reaches a key or a query.
/// </summary>
public interface ISeoReportStore
{
    /// <summary>The archived report for the month, or null when there is none.</summary>
    Task<ReportEntity?> GetReportAsync(Period period);

    /// <summary>Inserts the month's report or replaces the one already stored. Throws <see cref="ArgumentException"/> when <see cref="ReportEntity.Period"/> is not <c>YYYY-MM</c>.</summary>
    Task SaveReportAsync(ReportEntity report);

    /// <summary>Every archived month, oldest first.</summary>
    Task<IReadOnlyList<Period>> ListPeriodsAsync();

    Task<string?> GetNoteAsync(Period period);

    /// <summary>Inserts or replaces the month's note; null or empty deletes it. Longer than <see cref="NoteEntity.MaxLength"/> is refused.</summary>
    Task SaveNoteAsync(Period period, string? text);

    /// <summary>The JSON stored under the key, or null.</summary>
    Task<string?> GetDocumentAsync(string key);

    /// <summary>Inserts or replaces the document under the key.</summary>
    Task SaveDocumentAsync(string key, string json);

    /// <summary>Removes the document; a missing one is not an error.</summary>
    Task DeleteDocumentAsync(string key);

    /// <summary>
    /// Takes the named lock for <paramref name="ttl"/>. True when it was free, had expired (an expiry of exactly now counts),
    /// or is already held by <paramref name="owner"/>, whose expiry then moves to now plus <paramref name="ttl"/>; false when
    /// someone else holds it and it has not expired. The database decides when two instances ask at once.
    /// </summary>
    Task<bool> TryAcquireLockAsync(string name, string owner, TimeSpan ttl);

    /// <summary>Releases the lock only when <paramref name="owner"/> holds it; anyone else's call changes nothing.</summary>
    Task ReleaseLockAsync(string name, string owner);
}
