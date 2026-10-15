using System.Text.Json.Nodes;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Rendering;

/// <summary>
/// Builds the stored JSON of a one-item Block List value from a render request, so it can be
/// converted by the document property's own value converter (the same pipeline as the frontend).
/// </summary>
public static class BlockListValueJson
{
    private const string BlockListLayoutAlias = "Umbraco.BlockList";

    public static string Build(RenderRequest request)
    {
        var layoutItem = new JsonObject { ["contentKey"] = request.Content.Key.ToString() };
        if (request.Settings is not null)
        {
            layoutItem["settingsKey"] = request.Settings.Key.ToString();
        }

        var value = new JsonObject
        {
            ["layout"] = new JsonObject { [BlockListLayoutAlias] = new JsonArray(layoutItem) },
            ["contentData"] = new JsonArray(Data(request.Content)),
            ["settingsData"] = request.Settings is null ? new JsonArray() : new JsonArray(Data(request.Settings)),
            ["expose"] = Expose(request),
        };
        return value.ToJsonString();
    }

    private static JsonObject Data(BlockDataDto data)
    {
        var values = new JsonArray();
        foreach (BlockValueDto v in data.Values)
        {
            values.Add(new JsonObject
            {
                ["alias"] = v.Alias,
                ["culture"] = v.Culture,
                ["segment"] = v.Segment,
                ["value"] = v.Value is { } element ? JsonNode.Parse(element.GetRawText()) : null,
                ["editorAlias"] = v.EditorAlias,
            });
        }

        return new JsonObject
        {
            ["contentTypeKey"] = data.ContentTypeKey.ToString(),
            ["key"] = data.Key.ToString(),
            ["values"] = values,
        };
    }

    // The converter only keeps blocks exposed for the variation being rendered. Expose the block
    // invariantly and, when a culture or segment is being edited, for that variation too, so it
    // converts whether or not the element type itself varies.
    private static JsonArray Expose(RenderRequest request)
    {
        string contentKey = request.Content.Key.ToString();
        var expose = new JsonArray(new JsonObject { ["contentKey"] = contentKey, ["culture"] = null, ["segment"] = null });
        if (request.Culture is not null || request.Segment is not null)
        {
            expose.Add(new JsonObject { ["contentKey"] = contentKey, ["culture"] = request.Culture, ["segment"] = request.Segment });
        }

        return expose;
    }
}
