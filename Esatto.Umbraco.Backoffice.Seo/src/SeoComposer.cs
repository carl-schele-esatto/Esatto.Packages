using Esatto.Umbraco.Backoffice.Seo.Persistence;
using Esatto.Umbraco.Backoffice.Seo.Scanning;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations.Upgrade;
using Umbraco.Extensions;

namespace Esatto.Umbraco.Backoffice.Seo;

/// <summary>
/// Wires the package into Umbraco: services, the nightly scan job, and the migration plan that creates the
/// tables at startup. Composers are auto-discovered from any referenced assembly, so the dashboard, API and
/// nightly scan need no consumer-side wiring; the dashboard itself is in umbraco-package.json.
/// </summary>
public sealed class SeoComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddSeoHealth();
        builder.Services.AddRecurringBackgroundJob<SeoScanJob>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, RunSeoMigration>();
    }
}

/// <summary>Creates/updates the tables, and marks scans that a restart interrupted as incomplete.</summary>
public sealed class RunSeoMigration(
    ICoreScopeProvider scopeProvider,
    IMigrationPlanExecutor executor,
    IKeyValueService keyValueService,
    IRuntimeState runtimeState,
    ISeoStore store) : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    public async Task HandleAsync(UmbracoApplicationStartingNotification notification, CancellationToken cancellationToken)
    {
        if (runtimeState.Level != RuntimeLevel.Run) return;

        await new Upgrader(new SeoMigrationPlan()).ExecuteAsync(executor, scopeProvider, keyValueService);
        await store.MarkRunningAsIncompleteAsync();
    }
}
