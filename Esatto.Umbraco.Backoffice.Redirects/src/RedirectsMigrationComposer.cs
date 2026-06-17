using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations.Upgrade;

namespace Esatto.Umbraco.Backoffice.Redirects;

/// <summary>
/// Wires Esatto.Umbraco.Backoffice.Redirects into Umbraco's pipeline:
/// <list type="bullet">
///   <item>Appends <see cref="RedirectContentFinder"/> to the IContentFinder chain.</item>
///   <item>Registers the migration plan to run at app startup.</item>
/// </list>
/// Composers are auto-discovered by Umbraco from any referenced assembly
/// that has <see cref="IComposer"/> implementations, so the package works
/// with NO consumer-side wiring required.
/// </summary>
public sealed class RedirectsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.ContentFinders().Append<RedirectContentFinder>();

        // Register the service here (not in an opt-in extension method) so
        // RedirectContentFinder's dependencies always resolve automatically.
        // TryAdd keeps it idempotent — AddBackofficeRedirects() is also safe to call.
        builder.Services.TryAddSingleton<IRedirectService, RedirectService>();

        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, RunRedirectsMigration>();
    }
}

public sealed class RunRedirectsMigration : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly IMigrationPlanExecutor _executor;
    private readonly IKeyValueService _keyValueService;
    private readonly IRuntimeState _runtimeState;

    public RunRedirectsMigration(
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

        var plan = new RedirectsMigrationPlan();
        var upgrader = new Upgrader(plan);
        await upgrader.ExecuteAsync(_executor, _scopeProvider, _keyValueService);
    }
}
