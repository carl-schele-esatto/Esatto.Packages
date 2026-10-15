namespace Esatto.Umbraco.Backoffice.BlockPreview.Rendering;

/// <summary>
/// Converts a render request into the exact block item the frontend partial receives
/// (e.g. <c>BlockListItem&lt;CarouselBlock&gt;</c>).
/// </summary>
public interface IBlockItemFactory
{
    /// <summary>The block element type's alias (the partial's file name), without converting anything.</summary>
    string GetContentTypeAlias(Guid elementTypeKey);

    BlockItemResult Create(RenderRequest request);
}

/// <param name="Item">The block list item, or null when it couldn't be produced.</param>
/// <param name="ContentTypeAlias">The block's content type alias (the partial's file name).</param>
/// <param name="NotPreviewableReason">
/// <c>unsavedDocument</c> (the document has never been saved, so it isn't in the content cache) or
/// <c>unsupported</c> (the property isn't on the document's type, e.g. a Block List nested inside a
/// block); null when <paramref name="Item"/> was produced.
/// </param>
public sealed record BlockItemResult(object? Item, string ContentTypeAlias, string? NotPreviewableReason);
