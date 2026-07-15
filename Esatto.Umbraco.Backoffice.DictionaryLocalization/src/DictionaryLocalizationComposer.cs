using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;

namespace Esatto.Umbraco.Backoffice.DictionaryLocalization;

/// <summary>
/// Auto-discovered by Umbraco. Registers the dictionary cache singleton and wires the
/// save/delete notification handlers that invalidate it. No consumer-side wiring needed.
/// </summary>
public sealed class DictionaryLocalizationComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddSingleton<IDictionaryLocalizationCache, DictionaryLocalizationCache>();
        builder.AddNotificationHandler<DictionaryItemSavedNotification, DictionaryCacheInvalidationHandler>();
        builder.AddNotificationHandler<DictionaryItemDeletedNotification, DictionaryCacheInvalidationHandler>();
    }
}
