using Esatto.Umbraco.Backoffice.SharedPreviewLink;
using NSubstitute;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Navigation;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests;

public class DraftContentPathTests
{
    private static readonly Guid Root = Guid.NewGuid();
    private static readonly Guid News = Guid.NewGuid();
    private static readonly Guid Article = Guid.NewGuid();

    private readonly IDocumentNavigationQueryService _navigation = Substitute.For<IDocumentNavigationQueryService>();
    private readonly IDocumentUrlService _urls = Substitute.For<IDocumentUrlService>();

    public DraftContentPathTests()
    {
        // Root (home) → News → Article. Ancestors come immediate-parent first, root last.
        SetAncestors(Root);
        SetAncestors(News, Root);
        SetAncestors(Article, News, Root);

        _urls.GetUrlSegment(Root, Arg.Any<string>(), true).Returns("home");
        _urls.GetUrlSegment(News, Arg.Any<string>(), true).Returns("nyheter-kunskap");
        // A never-published draft only has a DRAFT segment.
        _urls.GetUrlSegment(Article, Arg.Any<string>(), true).Returns("umbracokalaset");
        _urls.GetUrlSegment(Article, Arg.Any<string>(), false).Returns((string?)null);
    }

    private void SetAncestors(Guid key, params Guid[] ancestors)
    {
        _navigation.TryGetAncestorsKeys(key, out Arg.Any<IEnumerable<Guid>>())
            .Returns(call =>
            {
                call[1] = ancestors.AsEnumerable();
                return true;
            });
    }

    [Fact]
    public void Never_published_draft_keeps_its_own_segment()
    {
        // Regression: Umbraco 18's IPublishedContent.UrlSegment() only returns the draft segment
        // inside a preview-mode request, so the article's segment was dropped and the link
        // pointed at its parent (/nyheter-kunskap).
        var path = DraftContentPath.RelativePath(_navigation, _urls, Article, "sv-SE");

        Assert.Equal("/nyheter-kunskap/umbracokalaset", path);
    }

    [Fact]
    public void Root_segment_is_never_part_of_the_path()
    {
        Assert.Equal("/nyheter-kunskap", DraftContentPath.RelativePath(_navigation, _urls, News, "sv-SE"));
        Assert.Equal(string.Empty, DraftContentPath.RelativePath(_navigation, _urls, Root, "sv-SE"));
    }

    [Fact]
    public void Invariant_content_passes_an_empty_culture()
    {
        DraftContentPath.RelativePath(_navigation, _urls, Article, culture: null);

        _urls.Received().GetUrlSegment(Article, string.Empty, true);
    }

    [Fact]
    public void Root_key_is_the_last_ancestor_or_the_document_itself()
    {
        Assert.Equal(Root, DraftContentPath.RootKey(_navigation, Article));
        Assert.Equal(Root, DraftContentPath.RootKey(_navigation, Root));
    }
}
