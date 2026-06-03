using Microsoft.Extensions.Logging;
using NPoco;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Backoffice.Redirects;

/// <summary>
/// Ensures the <c>siteKey</c> column + composite unique index exist on the
/// <c>Redirects</c> table, and drops the legacy single-column unique index
/// if it survived the rename.
/// </summary>
/// <remarks>
/// <para>
/// On fresh installs this no-ops — the table was just created from
/// <see cref="RedirectEntity"/> which already includes the column + index.
/// </para>
/// <para>
/// On legacy-Esatto installs predating the siteKey concept (rare, but
/// possible if the Esatto in-tree migration <c>add-sitekey</c> never ran),
/// this adds the column with a NULL-then-backfill-then-NOT-NULL pattern.
/// Default backfill key is empty string — single-site semantics.
/// </para>
/// </remarks>
public sealed class EnsureSiteKeyColumnMigration : AsyncMigrationBase
{
    private const string ColumnName = "siteKey";
    private const string DefaultBackfillKey = "";

    public EnsureSiteKeyColumnMigration(IMigrationContext context) : base(context) { }

    protected override Task MigrateAsync()
    {
        if (!TableExists(RedirectEntity.TableName))
        {
            Logger.LogDebug(
                "{Migration}: table {Table} not present, skipping",
                nameof(EnsureSiteKeyColumnMigration), RedirectEntity.TableName);
            return Task.CompletedTask;
        }

        if (!ColumnExists(RedirectEntity.TableName, ColumnName))
        {
            if (DatabaseType == DatabaseType.SQLite)
            {
                // SQLite has no ALTER COLUMN, and a fresh install always gets the
                // siteKey column from Create.Table<RedirectEntity>(). Reaching here
                // on SQLite means a hand-modified table — log and skip rather than
                // emitting SQL Server syntax SQLite can't run.
                Logger.LogWarning(
                    "{Migration}: {Column} missing on a SQLite {Table} table; skipping SQL-Server-only ALTER. Recreate the table if this is unexpected.",
                    nameof(EnsureSiteKeyColumnMigration), ColumnName, RedirectEntity.TableName);
            }
            else
            {
                // Two-step: nullable add → backfill → alter NOT NULL. Avoids the
                // "cannot add NOT NULL column without default" error on Azure SQL.
                Database.Execute(
                    $"ALTER TABLE [{RedirectEntity.TableName}] ADD [{ColumnName}] NVARCHAR(64) NULL");
                Database.Execute(
                    $"UPDATE [{RedirectEntity.TableName}] SET [{ColumnName}] = @0 WHERE [{ColumnName}] IS NULL",
                    DefaultBackfillKey);
                Database.Execute(
                    $"ALTER TABLE [{RedirectEntity.TableName}] ALTER COLUMN [{ColumnName}] NVARCHAR(64) NOT NULL");

                Logger.LogInformation(
                    "{Migration}: added {Column} column and backfilled existing rows to empty key",
                    nameof(EnsureSiteKeyColumnMigration), ColumnName);
            }
        }

        if (IndexExists(RedirectEntity.TableName, RedirectEntity.LegacyOldPathIndexName))
        {
            Delete.Index(RedirectEntity.LegacyOldPathIndexName).OnTable(RedirectEntity.TableName).Do();
        }

        if (!IndexExists(RedirectEntity.TableName, RedirectEntity.OldPathSiteKeyIndexName))
        {
            Create.Index(RedirectEntity.OldPathSiteKeyIndexName)
                .OnTable(RedirectEntity.TableName)
                .OnColumn("siteKey").Ascending()
                .OnColumn("oldPath").Ascending()
                .WithOptions().Unique()
                .Do();
        }

        return Task.CompletedTask;
    }

    // Cross-database index lookup. The previous implementation queried the
    // SQL-Server-only catalog views (sys.indexes/sys.tables), which throws
    // "no such table: sys.indexes" on SQLite. GetDefinedIndexes is provided by
    // every ISqlSyntaxProvider (SQL Server and SQLite alike) and returns one
    // tuple per (table, index, column, isUnique).
    private bool IndexExists(string table, string indexName)
        => Database.SqlContext.SqlSyntax
            .GetDefinedIndexes(Database)
            .Any(x => string.Equals(x.Item1, table, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(x.Item2, indexName, StringComparison.OrdinalIgnoreCase));
}
