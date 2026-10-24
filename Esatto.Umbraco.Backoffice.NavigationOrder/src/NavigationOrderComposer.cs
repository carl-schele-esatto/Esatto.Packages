using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Extensions;

namespace Esatto.Umbraco.Backoffice.NavigationOrder;

/// <summary>Registers the store and the table migration. The client is in umbraco-package.json.</summary>
public sealed class NavigationOrderComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddNavigationOrder();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, RunNavigationOrderMigration>();
    }
}
