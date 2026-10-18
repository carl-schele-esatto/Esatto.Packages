using System.Text.Json;
using Esatto.Umbraco.Backoffice.BlockPreview.Rendering;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Editors;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Infrastructure.Serialization;
using Xunit;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Tests;

// Live finding (1.0.2): a dropdown in a block failed the preview with "'S' is an invalid start of a
// value". The backoffice sends editor values (["SQL"]); value converters read stored values
// ("[\"SQL\"]"), which saving produces with each editor's FromEditor. The preview must do the same.
public class EditorValueConverterTests
{
    private static readonly Guid ElementTypeKey = Guid.NewGuid();
    private readonly IPublishedContentTypeCache _contentTypes = Substitute.For<IPublishedContentTypeCache>();
    private readonly IPublishedContentType _elementType = Substitute.For<IPublishedContentType>();
    private readonly List<IDataEditor> _editors = [];

    public EditorValueConverterTests()
    {
        _contentTypes.Get(PublishedItemType.Element, ElementTypeKey).Returns(_elementType);
    }

    private EditorValueConverter Converter() => new(
        _contentTypes,
        new PropertyEditorCollection(new DataEditorCollection(() => _editors)),
        new SystemTextJsonSerializer(new DefaultJsonSerializerEncoderFactory()),
        NullLogger<EditorValueConverter>.Instance);

    // A property on the element type, edited with a data editor whose FromEditor is `fromEditor`.
    private IDataValueEditor Property(string alias, string editorAlias)
    {
        var propertyType = Substitute.For<IPublishedPropertyType>();
        propertyType.EditorAlias.Returns(editorAlias);
        propertyType.DataType.Returns(new PublishedDataType(1, editorAlias, null, new Lazy<object?>(() => null)));
        _elementType.GetPropertyType(alias).Returns(propertyType);

        var valueEditor = Substitute.For<IDataValueEditor>();
        var editor = Substitute.For<IDataEditor>();
        editor.Alias.Returns(editorAlias);
        editor.GetValueEditor(Arg.Any<object?>()).Returns(valueEditor);
        _editors.Add(editor);
        return valueEditor;
    }

    private static BlockDataDto Data(string alias, string valueJson) => new()
    {
        ContentTypeKey = ElementTypeKey,
        Values = [new BlockValueDto { Alias = alias, Value = JsonDocument.Parse(valueJson).RootElement }],
    };

    [Fact]
    public void Converts_a_value_to_its_stored_format_like_saving_does()
    {
        IDataValueEditor dropdown = Property("language", "Umbraco.DropDown.Flexible");
        object? received = null;
        dropdown.FromEditor(Arg.Any<ContentPropertyData>(), Arg.Any<object?>()).Returns(call =>
        {
            received = call.Arg<ContentPropertyData>().Value;
            return "[\"SQL\"]";
        });
        BlockDataDto data = Data("language", "[\"SQL\"]");

        Converter().ToStoredFormat(data);

        // Deserialized the way Umbraco deserializes block values before FromEditor (a List<string>).
        Assert.Equal(["SQL"], Assert.IsAssignableFrom<IEnumerable<string>>(received));
        Assert.Equal(JsonValueKind.String, data.Values[0].Value!.Value.ValueKind);
        Assert.Equal("[\"SQL\"]", data.Values[0].Value!.Value.GetString());
    }

    [Theory]
    [InlineData("Umbraco.UploadField")]
    [InlineData("Umbraco.ImageCropper")]
    [InlineData("Umbraco.RichText")]
    [InlineData("Umbraco.BlockList")]
    [InlineData("Umbraco.BlockGrid")]
    public void Leaves_editors_whose_save_step_consumes_uploads_untouched(string editorAlias)
    {
        IDataValueEditor valueEditor = Property("field", editorAlias);
        BlockDataDto data = Data("field", "{\"src\":\"/media/a.png\",\"temporaryFileId\":\"t1\"}");

        Converter().ToStoredFormat(data);

        valueEditor.DidNotReceiveWithAnyArgs().FromEditor(default!, default);
        Assert.Equal(JsonValueKind.Object, data.Values[0].Value!.Value.ValueKind);
    }

    [Fact]
    public void Leaves_a_property_the_element_type_does_not_have_untouched()
    {
        BlockDataDto data = Data("gone", "[\"x\"]");
        Converter().ToStoredFormat(data);
        Assert.Equal(JsonValueKind.Array, data.Values[0].Value!.Value.ValueKind);
    }

    [Fact]
    public void Keeps_the_editor_value_when_the_save_step_fails()
    {
        IDataValueEditor dropdown = Property("language", "Umbraco.DropDown.Flexible");
        dropdown.FromEditor(Arg.Any<ContentPropertyData>(), Arg.Any<object?>()).Throws(new InvalidOperationException("boom"));
        BlockDataDto data = Data("language", "[\"SQL\"]");

        Converter().ToStoredFormat(data);

        Assert.Equal(JsonValueKind.Array, data.Values[0].Value!.Value.ValueKind);
    }

    [Fact]
    public void A_null_stored_value_becomes_json_null()
    {
        IDataValueEditor dropdown = Property("language", "Umbraco.DropDown.Flexible");
        dropdown.FromEditor(Arg.Any<ContentPropertyData>(), Arg.Any<object?>()).Returns((object?)null);
        BlockDataDto data = Data("language", "[]");

        Converter().ToStoredFormat(data);

        Assert.Equal(JsonValueKind.Null, data.Values[0].Value!.Value.ValueKind);
    }
}
