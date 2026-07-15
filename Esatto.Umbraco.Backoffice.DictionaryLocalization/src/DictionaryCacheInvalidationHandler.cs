using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Esatto.Umbraco.Backoffice.DictionaryLocalization;

/// <summary>
/// Drops the cached dictionary whenever an item is saved or deleted, so the next
/// backoffice fetch reflects edits. Invalidation is just a flag flip, so it is
/// idempotent and safe under batch notifications.
/// </summary>
internal sealed class DictionaryCacheInvalidationHandler :
    INotificationHandler<DictionaryItemSavedNotification>,
    INotificationHandler<DictionaryItemDeletedNotification>
{
    private readonly IDictionaryLocalizationCache _cache;

    public DictionaryCacheInvalidationHandler(IDictionaryLocalizationCache cache)
        => _cache = cache;

    public void Handle(DictionaryItemSavedNotification notification) => _cache.Invalidate();

    public void Handle(DictionaryItemDeletedNotification notification) => _cache.Invalidate();
}
