using System.Data.Common;
using NPoco;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.SqlSyntax;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Esatto.Umbraco.Backoffice.Seo.Persistence;

/// <summary><see cref="ISeoStore"/> over Umbraco's database (NPoco + scopes), like the Accessibility package.</summary>
public sealed class NPocoSeoStore(IScopeProvider scopeProvider) : ISeoStore
{
    public Task<string?> GetSettingsJsonAsync()
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        return Task.FromResult(scope.Database.SingleOrDefaultById<SettingsEntity>(1)?.Json);
    }

    public Task SaveSettingsJsonAsync(string json)
    {
        using var scope = scopeProvider.CreateScope();
        var db = scope.Database;
        var now = DateTime.UtcNow;
        // Write first (SQLite WAL + deferred transactions, see the Accessibility store), insert if nothing was there.
        if (db.Execute($"UPDATE {SettingsEntity.TableName} SET json = @0, updatedUtc = @1 WHERE id = 1", json, now) == 0)
        {
            var row = new SettingsEntity { Id = 1, Json = json, UpdatedUtc = now };
            try
            {
                db.Insert(row);
            }
            catch (DbException)
            {
                db.Update(row); // someone inserted it meanwhile
            }
        }

        scope.Complete();
        return Task.CompletedTask;
    }

    public Task<int> CreateScanAsync(ScanEntity scan) => Write(db => { db.Insert(scan); return scan.Id; });

    public Task UpdateScanAsync(ScanEntity scan) => Write(db => db.Update(scan));

    public Task<ScanEntity?> GetScanAsync(int id) => Read(db => (ScanEntity?)db.SingleOrDefaultById<ScanEntity>(id));

    /// <summary>Every column except the (large) JSON ones.</summary>
    private static readonly string[] SummaryColumns =
    [
        "id", "siteKey", "startedUtc", "finishedUtc", "trigger", "userName", "status", "pagesTotal", "pagesDone", "pagesFailed",
        "pagesSkipped", "score", "scoreVersion", "critical", "warning", "notice", "cultures", "trustedHosts", "message",
    ];

    /// <summary>
    /// The site's scans, newest first, summary columns only. Column names are quoted in the database's dialect:
    /// "trigger" is a reserved word in SQL Server (SQLite lets it pass unquoted).
    /// </summary>
    internal static string ScansSql(ISqlSyntaxProvider syntax)
        => $"SELECT {string.Join(", ", SummaryColumns.Select(syntax.GetQuotedColumnName))} FROM {syntax.GetQuotedTableName(ScanEntity.TableName)} "
            + $"WHERE {syntax.GetQuotedColumnName("siteKey")} = @0 "
            + $"ORDER BY {syntax.GetQuotedColumnName("startedUtc")} DESC, {syntax.GetQuotedColumnName("id")} DESC";

    public Task<IReadOnlyList<ScanEntity>> GetScansAsync(Guid siteKey, int take)
        => Read<IReadOnlyList<ScanEntity>>(db => db.Fetch<ScanEntity>(ScansSql(db.SqlContext.SqlSyntax), siteKey)
            .Take(take)
            .ToList());

    public Task<int> AddPageAsync(PageEntity page) => Write(db => { db.Insert(page); return page.Id; });

    public Task UpdatePageAsync(PageEntity page) => Write(db => db.Update(page));

    public Task<IReadOnlyList<PageEntity>> GetPagesAsync(int scanId)
        => Read<IReadOnlyList<PageEntity>>(db => db.Fetch<PageEntity>($"SELECT * FROM {PageEntity.TableName} WHERE scanId = @0", scanId));

    public Task PruneScansAsync(IEnumerable<int> scanIds)
    {
        var ids = scanIds.ToList();
        if (ids.Count == 0) return Task.CompletedTask;
        return Write(db =>
        {
            foreach (var id in ids)
            {
                db.Execute($"DELETE FROM {PageEntity.TableName} WHERE scanId = @0", id);
                db.Execute($"UPDATE {ScanEntity.TableName} SET probeJson = NULL, linksJson = NULL WHERE id = @0 AND (probeJson IS NOT NULL OR linksJson IS NOT NULL)", id);
            }
        });
    }

    public Task<IReadOnlyList<IgnoreEntity>> GetIgnoresAsync(Guid siteKey)
        => Read<IReadOnlyList<IgnoreEntity>>(db => db.Fetch<IgnoreEntity>($"SELECT * FROM {IgnoreEntity.TableName} WHERE siteKey = @0", siteKey));

    public Task<int> AddIgnoreAsync(IgnoreEntity ignore) => Write(db => { db.Insert(ignore); return ignore.Id; });

    public Task<IgnoreEntity?> GetIgnoreAsync(int id) => Read(db => (IgnoreEntity?)db.SingleOrDefaultById<IgnoreEntity>(id));

    public Task RemoveIgnoreAsync(int id) => Write(db => db.Execute($"DELETE FROM {IgnoreEntity.TableName} WHERE id = @0", id));

    public Task MarkRunningAsIncompleteAsync()
        => Write(db => db.Execute($"UPDATE {ScanEntity.TableName} SET status = @0 WHERE status = @1", ScanStatus.Incomplete, ScanStatus.Running));

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
