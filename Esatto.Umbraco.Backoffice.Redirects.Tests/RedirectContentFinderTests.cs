using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Umbraco.Cms.Core.Routing;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Redirects.Tests;

public class RedirectContentFinderTests
{
    private static IPublishedRequestBuilder FakeRequest(string path, string uri)
    {
        var request = Substitute.For<IPublishedRequestBuilder>();
        request.AbsolutePathDecoded.Returns(path);
        request.Uri.Returns(new Uri(uri));
        return request;
    }

    private static RedirectContentFinder Finder(IRedirectService service)
        => new RedirectContentFinder(service, NullLogger<RedirectContentFinder>.Instance);

    [Fact]
    public async Task NoMatch_ReturnsFalse_AndDoesNotRedirect()
    {
        var service = Substitute.For<IRedirectService>();
        service.LookupAsync(Arg.Any<string>()).Returns((string?)null);
        var request = FakeRequest("/missing", "https://example.com/missing");

        var handled = await Finder(service).TryFindContent(request);

        Assert.False(handled);
        request.DidNotReceive().SetRedirect(Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Match_NoQuery_Sets301ToDestination()
    {
        var service = Substitute.For<IRedirectService>();
        service.LookupAsync("/old").Returns("/new");
        var request = FakeRequest("/old", "https://example.com/old");

        var handled = await Finder(service).TryFindContent(request);

        Assert.True(handled);
        request.Received(1).SetRedirect("/new", 301);
    }

    [Fact]
    public async Task Match_WithIncomingQuery_MergesQuery()
    {
        var service = Substitute.For<IRedirectService>();
        service.LookupAsync("/old").Returns("/new");
        var request = FakeRequest("/old", "https://example.com/old?a=1&b=2");

        await Finder(service).TryFindContent(request);

        request.Received(1).SetRedirect("/new?a=1&b=2", 301);
    }

    [Fact]
    public async Task Match_DestinationHasQuery_UsesAmpersand()
    {
        var service = Substitute.For<IRedirectService>();
        service.LookupAsync("/old").Returns("/new?x=1");
        var request = FakeRequest("/old", "https://example.com/old?a=1");

        await Finder(service).TryFindContent(request);

        request.Received(1).SetRedirect("/new?x=1&a=1", 301);
    }

    [Fact]
    public async Task Match_DestinationHasFragment_MergesQueryBeforeFragment()
    {
        var service = Substitute.For<IRedirectService>();
        service.LookupAsync("/old").Returns("/new#section");
        var request = FakeRequest("/old", "https://example.com/old?a=1");

        await Finder(service).TryFindContent(request);

        request.Received(1).SetRedirect("/new?a=1#section", 301);
    }

    [Fact]
    public async Task EmptyPath_ReturnsFalse_WithoutLookup()
    {
        var service = Substitute.For<IRedirectService>();
        var request = FakeRequest("", "https://example.com/");

        var handled = await Finder(service).TryFindContent(request);

        Assert.False(handled);
        await service.DidNotReceive().LookupAsync(Arg.Any<string>());
    }
}
