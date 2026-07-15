using NSubstitute;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Xunit;

namespace Esatto.Umbraco.Backoffice.DictionaryLocalization.Tests;

public class DictionaryCacheInvalidationHandlerTests
{
    private sealed class CountingCache : IDictionaryLocalizationCache
    {
        public int InvalidateCount { get; private set; }

        public Task<IReadOnlyList<DictionaryLocalizationEntry>> GetAllAsync()
            => Task.FromResult<IReadOnlyList<DictionaryLocalizationEntry>>(Array.Empty<DictionaryLocalizationEntry>());

        public void Invalidate() => InvalidateCount++;
    }

    private static DictionaryItemSavedNotification Saved()
        => new(Substitute.For<IDictionaryItem>(), new EventMessages());

    private static DictionaryItemDeletedNotification Deleted()
        => new(Substitute.For<IDictionaryItem>(), new EventMessages());

    [Fact]
    public void Saved_notification_invalidates_the_cache()
    {
        var cache = new CountingCache();
        var handler = new DictionaryCacheInvalidationHandler(cache);

        handler.Handle(Saved());

        Assert.Equal(1, cache.InvalidateCount);
    }

    [Fact]
    public void Deleted_notification_invalidates_the_cache()
    {
        var cache = new CountingCache();
        var handler = new DictionaryCacheInvalidationHandler(cache);

        handler.Handle(Deleted());

        Assert.Equal(1, cache.InvalidateCount);
    }

    [Fact]
    public void Repeated_notifications_are_safe()
    {
        var cache = new CountingCache();
        var handler = new DictionaryCacheInvalidationHandler(cache);

        handler.Handle(Saved());
        handler.Handle(Saved());
        handler.Handle(Deleted());

        Assert.Equal(3, cache.InvalidateCount);
    }
}
