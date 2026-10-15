using System.Text.Json;
using System.Text.Json.Nodes;
using Esatto.Umbraco.Backoffice.BlockPreview.Rendering;
using Xunit;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Tests;

public class BlockListValueJsonTests
{
    private static readonly Guid ContentKey = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SettingsKey = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static RenderRequest Request(string? culture = "sv", bool withSettings = false) => new()
    {
        DocumentKey = Guid.NewGuid(),
        DocumentTypeKey = Guid.NewGuid(),
        PropertyAlias = "carouselList",
        Culture = culture,
        Nonce = "n1",
        Content = new BlockDataDto
        {
            Key = ContentKey,
            ContentTypeKey = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Values =
            [
                new BlockValueDto
                {
                    Alias = "images",
                    EditorAlias = "Umbraco.MediaPicker3",
                    Value = JsonDocument.Parse("""[{"key":"k1","mediaKey":"m1"}]""").RootElement,
                },
            ],
        },
        Settings = withSettings
            ? new BlockDataDto { Key = SettingsKey, ContentTypeKey = Guid.NewGuid(), Values = [] }
            : null,
    };

    private static JsonNode Parse(RenderRequest request) => JsonNode.Parse(BlockListValueJson.Build(request))!;

    [Fact]
    public void Layout_references_the_content_block_under_the_block_list_alias()
    {
        var layout = Parse(Request())["layout"]!["Umbraco.BlockList"]!.AsArray();
        Assert.Equal(ContentKey.ToString(), Assert.Single(layout)!["contentKey"]!.GetValue<string>());
    }

    [Fact]
    public void Content_data_keeps_the_raw_values()
    {
        var content = Assert.Single(Parse(Request())["contentData"]!.AsArray())!;
        var value = Assert.Single(content["values"]!.AsArray())!;
        Assert.Equal("images", value["alias"]!.GetValue<string>());
        Assert.Equal("Umbraco.MediaPicker3", value["editorAlias"]!.GetValue<string>());
        Assert.Equal("m1", value["value"]![0]!["mediaKey"]!.GetValue<string>());
    }

    // Review Focus 2.
    [Fact]
    public void Without_settings_there_is_no_settings_key_and_no_settings_data()
    {
        var json = Parse(Request(withSettings: false));
        Assert.Null(json["layout"]!["Umbraco.BlockList"]![0]!["settingsKey"]);
        Assert.Empty(json["settingsData"]!.AsArray());
    }

    [Fact]
    public void With_settings_the_layout_links_them()
    {
        var json = Parse(Request(withSettings: true));
        Assert.Equal(SettingsKey.ToString(), json["layout"]!["Umbraco.BlockList"]![0]!["settingsKey"]!.GetValue<string>());
        Assert.Single(json["settingsData"]!.AsArray());
    }

    // Review Focus 1: the converter only keeps exposed blocks.
    [Fact]
    public void Expose_contains_culture_and_invariant_entries()
    {
        var expose = Parse(Request(culture: "sv"))["expose"]!.AsArray();
        Assert.Contains(expose, e => e!["culture"]?.GetValue<string>() == "sv");
        Assert.Contains(expose, e => e!["culture"] is null);
        Assert.All(expose, e => Assert.Equal(ContentKey.ToString(), e!["contentKey"]!.GetValue<string>()));
    }

    [Fact]
    public void Invariant_document_gets_a_single_invariant_expose_entry()
    {
        Assert.Single(Parse(Request(culture: null))["expose"]!.AsArray());
    }
}
