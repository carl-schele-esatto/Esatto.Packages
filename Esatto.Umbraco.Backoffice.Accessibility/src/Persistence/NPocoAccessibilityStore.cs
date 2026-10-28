using System.Data.Common;
using NPoco;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Esatto.Umbraco.Backoffice.Accessibility.Persistence;

/// <summary><see cref="IAccessibilityStore"/> over Umbraco's database (NPoco + scopes), like the Redirects package.</summary>
public sealed class NPocoAccessibilityStore(IScopeProvider scopeProvider) : IAccessibilityStore
{
    public Task<string?> GetSettingsJsonAsync()
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        var row = scope.Database.SingleOrDefaultById<SettingsEntity>(1);
        return Task.FromResult(row?.Json);
    }

    public Task SaveSettingsJsonAsync(string json)
    {
        using var scope = scopeProvider.CreateScope();
        SaveSettings(scope.Database, new SettingsEntity { Id = 1, Json = json, UpdatedUtc = DateTime.UtcNow });
        scope.Complete();
        return Task.CompletedTask;
    }

    public Task<int> CreateScanAsync(ScanEntity scan)
    {
        using var scope = scopeProvider.CreateScope();
        scope.Database.Insert(scan);
        scope.Complete();
        return Task.FromResult(scan.Id);
    }

    public Task<ScanEntity?> GetScanAsync(int id)
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        return Task.FromResult<ScanEntity?>(scope.Database.SingleOrDefaultById<ScanEntity>(id));
    }

    public Task<IReadOnlyList<ScanEntity>> GetScansAsync(Guid siteKey, int take)
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        IReadOnlyList<ScanEntity> rows = scope.Database
            .Query<ScanEntity>()
            .Where(s => s.SiteKey == siteKey)
            .OrderByDescending(s => s.StartedUtc)
            .Limit(take)
            .ToList();
        return Task.FromResult(rows);
    }

    public Task UpdateScanAsync(ScanEntity scan)
    {
        using var scope = scopeProvider.CreateScope();
        scope.Database.Update(scan);
        scope.Complete();
        return Task.CompletedTask;
    }

    public Task SavePageAsync(ScanPageEntity page, IReadOnlyList<FindingEntity> findings)
    {
        using var scope = scopeProvider.CreateScope();
        SavePage(scope.Database, page, findings);
        scope.Complete();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ScanPageEntity>> GetPagesAsync(int scanId)
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        IReadOnlyList<ScanPageEntity> rows = scope.Database.Fetch<ScanPageEntity>(
            $"SELECT * FROM {ScanPageEntity.TableName} WHERE scanId = @0", scanId);
        return Task.FromResult(rows);
    }

    public Task<IReadOnlyList<FindingEntity>> GetFindingsAsync(int scanId)
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        IReadOnlyList<FindingEntity> rows = scope.Database.Fetch<FindingEntity>(
            $"SELECT * FROM {FindingEntity.TableName} WHERE scanId = @0", scanId);
        return Task.FromResult(rows);
    }

    public Task<IReadOnlyList<FindingEntity>> GetFindingSummariesAsync(int scanId)
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        IReadOnlyList<FindingEntity> rows = scope.Database.Fetch<FindingEntity>(
            $"SELECT id, scanId, scanPageId, ruleId, help, helpUrl, criteria, impact, kind, selector, mediaKey FROM {FindingEntity.TableName} WHERE scanId = @0", scanId);
        return Task.FromResult(rows);
    }

    public Task PruneScansAsync(IEnumerable<int> scanIds)
    {
        var ids = scanIds.ToList();
        if (ids.Count == 0) return Task.CompletedTask;

        using var scope = scopeProvider.CreateScope();
        foreach (var id in ids)
        {
            scope.Database.Execute($"DELETE FROM {FindingEntity.TableName} WHERE scanId = @0", id);
            scope.Database.Execute($"DELETE FROM {ScanPageEntity.TableName} WHERE scanId = @0", id);
        }

        scope.Complete();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FalsePositiveEntity>> GetFalsePositivesAsync(Guid siteKey)
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        IReadOnlyList<FalsePositiveEntity> rows = scope.Database.Fetch<FalsePositiveEntity>(
            $"SELECT * FROM {FalsePositiveEntity.TableName} WHERE siteKey = @0", siteKey);
        return Task.FromResult(rows);
    }

    public Task<int> AddFalsePositiveAsync(FalsePositiveEntity falsePositive)
    {
        using var scope = scopeProvider.CreateScope();
        scope.Database.Insert(falsePositive);
        scope.Complete();
        return Task.FromResult(falsePositive.Id);
    }

    public Task DeleteFalsePositiveAsync(int id)
    {
        using var scope = scopeProvider.CreateScope();
        scope.Database.Execute($"DELETE FROM {FalsePositiveEntity.TableName} WHERE id = @0", id);
        scope.Complete();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ManualResultEntity>> GetManualResultsAsync(Guid siteKey)
    {
        using var scope = scopeProvider.CreateScope(autoComplete: true);
        IReadOnlyList<ManualResultEntity> rows = scope.Database.Fetch<ManualResultEntity>(
            $"SELECT * FROM {ManualResultEntity.TableName} WHERE siteKey = @0", siteKey);
        return Task.FromResult(rows);
    }

    public Task UpsertManualResultAsync(ManualResultEntity result)
    {
        using var scope = scopeProvider.CreateScope();
        UpsertManualResult(scope.Database, result);
        scope.Complete();
        return Task.CompletedTask;
    }

    public Task DeleteManualResultAsync(Guid siteKey, string criterionId)
    {
        using var scope = scopeProvider.CreateScope();
        scope.Database.Execute($"DELETE FROM {ManualResultEntity.TableName} WHERE siteKey = @0 AND criterionId = @1", siteKey, criterionId);
        scope.Complete();
        return Task.CompletedTask;
    }

    // Every write below starts with a write, never a read. Umbraco runs SQLite in WAL mode with deferred
    // transactions: a transaction that reads first and writes after another one committed fails with
    // SQLITE_BUSY_SNAPSHOT, and the dashboard posts three pages at a time. Writing first takes the write
    // lock up front (others wait for it), and on SQL Server it avoids a select-then-insert race.

    internal static void SaveSettings(IDatabase db, SettingsEntity row)
    {
        var updated = db.Execute($"UPDATE {SettingsEntity.TableName} SET json = @0, updatedUtc = @1 WHERE id = @2", row.Json, row.UpdatedUtc, row.Id);
        if (updated == 0) InsertOrUpdate(db, row, () => db.Update(row));
    }

    internal static void SavePage(IDatabase db, ScanPageEntity page, IReadOnlyList<FindingEntity> findings)
    {
        const string samePage = "scanId = @0 AND contentKey = @1 AND culture = @2 AND viewport = @3";
        object[] args = [page.ScanId, page.ContentKey, page.Culture, page.Viewport];
        db.Execute($"DELETE FROM {FindingEntity.TableName} WHERE scanPageId IN (SELECT id FROM {ScanPageEntity.TableName} WHERE {samePage})", args);
        db.Execute($"DELETE FROM {ScanPageEntity.TableName} WHERE {samePage}", args);

        db.Insert(page);
        foreach (var finding in findings)
        {
            finding.ScanPageId = page.Id;
            finding.ScanId = page.ScanId;
            db.Insert(finding);
        }
    }

    internal static void UpsertManualResult(IDatabase db, ManualResultEntity result)
    {
        const string sql = $"UPDATE {ManualResultEntity.TableName} SET status = @0, note = @1, userName = @2, updatedUtc = @3 WHERE siteKey = @4 AND criterionId = @5";
        object[] args = [result.Status, result.Note is null ? DBNull.Value : result.Note, result.UserName, result.UpdatedUtc, result.SiteKey, result.CriterionId];
        if (db.Execute(sql, args) == 0) InsertOrUpdate(db, result, () => db.Execute(sql, args));
    }

    /// <summary>Inserts; if someone else inserted the same row in the meantime (unique key), updates theirs instead.</summary>
    private static void InsertOrUpdate<T>(IDatabase db, T row, Action update)
    {
        try
        {
            db.Insert(row);
        }
        catch (DbException)
        {
            update();
        }
    }
}
