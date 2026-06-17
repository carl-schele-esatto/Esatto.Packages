using Microsoft.Extensions.Logging;
using NPoco;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Esatto.Umbraco.Backoffice.Redirects;

/// <summary>
/// Renames the legacy <c>esattoRedirects</c> table to the canonical
/// <c>Redirects</c> name. Skipped on fresh installs (no legacy table) and
/// on re-runs (already renamed).
/// </summary>
/// <remarks>
/// <para>
/// Designed for Esatto.Web's transition from the in-tree implementation —
/// which used the brand-named table <c>esattoRedirects</c> — to this
/// vendor-neutral package. <c>sp_rename</c> is a metadata-only operation
/// that holds a brief schema lock and is atomic; no row data is touched.
/// </para>
/// <para>
/// Indexes named <c>IX_esattoRedirects_*</c> are also renamed to their
/// <c>IX_Redirects_*</c> equivalents so subsequent migrations see the
/// expected names.
/// </para>
/// </remarks>
public sealed class RenameLegacyTableMigration : AsyncMigrationBase
{
    private const string LegacyTableName = "esattoRedirects";
    private const string LegacyCompositeIndexName = "IX_esattoRedirects_siteKey_oldPath";
    private const string LegacyOldPathIndexName = "IX_esattoRedirects_oldPath";

    public RenameLegacyTableMigration(IMigrationContext context) : base(context) { }

    protected override Task MigrateAsync()
    {
        // The legacy esattoRedirects table only ever existed in Esatto.Web's
        // SQL Server implementation. There is nothing to rename on SQLite, and
        // sp_rename is SQL-Server-only syntax — so skip this step entirely.
        if (DatabaseType == DatabaseType.SQLite)
        {
            Logger.LogDebug(
                "{Migration}: SQLite database, no legacy SQL Server table to rename, skipping",
                nameof(RenameLegacyTableMigration));
            return Task.CompletedTask;
        }

        // Already-renamed (re-run after success) OR fresh install (no legacy):
        // both cases are safe no-ops. Order matters — check the new name first
        // so the "already renamed" path doesn't fall through to the legacy
        // check, which would also be false on a fresh install.
        if (TableExists(RedirectEntity.TableName))
        {
            Logger.LogDebug(
                "{Migration}: target table {Table} already exists, skipping rename",
                nameof(RenameLegacyTableMigration), RedirectEntity.TableName);
            return Task.CompletedTask;
        }

        if (!TableExists(LegacyTableName))
        {
            Logger.LogDebug(
                "{Migration}: no legacy table {Legacy} present, skipping (fresh install)",
                nameof(RenameLegacyTableMigration), LegacyTableName);
            return Task.CompletedTask;
        }

        // sp_rename is metadata-only + atomic. Lock is held briefly while
        // the system catalogs are updated; row data is never touched.
        Logger.LogInformation(
            "{Migration}: renaming legacy table {Legacy} -> {Target}",
            nameof(RenameLegacyTableMigration), LegacyTableName, RedirectEntity.TableName);
        Database.Execute($"EXEC sp_rename '{LegacyTableName}', '{RedirectEntity.TableName}'");

        // Indexes need explicit renames; sp_rename on a table doesn't cascade
        // to its indexes. Index sp_rename syntax: 'Table.IndexName' + 'INDEX' type.
        if (IndexExists(RedirectEntity.TableName, LegacyCompositeIndexName))
        {
            Logger.LogInformation(
                "{Migration}: renaming index {Legacy} -> {Target}",
                nameof(RenameLegacyTableMigration),
                LegacyCompositeIndexName,
                RedirectEntity.LegacyCompositeIndexName);
            Database.Execute(
                $"EXEC sp_rename '{RedirectEntity.TableName}.{LegacyCompositeIndexName}', " +
                $"'{RedirectEntity.LegacyCompositeIndexName}', 'INDEX'");
        }

        // The legacy single-column index is normalized below by collapse-to-single-site step.
        // Don't drop it here — keep this migration focused on rename-only.
        if (IndexExists(RedirectEntity.TableName, LegacyOldPathIndexName))
        {
            Logger.LogInformation(
                "{Migration}: renaming legacy index {Legacy} -> {Target} (normalized below by collapse-to-single-site step)",
                nameof(RenameLegacyTableMigration),
                LegacyOldPathIndexName,
                RedirectEntity.OldPathIndexName);
            Database.Execute(
                $"EXEC sp_rename '{RedirectEntity.TableName}.{LegacyOldPathIndexName}', " +
                $"'{RedirectEntity.OldPathIndexName}', 'INDEX'");
        }

        return Task.CompletedTask;
    }

    // Cross-database index lookup — see CollapseToSingleSiteMigration for why the
    // SQL-Server catalog-view query was replaced with GetDefinedIndexes. This
    // rename path is SQL-Server-only legacy migration (sp_rename), but the helper
    // is kept database-agnostic so the guard itself never throws on SQLite.
    private bool IndexExists(string table, string indexName)
        => Database.SqlContext.SqlSyntax
            .GetDefinedIndexes(Database)
            .Any(x => string.Equals(x.Item1, table, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(x.Item2, indexName, StringComparison.OrdinalIgnoreCase));
}
