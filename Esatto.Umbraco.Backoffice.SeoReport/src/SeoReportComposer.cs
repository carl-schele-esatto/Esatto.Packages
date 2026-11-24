using Esatto.Umbraco.Backoffice.SeoReport.Jobs;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
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

namespace Esatto.Umbraco.Backoffice.SeoReport;

/// <summary>
/// Wires the package into Umbraco: the services, the refresh job that backs up Fetch now, and the migration plan that creates the
/// tables at startup. Composers are auto-discovered from any referenced assembly, so the dashboard, the API and the job need no
/// consumer-side wiring; the dashboard itself is in umbraco-package.json.
/// </summary>
public sealed class SeoReportComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddSeoReport();
        builder.Services.AddRecurringBackgroundJob<SeoReportRefreshJob>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, RunSeoReportMigration>();
    }
}

/// <summary>Creates the package's tables at startup, when they don't exist yet.</summary>
public sealed class RunSeoReportMigration(
    ICoreScopeProvider scopeProvider,
    IMigrationPlanExecutor executor,
    IKeyValueService keyValueService,
    IRuntimeState runtimeState) : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    public async Task HandleAsync(UmbracoApplicationStartingNotification notification, CancellationToken cancellationToken)
    {
        if (runtimeState.Level != RuntimeLevel.Run) return;

        await new Upgrader(new SeoReportMigrationPlan()).ExecuteAsync(executor, scopeProvider, keyValueService);
    }
}
