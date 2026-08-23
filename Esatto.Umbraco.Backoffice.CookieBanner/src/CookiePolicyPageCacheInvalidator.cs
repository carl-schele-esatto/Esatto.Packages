using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Clears <see cref="CookiePolicyPageResolver"/>'s cached policy-page key whenever content is
/// published, unpublished or deleted.
/// </summary>
/// <remarks>
/// Deliberately unconditional - it does not inspect which content type changed. Checking that would
/// mean reading <c>IContent.ContentType.Alias</c> on every content event site-wide purely to decide
/// whether to clear one cache key, which is a needless amount of new surface for what is already a
/// rare event (an editor publishing/unpublishing/deleting something) next to the request traffic
/// <see cref="CookiePolicyPageResolver"/> is optimising for. Clearing a key nobody was about to read
/// again costs nothing; the next request just re-runs the scan once.
/// </remarks>
internal sealed class CookiePolicyPageCacheInvalidator(AppCaches appCaches) :
    INotificationHandler<ContentPublishedNotification>,
    INotificationHandler<ContentUnpublishedNotification>,
    INotificationHandler<ContentDeletedNotification>
{
    public void Handle(ContentPublishedNotification notification) => Invalidate();

    public void Handle(ContentUnpublishedNotification notification) => Invalidate();

    public void Handle(ContentDeletedNotification notification) => Invalidate();

    // Clear(key), not ClearByKey(keyPrefix): the resolver caches under exactly one key, and
    // Clear(...) is the exact-match member - ClearByKey(...) does a startsWith scan over every
    // cached key site-wide, which would be a strange amount of extra work for one entry.
    private void Invalidate() =>
        appCaches.RuntimeCache.Clear(CookiePolicyPageResolver.RuntimeCacheKey);
}
