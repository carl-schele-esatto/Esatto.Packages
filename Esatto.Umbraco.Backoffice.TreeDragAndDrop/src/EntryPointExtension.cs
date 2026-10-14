using System.Text.Json.Serialization;

namespace Esatto.Umbraco.Backoffice.TreeDragAndDrop;

/// <summary>A <c>backofficeEntryPoint</c> extension as it appears in a package manifest.</summary>
public sealed record EntryPointExtension(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("alias")] string Alias,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("js")] string Js,
    [property: JsonPropertyName("conditions")] IReadOnlyList<ExtensionCondition> Conditions);

/// <summary>An extension condition, e.g. <c>Umb.Condition.SectionAlias</c>.</summary>
public sealed record ExtensionCondition(
    [property: JsonPropertyName("alias")] string Alias,
    [property: JsonPropertyName("match")] string Match);
