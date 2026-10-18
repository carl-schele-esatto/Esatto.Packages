using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Migrations.Upgrade;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// Migration plan for the scheduled task settings. Each step is idempotent, so re-runs are safe.
/// </summary>
public sealed class ScheduledTasksMigrationPlan : MigrationPlan
{
    public ScheduledTasksMigrationPlan() : base("Esatto.Umbraco.Backoffice.ScheduledTasks")
    {
        From(string.Empty)
            .To<AddScheduledTaskSettingsTableMigration>("add-scheduled-task-settings-table")
            .To<AddScheduledTaskPropertiesColumnMigration>("add-scheduled-task-properties-column")
            .To<AddScheduledTaskCalendarColumnsMigration>("add-scheduled-task-calendar-columns")
            .To<AddScheduledTaskOccurrencesTableMigration>("add-scheduled-task-occurrences-table");
    }
}

/// <summary>
/// Creates the <c>ScheduledTaskOccurrences</c> table (each server's last handled calendar occurrence
/// of a recurring task) if it doesn't already exist.
/// </summary>
public sealed class AddScheduledTaskOccurrencesTableMigration : AsyncMigrationBase
{
    public AddScheduledTaskOccurrencesTableMigration(IMigrationContext context) : base(context) { }

    protected override Task MigrateAsync()
    {
        if (!TableExists(ScheduledTaskOccurrenceEntity.TableName)) Create.Table<ScheduledTaskOccurrenceEntity>().Do();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Adds the calendar schedule columns (<c>scheduleJson</c>, <c>lastScheduledUtc</c>) to tables
/// created before calendar schedules existed. A no-op when they're already there.
/// </summary>
public sealed class AddScheduledTaskCalendarColumnsMigration : AsyncMigrationBase
{
    public AddScheduledTaskCalendarColumnsMigration(IMigrationContext context) : base(context) { }

    protected override Task MigrateAsync()
    {
        foreach (var column in new[] { ScheduledTaskSettingEntity.ScheduleColumn, ScheduledTaskSettingEntity.LastScheduledColumn })
        {
            if (ColumnExists(ScheduledTaskSettingEntity.TableName, column)) continue;
            AddColumn<ScheduledTaskSettingEntity>(column);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Adds the <c>propertiesJson</c> column to tables created before task properties existed.
/// Fresh installs already get it from the table definition, so this is then a no-op.
/// </summary>
public sealed class AddScheduledTaskPropertiesColumnMigration : AsyncMigrationBase
{
    public AddScheduledTaskPropertiesColumnMigration(IMigrationContext context) : base(context) { }

    protected override Task MigrateAsync()
    {
        if (ColumnExists(ScheduledTaskSettingEntity.TableName, ScheduledTaskSettingEntity.PropertiesColumn))
        {
            Logger.LogDebug(
                "{Migration}: column {Column} already exists, skipping",
                nameof(AddScheduledTaskPropertiesColumnMigration), ScheduledTaskSettingEntity.PropertiesColumn);
            return Task.CompletedTask;
        }

        AddColumn<ScheduledTaskSettingEntity>(ScheduledTaskSettingEntity.PropertiesColumn);
        return Task.CompletedTask;
    }
}

/// <summary>Creates the <c>ScheduledTaskSettings</c> table if it doesn't already exist.</summary>
public sealed class AddScheduledTaskSettingsTableMigration : AsyncMigrationBase
{
    public AddScheduledTaskSettingsTableMigration(IMigrationContext context) : base(context) { }

    protected override Task MigrateAsync()
    {
        if (TableExists(ScheduledTaskSettingEntity.TableName))
        {
            Logger.LogDebug(
                "{Migration}: table {Table} already exists, skipping",
                nameof(AddScheduledTaskSettingsTableMigration), ScheduledTaskSettingEntity.TableName);
            return Task.CompletedTask;
        }

        Create.Table<ScheduledTaskSettingEntity>().Do();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Whether the <c>ScheduledTaskSettings</c> table is at the plan's final state on this run, so the
/// settings can be read. Until <see cref="RunScheduledTasksMigration"/> has completed the plan the
/// columns may not exist yet, even when Umbraco's runtime level is already <c>Run</c> (unattended
/// upgrades start the hosted services and reach <c>Run</c> before this package's migration runs).
/// </summary>
public sealed class ScheduledTasksSchemaState
{
    private volatile bool _isReady;

    public bool IsReady => _isReady;

    internal void MarkReady() => _isReady = true;
}

/// <summary>Runs <see cref="ScheduledTasksMigrationPlan"/> at application start.</summary>
public sealed class RunScheduledTasksMigration : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly IMigrationPlanExecutor _executor;
    private readonly IKeyValueService _keyValueService;
    private readonly IRuntimeState _runtimeState;
    private readonly ScheduledTasksSchemaState _schema;

    public RunScheduledTasksMigration(
        ICoreScopeProvider scopeProvider,
        IMigrationPlanExecutor executor,
        IKeyValueService keyValueService,
        IRuntimeState runtimeState,
        ScheduledTasksSchemaState schema)
    {
        _scopeProvider = scopeProvider;
        _executor = executor;
        _keyValueService = keyValueService;
        _runtimeState = runtimeState;
        _schema = schema;
    }

    [Obsolete("Use the constructor taking ScheduledTasksSchemaState; without it the settings store never learns the schema is ready.")]
    public RunScheduledTasksMigration(
        ICoreScopeProvider scopeProvider,
        IMigrationPlanExecutor executor,
        IKeyValueService keyValueService,
        IRuntimeState runtimeState)
        : this(scopeProvider, executor, keyValueService, runtimeState, new ScheduledTasksSchemaState())
    {
    }

    public async Task HandleAsync(UmbracoApplicationStartingNotification notification, CancellationToken cancellationToken)
    {
        if (_runtimeState.Level != RuntimeLevel.Run) return;

        var plan = new ScheduledTasksMigrationPlan();
        var result = await new Upgrader(plan).ExecuteAsync(_executor, _scopeProvider, _keyValueService);

        // Only a plan that reached its final state has every column the settings store reads.
        if (result.Successful && result.FinalState == plan.FinalState) _schema.MarkReady();
    }
}
