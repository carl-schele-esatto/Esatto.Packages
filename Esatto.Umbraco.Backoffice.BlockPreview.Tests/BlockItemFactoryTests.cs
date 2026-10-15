using System.Text.Json;
using Esatto.Umbraco.Backoffice.BlockPreview.Rendering;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.Editors;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Infrastructure.Serialization;
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
        var factory = new BlockItemFactory(contents, contentTypes, Values(contentTypes, []));

        BlockItemResult result = factory.Create(new RenderRequest { DocumentKey = Guid.NewGuid() });

        Assert.Equal("unsavedDocument", result.NotPreviewableReason);
        contentTypes.DidNotReceiveWithAnyArgs().Get(default, default(Guid));
    }

    private static EditorValueConverter Values(IPublishedContentTypeCache contentTypes, List<IDataEditor> editors) => new(
        contentTypes, new PropertyEditorCollection(new DataEditorCollection(() => editors)),
        new SystemTextJsonSerializer(new DefaultJsonSerializerEncoderFactory()), NullLogger<EditorValueConverter>.Instance);

    // Live finding (1.0.2): a dropdown in a block failed the preview — the block was converted from
    // editor values. Content AND settings must reach the converter in their stored format.
    [Fact]
    public void Converts_content_and_settings_from_their_stored_format()
    {
        var elementKey = Guid.NewGuid();
        var contentTypes = Substitute.For<IPublishedContentTypeCache>();
        var elementType = Substitute.For<IPublishedContentType>();
        contentTypes.Get(PublishedItemType.Element, elementKey).Returns(elementType);
        var dropdownProperty = Substitute.For<IPublishedPropertyType>();
        dropdownProperty.EditorAlias.Returns("Umbraco.DropDown.Flexible");
        dropdownProperty.DataType.Returns(new PublishedDataType(1, "Umbraco.DropDown.Flexible", null, new Lazy<object?>(() => null)));
        elementType.GetPropertyType(Arg.Any<string>()).Returns(dropdownProperty);
        var valueEditor = Substitute.For<IDataValueEditor>();
        valueEditor.FromEditor(Arg.Any<ContentPropertyData>(), Arg.Any<object?>()).Returns("STORED");
        var dropdown = Substitute.For<IDataEditor>();
        dropdown.Alias.Returns("Umbraco.DropDown.Flexible");
        dropdown.GetValueEditor(Arg.Any<object?>()).Returns(valueEditor);

        var owner = Substitute.For<IPublishedContent>();
        var contents = Substitute.For<IPublishedContentCache>();
        contents.GetById(true, Arg.Any<Guid>()).Returns(owner);
        var blockListProperty = Substitute.For<IPublishedPropertyType>();
        owner.ContentType.GetPropertyType("contentArea").Returns(blockListProperty);
        string? source = null;
        blockListProperty.ConvertSourceToInter(owner, Arg.Do<object?>(s => source = s as string), true).Returns("inter");
        blockListProperty.ConvertInterToObject(owner, PropertyCacheLevel.None, "inter", true).Returns(new BlockListModel([Item()]));

        BlockDataDto Data(string alias) => new()
        {
            Key = Guid.NewGuid(),
            ContentTypeKey = elementKey,
            Values = [new BlockValueDto { Alias = alias, Value = JsonDocument.Parse("[\"SQL\"]").RootElement }],
        };
        var request = new RenderRequest { DocumentKey = Guid.NewGuid(), PropertyAlias = "contentArea", Content = Data("language"), Settings = Data("theme") };

        new BlockItemFactory(contents, contentTypes, Values(contentTypes, [dropdown])).Create(request);

        var json = JsonDocument.Parse(source!).RootElement;
        Assert.Equal("STORED", json.GetProperty("contentData")[0].GetProperty("values")[0].GetProperty("value").GetString());
        Assert.Equal("STORED", json.GetProperty("settingsData")[0].GetProperty("values")[0].GetProperty("value").GetString());
    }
}
