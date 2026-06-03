using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Backoffice.Redirects;

/// <summary>
/// Creates the <c>Redirects</c> table from <see cref="RedirectEntity"/> if
/// it doesn't already exist. Idempotent — safe to re-run.
/// </summary>
/// <remarks>
/// On fresh installs this creates the table including the <c>siteKey</c>
/// column and the composite unique index. On legacy-Esatto installs the
/// preceding <see cref="RenameLegacyTableMigration"/> already renamed the
/// existing table to <c>Redirects</c>, so this step no-ops.
/// </remarks>
public sealed class AddRedirectsTableMigration : AsyncMigrationBase
{
    public AddRedirectsTableMigration(IMigrationContext context) : base(context) { }

    protected override Task MigrateAsync()
    {
        if (TableExists(RedirectEntity.TableName))
        {
            Logger.LogDebug(
                "{Migration}: table {Table} already exists, skipping",
                nameof(AddRedirectsTableMigration), RedirectEntity.TableName);
            return Task.CompletedTask;
        }

        Create.Table<RedirectEntity>().Do();
        return Task.CompletedTask;
    }
}
