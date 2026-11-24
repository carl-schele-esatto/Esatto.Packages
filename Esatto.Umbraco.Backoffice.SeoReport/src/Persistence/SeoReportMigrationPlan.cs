using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Esatto.Umbraco.Backoffice.SeoReport.Persistence;

/// <summary>Migration plan for the package's tables. Runs on every start; each step is idempotent.</summary>
public sealed class SeoReportMigrationPlan : MigrationPlan
{
    public SeoReportMigrationPlan() : base("Esatto.Umbraco.Backoffice.SeoReport")
    {
        From(string.Empty)
            .To<CreateSeoReportTablesMigration>("create-tables");
    }
}

/// <summary>Creates every <c>esattoSeoReport*</c> table that doesn't exist yet.</summary>
public sealed class CreateSeoReportTablesMigration : AsyncMigrationBase
{
    public CreateSeoReportTablesMigration(IMigrationContext context) : base(context) { }

    protected override Task MigrateAsync()
    {
        CreateIfMissing<ReportEntity>(ReportEntity.TableName);
        CreateIfMissing<NoteEntity>(NoteEntity.TableName);
        CreateIfMissing<DocumentEntity>(DocumentEntity.TableName);
        CreateIfMissing<LockEntity>(LockEntity.TableName);
        return Task.CompletedTask;
    }

    private void CreateIfMissing<T>(string table)
    {
        if (TableExists(table))
        {
            Logger.LogDebug("{Migration}: table {Table} already exists, skipping", nameof(CreateSeoReportTablesMigration), table);
            return;
        }

        Create.Table<T>().Do();
    }
}
