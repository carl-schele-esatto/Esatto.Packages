using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Migrations.Upgrade;

namespace Esatto.Umbraco.Backoffice.NavigationOrder;

/// <summary>Migration plan for the saved navigation order. Each step is idempotent, so re-runs are safe.</summary>
public sealed class NavigationOrderMigrationPlan : MigrationPlan
{
    public NavigationOrderMigrationPlan() : base("Esatto.Umbraco.Backoffice.NavigationOrder")
    {
        From(string.Empty)
            .To<AddNavigationOrderTableMigration>("add-navigation-order-table");
    }
}

/// <summary>Creates the <c>NavigationOrder</c> table if it doesn't already exist.</summary>
public sealed class AddNavigationOrderTableMigration : AsyncMigrationBase
{
    public AddNavigationOrderTableMigration(IMigrationContext context) : base(context) { }

    protected override Task MigrateAsync()
    {
        if (!TableExists(NavigationOrderEntity.TableName)) Create.Table<NavigationOrderEntity>().Do();
        return Task.CompletedTask;
    }
}

/// <summary>Runs <see cref="NavigationOrderMigrationPlan"/> at application start.</summary>
public sealed class RunNavigationOrderMigration : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly IMigrationPlanExecutor _executor;
    private readonly IKeyValueService _keyValueService;
    private readonly IRuntimeState _runtimeState;

    public RunNavigationOrderMigration(
        ICoreScopeProvider scopeProvider,
        IMigrationPlanExecutor executor,
        IKeyValueService keyValueService,
        IRuntimeState runtimeState)
    {
        _scopeProvider = scopeProvider;
        _executor = executor;
        _keyValueService = keyValueService;
        _runtimeState = runtimeState;
    }

    public async Task HandleAsync(UmbracoApplicationStartingNotification notification, CancellationToken cancellationToken)
    {
        if (_runtimeState.Level != RuntimeLevel.Run) return;

        await new Upgrader(new NavigationOrderMigrationPlan()).ExecuteAsync(_executor, _scopeProvider, _keyValueService);
    }
}
