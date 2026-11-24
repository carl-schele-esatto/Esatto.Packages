using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;

namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

/// <summary>
/// An <see cref="IMemoryCache"/> whose entries expire by the injected <see cref="TimeProvider"/>, so a test moves them with a
/// <c>FakeTimeProvider</c> and no real time passes. Each entry carries its own expiry; the memory cache's own (real-time) expiry is
/// only a backstop that keeps old entries from staying in memory for good, and is set to twice the lifetime so it can never cut
/// an entry short of the lifetime the caller asked for.
/// </summary>
internal sealed class ClockedCache(IMemoryCache cache, TimeProvider time)
{
    public bool TryGet<T>(string key, [NotNullWhen(true)] out T? value)
        where T : class
    {
        if (cache.TryGetValue(key, out Entry<T>? entry) && entry is not null && time.GetUtcNow() < entry.Until)
        {
            value = entry.Value;
            return true;
        }

        value = null;
        return false;
    }

    public void Set<T>(string key, T value, TimeSpan lifetime)
        where T : class
        => cache.Set(key, new Entry<T>(value, time.GetUtcNow() + lifetime), lifetime * 2);

    public void Remove(string key) => cache.Remove(key);

    private sealed record Entry<T>(T Value, DateTimeOffset Until);
}
