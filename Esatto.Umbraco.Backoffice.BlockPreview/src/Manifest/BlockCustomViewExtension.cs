using System.Text.Json.Serialization;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Manifest;

/// <summary>A <c>blockEditorCustomView</c> extension as it appears in a package manifest.</summary>
public sealed record BlockCustomViewExtension(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("alias")] string Alias,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("element")] string Element,
    [property: JsonPropertyName("elementName")] string ElementName,
    [property: JsonPropertyName("forBlockEditor")] string ForBlockEditor,
    [property: JsonPropertyName("weight")] int Weight);
