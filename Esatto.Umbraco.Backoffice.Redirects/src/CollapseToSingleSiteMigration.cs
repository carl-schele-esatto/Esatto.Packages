using Microsoft.Extensions.Logging;
using NPoco;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Esatto.Umbraco.Backoffice.Redirects;

/// <summary>
/// Converts a <c>Redirects</c> table carried over from the old multi-site
/// Backoffice.Redirects package to the single-site schema: drops the composite
/// (siteKey, oldPath) unique index, drops the <c>siteKey</c> column, and ensures
/// a single-column unique index on <c>oldPath</c>. Idempotent — a no-op on fresh
/// single-site installs (the table is created single-site by the add step).
/// </summary>
/// <remarks>
/// Single-site data is assumed (every legacy row had an empty site key), so
/// flattening cannot produce an oldPath collision; no dedup is required.
/// </remarks>
public sealed class CollapseToSingleSiteMigration : AsyncMigrationBase
{
    private const string SiteKeyColumn = "siteKey";

    public CollapseToSingleSiteMigration(IMigrationContext context) : base(context) { }

    protected override Task MigrateAsync()
    {
        if (!TableExists(RedirectEntity.TableName))
        {
            Logger.LogDebug(
                "{Migration}: table {Table} not present, skipping",
                nameof(CollapseToSingleSiteMigration), RedirectEntity.TableName);
            return Task.CompletedTask;
        }

        // 1. Drop the legacy multi-site composite unique index if present.
        if (IndexExists(RedirectEntity.TableName, RedirectEntity.LegacyCompositeIndexName))
        {
            Delete.Index(RedirectEntity.LegacyCompositeIndexName)
                .OnTable(RedirectEntity.TableName).Do();
            Logger.LogInformation(
                "{Migration}: dropped legacy composite index {Index}",
                nameof(CollapseToSingleSiteMigration), RedirectEntity.LegacyCompositeIndexName);
        }

        // 2. Drop the siteKey column if present.
        if (ColumnExists(RedirectEntity.TableName, SiteKeyColumn))
        {
            if (DatabaseType == DatabaseType.SQLite)
            {
                // A fresh SQLite install never has this column; reaching here means a
                // hand-modified DB. Older SQLite providers lack reliable DROP COLUMN —
                // log and skip rather than emit SQL it can't run.
                Logger.LogWarning(
                    "{Migration}: {Column} present on a SQLite {Table}; skipping DROP COLUMN. Recreate the table if this is unexpected.",
                    nameof(CollapseToSingleSiteMigration), SiteKeyColumn, RedirectEntity.TableName);
            }
            else
            {
                Database.Execute(
                    $"ALTER TABLE [{RedirectEntity.TableName}] DROP COLUMN [{SiteKeyColumn}]");
                Logger.LogInformation(
                    "{Migration}: dropped {Column} column",
                    nameof(CollapseToSingleSiteMigration), SiteKeyColumn);
            }
        }

        // 3. Ensure a single-column UNIQUE index on oldPath. If an index with the
        // canonical name exists but is NOT unique (legacy esatto rename path),
        // drop and recreate it unique.
        var defined = Database.SqlContext.SqlSyntax
            .GetDefinedIndexes(Database)
            .Where(x => string.Equals(x.Item1, RedirectEntity.TableName, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(x.Item2, RedirectEntity.OldPathIndexName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (defined.Count > 0 && defined.Any(x => !x.Item4))
        {
            Delete.Index(RedirectEntity.OldPathIndexName).OnTable(RedirectEntity.TableName).Do();
            defined.Clear();
        }

        if (defined.Count == 0)
        {
            Create.Index(RedirectEntity.OldPathIndexName)
                .OnTable(RedirectEntity.TableName)
                .OnColumn("oldPath").Ascending()
                .WithOptions().Unique()
                .Do();
            Logger.LogInformation(
                "{Migration}: ensured unique index {Index}",
                nameof(CollapseToSingleSiteMigration), RedirectEntity.OldPathIndexName);
        }

        return Task.CompletedTask;
    }

    // Cross-database index lookup (SQL Server + SQLite). GetDefinedIndexes returns
    // one tuple per (table, index, column, isUnique).
    private bool IndexExists(string table, string indexName)
        => Database.SqlContext.SqlSyntax
            .GetDefinedIndexes(Database)
            .Any(x => string.Equals(x.Item1, table, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(x.Item2, indexName, StringComparison.OrdinalIgnoreCase));
}
