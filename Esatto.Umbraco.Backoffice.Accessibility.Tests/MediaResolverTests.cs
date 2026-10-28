using Esatto.Umbraco.Backoffice.Accessibility.Scanning;
using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Accessibility.Tests;

public class MediaResolverTests
{
    [Fact]
    public void Media_src_with_query_resolves()
    {
        var key = Guid.NewGuid();
        var media = Substitute.For<IMedia>();
        media.Key.Returns(key);
        var service = Substitute.For<IMediaService>();
        service.GetMediaByPath("/media/abc/x.jpg").Returns(media);

        var resolved = new MediaResolver(service).Resolve("""<img class="hero" src="/media/abc/x.jpg?width=600&amp;v=2" alt="">""");

        Assert.Equal(key, resolved);
    }

    [Theory]
    [InlineData("""<img src="data:image/png;base64,AAAA">""")]
    [InlineData("""<img src="https://cdn.example.com/x.jpg">""")]
    [InlineData("""<a href="/media/abc/x.pdf">Report</a>""")]
    [InlineData("")]
    public void Non_media_src_resolves_to_null(string html)
    {
        var service = Substitute.For<IMediaService>();

        Assert.Null(new MediaResolver(service).Resolve(html));
        service.DidNotReceiveWithAnyArgs().GetMediaByPath(default!);
    }

    [Fact]
    public void Same_path_is_looked_up_once()
    {
        var service = Substitute.For<IMediaService>();
        var resolver = new MediaResolver(service);

        resolver.Resolve("""<img src="/media/abc/x.jpg">""");
        resolver.Resolve("""<img src="/media/abc/x.jpg?width=200">""");

        service.Received(1).GetMediaByPath("/media/abc/x.jpg");
    }
}
