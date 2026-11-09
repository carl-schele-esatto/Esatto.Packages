using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Esatto.Umbraco.Backoffice.Seo.Persistence;

/// <summary>Migration plan for the package's tables. Runs on every start; each step is idempotent.</summary>
public sealed class SeoMigrationPlan : MigrationPlan
{
    public SeoMigrationPlan() : base("Esatto.Umbraco.Backoffice.Seo")
    {
        From(string.Empty)
            .To<CreateSeoTablesMigration>("create-tables");
    }
}

/// <summary>Creates every <c>esattoSeo*</c> table that doesn't exist yet.</summary>
public sealed class CreateSeoTablesMigration : AsyncMigrationBase
{
    public CreateSeoTablesMigration(IMigrationContext context) : base(context) { }

    protected override Task MigrateAsync()
    {
        CreateIfMissing<SettingsEntity>(SettingsEntity.TableName);
        CreateIfMissing<ScanEntity>(ScanEntity.TableName);
        CreateIfMissing<PageEntity>(PageEntity.TableName);
        CreateIfMissing<IgnoreEntity>(IgnoreEntity.TableName);
        return Task.CompletedTask;
    }

    private void CreateIfMissing<T>(string table)
    {
        if (TableExists(table))
        {
            Logger.LogDebug("{Migration}: table {Table} already exists, skipping", nameof(CreateSeoTablesMigration), table);
            return;
        }

        Create.Table<T>().Do();
    }
}
