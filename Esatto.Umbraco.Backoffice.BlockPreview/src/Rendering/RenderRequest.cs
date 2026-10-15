using System.Text.Json;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Rendering;

/// <summary>What the backoffice sends to render one block. Values are the block's raw, unsaved data.</summary>
public sealed class RenderRequest
{
    public Guid DocumentKey { get; set; }
    public Guid DocumentTypeKey { get; set; }
    public string PropertyAlias { get; set; } = string.Empty;
    public string? Culture { get; set; }
    public string? Segment { get; set; }
    /// <summary>Per-render id the iframe echoes in its height messages.</summary>
    public string Nonce { get; set; } = string.Empty;
    public BlockDataDto Content { get; set; } = new();
    public BlockDataDto? Settings { get; set; }
}

public sealed class BlockDataDto
{
    public Guid Key { get; set; }
    public Guid ContentTypeKey { get; set; }
    public List<BlockValueDto> Values { get; set; } = [];
}

public sealed class BlockValueDto
{
    public string Alias { get; set; } = string.Empty;
    public string? Culture { get; set; }
    public string? Segment { get; set; }
    public JsonElement? Value { get; set; }
    public string? EditorAlias { get; set; }
}
