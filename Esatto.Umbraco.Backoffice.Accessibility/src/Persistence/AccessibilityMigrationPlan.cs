using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Esatto.Umbraco.Backoffice.Accessibility.Persistence;

/// <summary>Migration plan for the package's tables. Runs on every start; each step is idempotent.</summary>
public sealed class AccessibilityMigrationPlan : MigrationPlan
{
    public AccessibilityMigrationPlan() : base("Esatto.Umbraco.Backoffice.Accessibility")
    {
        From(string.Empty)
            .To<CreateTablesMigration>("create-tables");
    }
}

/// <summary>Creates every <c>esattoA11y*</c> table that doesn't exist yet.</summary>
public sealed class CreateTablesMigration : AsyncMigrationBase
{
    public CreateTablesMigration(IMigrationContext context) : base(context) { }

    protected override Task MigrateAsync()
    {
        CreateIfMissing<SettingsEntity>(SettingsEntity.TableName);
        CreateIfMissing<ScanEntity>(ScanEntity.TableName);
        CreateIfMissing<ScanPageEntity>(ScanPageEntity.TableName);
        CreateIfMissing<FindingEntity>(FindingEntity.TableName);
        CreateIfMissing<FalsePositiveEntity>(FalsePositiveEntity.TableName);
        CreateIfMissing<ManualResultEntity>(ManualResultEntity.TableName);
        return Task.CompletedTask;
    }

    private void CreateIfMissing<T>(string table)
    {
        if (TableExists(table))
        {
            Logger.LogDebug("{Migration}: table {Table} already exists, skipping", nameof(CreateTablesMigration), table);
            return;
        }

        Create.Table<T>().Do();
    }
}
