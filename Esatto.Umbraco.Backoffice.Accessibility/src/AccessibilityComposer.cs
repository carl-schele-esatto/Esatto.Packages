using Esatto.Umbraco.Backoffice.Accessibility.Persistence;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations.Upgrade;

namespace Esatto.Umbraco.Backoffice.Accessibility;

/// <summary>
/// Wires the package into Umbraco: services, and the migration plan that creates the tables at
/// startup. Composers are auto-discovered from any referenced assembly, so the dashboard and API need
/// no consumer-side wiring; the dashboard itself is in umbraco-package.json.
/// </summary>
public sealed class AccessibilityComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddAccessibility();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, RunAccessibilityMigration>();
    }
}

public sealed class RunAccessibilityMigration(
    ICoreScopeProvider scopeProvider,
    IMigrationPlanExecutor executor,
    IKeyValueService keyValueService,
    IRuntimeState runtimeState) : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    public async Task HandleAsync(UmbracoApplicationStartingNotification notification, CancellationToken cancellationToken)
    {
        if (runtimeState.Level != RuntimeLevel.Run) return;

        var upgrader = new Upgrader(new AccessibilityMigrationPlan());
        await upgrader.ExecuteAsync(executor, scopeProvider, keyValueService);
    }
}
