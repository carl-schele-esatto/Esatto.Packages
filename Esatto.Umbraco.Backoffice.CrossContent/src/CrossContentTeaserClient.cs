using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Esatto.Umbraco.Backoffice.CrossContent;

public sealed class CrossContentTeaserClient(
    ICrossContentTeaserFetcher fetcher,
    IMemoryCache cache,
    TimeProvider clock,
    IOptions<CrossContentOptions> options) : ICrossContentTeaserClient
{
    private readonly CrossContentOptions _o = options.Value;
    private readonly ConcurrentDictionary<string, Lazy<Task<TeaserFetchResult>>> _inflight = new();

    private sealed record CachedTeaser(CrossContentTeaser Teaser, DateTimeOffset FetchedAt);

    public async Task<CrossContentTeaser?> GetTeaserAsync(Guid key, string? culture, CancellationToken ct)
    {
        culture ??= "";
        var cacheKey = $"crosscontent-teaser:{key}:{culture}";
        var goneKey = $"crosscontent-teaser-gone:{key}:{culture}";

        if (cache.TryGetValue(goneKey, out _)) return null;            // negative cache

        if (cache.TryGetValue(cacheKey, out CachedTeaser? entry) && entry is not null)
        {
            var age = clock.GetUtcNow() - entry.FetchedAt;
            if (age >= TimeSpan.FromMinutes(_o.FreshWindowMinutes))
                _ = RefreshInBackground(key, culture, cacheKey, goneKey);   // aging → SWR, don't await
            return entry.Teaser;                                            // fresh OR aging → serve now
        }

        var result = await Coalesced(key, culture, cacheKey);          // cold miss — coalesced fetch
        return result.Outcome switch
        {
            TeaserFetchOutcome.Ok => Store(cacheKey, result.Teaser!).Teaser,
            TeaserFetchOutcome.Gone => NegativeCache(goneKey),
            _ => null,
        };
    }

    private async Task RefreshInBackground(Guid key, string culture, string cacheKey, string goneKey)
    {
        try
        {
            var r = await Coalesced(key, culture, cacheKey);
            if (r.Outcome == TeaserFetchOutcome.Ok) Store(cacheKey, r.Teaser!);
            else if (r.Outcome == TeaserFetchOutcome.Gone) { cache.Remove(cacheKey); NegativeCache(goneKey); }
            // Failed → leave the stale entry in place (serve-stale)
        }
        catch { /* background: never surface */ }
    }

    private Task<TeaserFetchResult> Coalesced(Guid key, string culture, string cacheKey)
    {
        var lazy = _inflight.GetOrAdd(cacheKey, unusedKey => new Lazy<Task<TeaserFetchResult>>(async () =>
        {
            try { return await fetcher.FetchAsync(key, culture, CancellationToken.None); }
            finally { _inflight.TryRemove(cacheKey, out _); }
        }));
        return lazy.Value;
    }

    private CachedTeaser Store(string cacheKey, CrossContentTeaser teaser)
    {
        var entry = new CachedTeaser(teaser, clock.GetUtcNow());
        cache.Set(cacheKey, entry, TimeSpan.FromHours(_o.LastKnownGoodHours));
        return entry;
    }

    private CrossContentTeaser? NegativeCache(string goneKey)
    {
        cache.Set(goneKey, true, TimeSpan.FromSeconds(_o.NegativeCacheSeconds));
        return null;
    }
}
