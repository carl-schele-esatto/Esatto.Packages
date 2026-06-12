using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Esatto.Umbraco.Backoffice.DictionaryFilterValues;

/// <summary>
/// Drops the cached dictionary whenever an item is saved or deleted, so the filter
/// reflects edits without a full reload. Invalidation is just a flag flip, so it is
/// idempotent and safe under batch notifications.
/// </summary>
internal sealed class DictionaryCacheInvalidationHandler :
    INotificationHandler<DictionaryItemSavedNotification>,
    INotificationHandler<DictionaryItemDeletedNotification>
{
    private readonly IDictionaryFilterValuesCache _cache;

    public DictionaryCacheInvalidationHandler(IDictionaryFilterValuesCache cache)
        => _cache = cache;

    public void Handle(DictionaryItemSavedNotification notification) => _cache.Invalidate();

    public void Handle(DictionaryItemDeletedNotification notification) => _cache.Invalidate();
}
