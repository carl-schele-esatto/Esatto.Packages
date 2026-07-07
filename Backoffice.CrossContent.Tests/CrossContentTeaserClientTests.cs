using Backoffice.CrossContent.Tests.TestSupport;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Backoffice.CrossContent.Tests;

public class CrossContentTeaserClientTests
{
    private static readonly Guid Key = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static CrossContentTeaser Teaser(string title = "T") =>
        new(Key, "casePage", title, "x", "https://t/i.jpg", "Read", "https://t/case");

    private static (CrossContentTeaserClient client, ICrossContentTeaserFetcher fetcher, TestTimeProvider clock, IMemoryCache cache) Create()
    {
        var fetcher = Substitute.For<ICrossContentTeaserFetcher>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var clock = new TestTimeProvider(T0);
        var options = Options.Create(new CrossContentOptions
        {
            FreshWindowMinutes = 5, LastKnownGoodHours = 48, NegativeCacheSeconds = 60,
        });
        return (new CrossContentTeaserClient(fetcher, cache, clock, options), fetcher, clock, cache);
    }

    [Fact]
    public async Task ColdMiss_Ok_FetchesAndReturnsTeaser()
    {
        var (client, fetcher, _, _) = Create();
        fetcher.FetchAsync(Key, "", Arg.Any<CancellationToken>()).Returns(TeaserFetchResult.Ok(Teaser()));

        var result = await client.GetTeaserAsync(Key, null, CancellationToken.None);

        Assert.NotNull(result);
        await fetcher.Received(1).FetchAsync(Key, "", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ColdMiss_Gone_ReturnsNull_AndNegativeCaches()
    {
        var (client, fetcher, _, _) = Create();
        fetcher.FetchAsync(Key, "", Arg.Any<CancellationToken>()).Returns(TeaserFetchResult.Gone);

        var first = await client.GetTeaserAsync(Key, null, CancellationToken.None);
        var second = await client.GetTeaserAsync(Key, null, CancellationToken.None);

        Assert.Null(first);
        Assert.Null(second);
        await fetcher.Received(1).FetchAsync(Key, "", Arg.Any<CancellationToken>()); // negative cache short-circuits
    }

    [Fact]
    public async Task ColdMiss_Failed_ReturnsNull_NoCaching()
    {
        var (client, fetcher, _, _) = Create();
        fetcher.FetchAsync(Key, "", Arg.Any<CancellationToken>()).Returns(TeaserFetchResult.Failed);

        var result = await client.GetTeaserAsync(Key, null, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Fresh_ServesFromCache_WithoutRefetch()
    {
        var (client, fetcher, clock, _) = Create();
        fetcher.FetchAsync(Key, "", Arg.Any<CancellationToken>()).Returns(TeaserFetchResult.Ok(Teaser("first")));
        await client.GetTeaserAsync(Key, null, CancellationToken.None); // warm

        clock.Advance(TimeSpan.FromMinutes(2)); // within 5-min fresh window
        var result = await client.GetTeaserAsync(Key, null, CancellationToken.None);

        Assert.Equal("first", result!.Title);
        await fetcher.Received(1).FetchAsync(Key, "", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Aging_ServesStaleImmediately_AndRefreshesInBackground()
    {
        var (client, fetcher, clock, _) = Create();
        fetcher.FetchAsync(Key, "", Arg.Any<CancellationToken>()).Returns(TeaserFetchResult.Ok(Teaser("v1")));
        await client.GetTeaserAsync(Key, null, CancellationToken.None); // warm with v1

        clock.Advance(TimeSpan.FromMinutes(10)); // past 5-min fresh window → aging
        fetcher.FetchAsync(Key, "", Arg.Any<CancellationToken>()).Returns(TeaserFetchResult.Ok(Teaser("v2")));
        var served = await client.GetTeaserAsync(Key, null, CancellationToken.None);

        Assert.Equal("v1", served!.Title); // stale served immediately
        // background refresh runs; allow it to complete
        await Task.Delay(50);
        var next = await client.GetTeaserAsync(Key, null, CancellationToken.None);
        Assert.Equal("v2", next!.Title);
    }
}
