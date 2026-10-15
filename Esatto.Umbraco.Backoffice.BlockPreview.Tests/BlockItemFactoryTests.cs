using Esatto.Umbraco.Backoffice.BlockPreview.Rendering;
using NSubstitute;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Xunit;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Tests;

public class BlockItemFactoryTests
{
    private static BlockListItem Item() => new(Guid.NewGuid(), Substitute.For<IPublishedElement>(), null, null);

    [Fact]
    public void Takes_the_first_item_of_a_block_list_model()
    {
        var item = Item();
        Assert.Same(item, BlockItemFactory.ExtractItem(new BlockListModel([item])));
    }

    // Final review, Important 2: in (deprecated but still offered) single block mode the converter
    // returns the BlockListItem itself, not a BlockListModel.
    [Fact]
    public void Accepts_a_single_block_mode_item()
    {
        var item = Item();
        Assert.Same(item, BlockItemFactory.ExtractItem(item));
    }

    [Fact]
    public void Returns_null_for_an_empty_model_or_nothing()
    {
        Assert.Null(BlockItemFactory.ExtractItem(new BlockListModel([])));
        Assert.Null(BlockItemFactory.ExtractItem(null));
    }

    // Deferred minor (1.0.1): the renderer already resolved the block type; Create looked it up again.
    [Fact]
    public void Create_does_not_look_up_the_block_type_again()
    {
        var contentTypes = Substitute.For<IPublishedContentTypeCache>();
        var contents = Substitute.For<IPublishedContentCache>();
        contents.GetById(true, Arg.Any<Guid>()).Returns((IPublishedContent?)null);
        var factory = new BlockItemFactory(contents, contentTypes);

        BlockItemResult result = factory.Create(new RenderRequest { DocumentKey = Guid.NewGuid() });

        Assert.Equal("unsavedDocument", result.NotPreviewableReason);
        contentTypes.DidNotReceiveWithAnyArgs().Get(default, default(Guid));
    }
}
