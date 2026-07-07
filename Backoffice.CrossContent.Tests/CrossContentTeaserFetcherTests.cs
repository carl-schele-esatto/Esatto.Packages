using System.Net;
using Backoffice.CrossContent.Tests.TestSupport;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Backoffice.CrossContent.Tests;

public class CrossContentTeaserFetcherTests
{
    private static (CrossContentTeaserFetcher fetcher, StubHttpMessageHandler handler) Create(
        HttpResponseMessage response)
    {
        var handler = new StubHttpMessageHandler(_ => response);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://target.example/") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("crosscontent").Returns(http);
        return (new CrossContentTeaserFetcher(factory, Options.Create(new CrossContentOptions())), handler);
    }

    [Fact]
    public async Task FetchAsync_ReturnsOk_WithDeserializedTeaser()
    {
        const string body = """
            { "key": "11111111-1111-1111-1111-111111111111", "type": "casePage",
              "title": "T", "teaserText": "x", "teaserImage": "https://t/i.jpg",
              "ctaLabel": "Read", "url": "https://t/case" }
            """;
        var (fetcher, handler) = Create(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") });

        var result = await fetcher.FetchAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"), "sv", CancellationToken.None);

        Assert.Equal(TeaserFetchOutcome.Ok, result.Outcome);
        Assert.Equal("https://t/i.jpg", result.Teaser!.TeaserImageUrl);
        Assert.Equal("api/crosscontent/teaser/11111111-1111-1111-1111-111111111111?culture=sv",
            handler.LastRequest!.RequestUri!.PathAndQuery.TrimStart('/'));
    }

    [Fact]
    public async Task FetchAsync_ReturnsGone_On404()
    {
        var (fetcher, _) = Create(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await fetcher.FetchAsync(Guid.NewGuid(), null, CancellationToken.None);

        Assert.Equal(TeaserFetchOutcome.Gone, result.Outcome);
    }

    [Fact]
    public async Task FetchAsync_ReturnsFailed_OnServerError()
    {
        var (fetcher, _) = Create(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await fetcher.FetchAsync(Guid.NewGuid(), null, CancellationToken.None);

        Assert.Equal(TeaserFetchOutcome.Failed, result.Outcome);
    }
}
