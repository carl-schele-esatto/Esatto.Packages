using System.Data.Common;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using NPoco;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.SqlSyntax;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Esatto.Umbraco.Backoffice.SeoReport.Persistence;

/// <summary>
/// <see cref="ISeoReportStore"/> over Umbraco's database (NPoco + scopes), like the SEO Health and Accessibility packages.
/// Works on SQL Server and SQLite: no <c>MERGE</c>, no <c>INSERT OR REPLACE</c>, and every identifier in hand-written SQL
/// comes from the database's syntax provider.
/// </summary>
public sealed class NPocoSeoReportStore(IScopeProvider scopeProvider, TimeProvider? timeProvider = null) : ISeoReportStore
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public Task<ReportEntity?> GetReportAsync(Period period)
        => Read(db =>
        {
            var row = db.SingleOrDefaultById<ReportEntity>(period.ToString());
            if (row is null) return null;

            // The column has no time zone and SQL Server hands back Kind Unspecified; it is always UTC.
            row.FetchedUtc = DateTime.SpecifyKind(row.FetchedUtc, DateTimeKind.Utc);
            return (ReportEntity?)row;
        });

    public Task SaveReportAsync(ReportEntity report)
    {
        var period = report.ToPeriod(); // refuses anything but YYYY-MM before it reaches a key or a statement
        return Write(db => UpsertReport(db, new ReportEntity
        {
            Period = period.ToString(),
            Json = report.Json,
            FetchedUtc = report.FetchedUtc,
            ContractVersion = report.ContractVersion,
        }));
    }

    public Task<IReadOnlyList<Period>> ListPeriodsAsync()
        => Read<IReadOnlyList<Period>>(db =>
        {
            var periods = new List<Period>();
            foreach (var text in db.Fetch<string>(ListPeriodsSql(db.SqlContext.SqlSyntax)))
            {
                // A row someone edited by hand into something else is skipped, not fatal.
                if (Period.TryParse(text, out var period)) periods.Add(period);
            }

            return periods;
        });

    public Task<string?> GetNoteAsync(Period period)
        => Read(db => db.SingleOrDefaultById<NoteEntity>(period.ToString())?.Text);

    public Task SaveNoteAsync(Period period, string? text)
    {
        if (!string.IsNullOrEmpty(text)) StoreGuards.Note(text);

        return Write(db =>
        {
            if (string.IsNullOrEmpty(text))
                db.Execute(DeleteNoteSql(db.SqlContext.SqlSyntax), period.ToString());
            else
                UpsertNote(db, new NoteEntity { Period = period.ToString(), Text = text });
        });
    }

    public Task<string?> GetDocumentAsync(string key)
    {
        StoreGuards.DocumentKey(key);
        return Read(db => db.SingleOrDefaultById<DocumentEntity>(key)?.Json);
    }

    public Task SaveDocumentAsync(string key, string json)
    {
        StoreGuards.DocumentKey(key);
        return Write(db => UpsertDocument(db, new DocumentEntity { Key = key, Json = json, UpdatedUtc = _time.GetUtcNow().UtcDateTime }));
    }

    public Task DeleteDocumentAsync(string key)
    {
        StoreGuards.DocumentKey(key);
        return Write(db => db.Execute(DeleteDocumentSql(db.SqlContext.SqlSyntax), key));
    }

    public Task<bool> TryAcquireLockAsync(string name, string owner, TimeSpan ttl)
    {
        StoreGuards.Lock(name, owner);
        StoreGuards.Lifetime(ttl);
        var now = _time.GetUtcNow().UtcDateTime;
        return Write(db => TryAcquireLock(db, name, owner, now, now + ttl));
    }

    public Task ReleaseLockAsync(string name, string owner)
    {
        StoreGuards.Lock(name, owner);
        return Write(db => db.Execute(ReleaseLockSql(db.SqlContext.SqlSyntax), name, owner));
    }

    // The hand-written statements. Each is rendered through the syntax provider (GetQuotedTableName / GetQuotedColumnName)
    // so it is valid on the database in use: "key" is a reserved word in SQL Server, and "name", "owner" and "text" are
    // keyword-like. Internal static so the tests can render them in both dialects without a database.

    /// <summary>The archived months, oldest first (<c>YYYY-MM</c> sorts as text).</summary>
    internal static string ListPeriodsSql(ISqlSyntaxProvider syntax)
        => $"SELECT {Column(syntax, "period")} FROM {Table(syntax, ReportEntity.TableName)} ORDER BY {Column(syntax, "period")} ASC";

    internal static string UpdateReportSql(ISqlSyntaxProvider syntax)
        => $"UPDATE {Table(syntax, ReportEntity.TableName)} SET {Column(syntax, "json")} = @0, {Column(syntax, "fetchedUtc")} = @1, "
           + $"{Column(syntax, "contractVersion")} = @2 WHERE {Column(syntax, "period")} = @3";

    internal static string UpdateNoteSql(ISqlSyntaxProvider syntax)
        => $"UPDATE {Table(syntax, NoteEntity.TableName)} SET {Column(syntax, "text")} = @0 WHERE {Column(syntax, "period")} = @1";

    internal static string DeleteNoteSql(ISqlSyntaxProvider syntax)
        => $"DELETE FROM {Table(syntax, NoteEntity.TableName)} WHERE {Column(syntax, "period")} = @0";

    internal static string UpdateDocumentSql(ISqlSyntaxProvider syntax)
        => $"UPDATE {Table(syntax, DocumentEntity.TableName)} SET {Column(syntax, "json")} = @0, {Column(syntax, "updatedUtc")} = @1 "
           + $"WHERE {Column(syntax, "key")} = @2";

    internal static string DeleteDocumentSql(ISqlSyntaxProvider syntax)
        => $"DELETE FROM {Table(syntax, DocumentEntity.TableName)} WHERE {Column(syntax, "key")} = @0";

    /// <summary>
    /// Takes the lock row when it is ours or has lapsed, in one statement: @0 new owner, @1 new expiry, @2 name, @3 the owner
    /// that may renew, @4 now. The "ours or expired" test runs in the database, on its own row lock, not in this process.
    /// </summary>
    internal static string AcquireLockSql(ISqlSyntaxProvider syntax)
        => $"UPDATE {Table(syntax, LockEntity.TableName)} SET {Column(syntax, "owner")} = @0, {Column(syntax, "expiresUtc")} = @1 "
           + $"WHERE {Column(syntax, "name")} = @2 AND ({Column(syntax, "owner")} = @3 OR {Column(syntax, "expiresUtc")} <= @4)";

    /// <summary>
    /// Adds the lock row only when there is none: @0 name, @1 owner, @2 expiry, @3 name again (for the existence test). An
    /// existing row, live or not, makes it insert nothing and report 0 rows, which is the ordinary "someone holds it" outcome
    /// and throws nothing; the primary key stays as the backstop for two instances that both saw no row.
    /// </summary>
    internal static string InsertLockSql(ISqlSyntaxProvider syntax)
        => $"INSERT INTO {Table(syntax, LockEntity.TableName)} ({Column(syntax, "name")}, {Column(syntax, "owner")}, {Column(syntax, "expiresUtc")}) "
           + $"SELECT @0, @1, @2 WHERE NOT EXISTS (SELECT 1 FROM {Table(syntax, LockEntity.TableName)} WHERE {Column(syntax, "name")} = @3)";

    internal static string ReleaseLockSql(ISqlSyntaxProvider syntax)
        => $"DELETE FROM {Table(syntax, LockEntity.TableName)} WHERE {Column(syntax, "name")} = @0 AND {Column(syntax, "owner")} = @1";

    private static string Table(ISqlSyntaxProvider syntax, string table) => syntax.GetQuotedTableName(table);

    private static string Column(ISqlSyntaxProvider syntax, string column) => syntax.GetQuotedColumnName(column);

    // Every write below starts with a write, never a read. Umbraco runs SQLite in WAL mode with deferred transactions: a
    // transaction that reads first and writes after another one committed fails with SQLITE_BUSY_SNAPSHOT. Writing first
    // takes the write lock up front (others wait for it), and on SQL Server it avoids a select-then-insert race. The same
    // update-then-insert shape works on both databases, which is why there is no MERGE or INSERT OR REPLACE here.

    internal static void UpsertReport(IUmbracoDatabase db, ReportEntity row)
    {
        var sql = UpdateReportSql(db.SqlContext.SqlSyntax);
        object[] args = [row.Json, row.FetchedUtc, row.ContractVersion, row.Period];
        if (db.Execute(sql, args) == 0) InsertOrUpdate(db, row, sql, args);
    }

    internal static void UpsertNote(IUmbracoDatabase db, NoteEntity row)
    {
        var sql = UpdateNoteSql(db.SqlContext.SqlSyntax);
        object[] args = [row.Text, row.Period];
        if (db.Execute(sql, args) == 0) InsertOrUpdate(db, row, sql, args);
    }

    internal static void UpsertDocument(IUmbracoDatabase db, DocumentEntity row)
    {
        var sql = UpdateDocumentSql(db.SqlContext.SqlSyntax);
        object[] args = [row.Json, row.UpdatedUtc, row.Key];
        if (db.Execute(sql, args) == 0) InsertOrUpdate(db, row, sql, args);
    }

    /// <summary>
    /// The lock, settled by the database. (1) The conditional UPDATE takes a row that is ours or has lapsed. (2) If it touched
    /// nothing there is no row or someone else holds it, so the conditional INSERT adds the row only when none exists; a row
    /// that is there makes it report 0 rows without throwing, so a held lock is refused without an exception. (3) With 0 rows
    /// inserted the UPDATE runs once more, in case the lock lapsed between the two statements, and its result is the answer.
    /// Two instances that both find no row both reach the INSERT, and the primary key on the name lets exactly one through.
    /// The loser's INSERT throws; that is read as a lost race only when the row is there now (the retry UPDATE then answers:
    /// false for the winner's live lock, true if it was this same owner). With no row behind the failure it was something
    /// else (a timeout, permissions, a lost connection), and the original exception propagates rather than becoming "held".
    /// </summary>
    internal static bool TryAcquireLock(IUmbracoDatabase db, string name, string owner, DateTime now, DateTime expires)
    {
        var syntax = db.SqlContext.SqlSyntax;
        var update = AcquireLockSql(syntax);
        object[] updateArgs = [owner, expires, name, owner, now];
        if (db.Execute(update, updateArgs) == 1) return true;

        int inserted;
        try
        {
            inserted = db.Execute(InsertLockSql(syntax), name, owner, expires, name);
        }
        catch (DbException)
        {
            if (db.SingleOrDefaultById<LockEntity>(name) is null) throw;
            return db.Execute(update, updateArgs) == 1;
        }

        return inserted == 1 || db.Execute(update, updateArgs) == 1;
    }

    /// <summary>
    /// Inserts the row after an UPDATE found nothing. If the INSERT throws, someone else inserting the same key meanwhile is
    /// the expected cause: the UPDATE then finds their row and the save lands on it. If the UPDATE still finds nothing the
    /// INSERT failed for another reason (a timeout, permissions, a lost connection), and nothing was saved, so the original
    /// exception propagates instead of the save being reported as done.
    /// </summary>
    private static void InsertOrUpdate<T>(IUmbracoDatabase db, T row, string updateSql, object[] updateArgs)
    {
        try
        {
            db.Insert(row);
        }
        catch (DbException)
        {
            if (db.Execute(updateSql, updateArgs) == 0) throw;
        }
    }

    private Task<T> Read<T>(Func<IUmbracoDatabase, T> read)
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        return Task.FromResult(read(scope.Database));
    }

    private Task<T> Write<T>(Func<IUmbracoDatabase, T> write)
    {
        using var scope = scopeProvider.CreateScope();
        var result = write(scope.Database);
        scope.Complete();
        return Task.FromResult(result);
    }

    private Task Write(Action<IUmbracoDatabase> write) => Write(db => { write(db); return 0; });
}
