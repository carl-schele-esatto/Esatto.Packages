using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.PublishedCache;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Rendering;

/// <summary>
/// Runs the block through the document property's own Block List value converter, the same
/// pipeline the frontend uses. The result is the frontend's <c>BlockListItem&lt;T&gt;</c>, with
/// typed models from IPublishedModelFactory (works with InMemoryAuto).
/// </summary>
public sealed class BlockItemFactory : IBlockItemFactory
{
    private readonly IPublishedContentCache _contentCache;
    private readonly IPublishedContentTypeCache _contentTypeCache;
    private readonly EditorValueConverter _values;

    public BlockItemFactory(IPublishedContentCache contentCache, IPublishedContentTypeCache contentTypeCache, EditorValueConverter values)
    {
        _contentCache = contentCache;
        _contentTypeCache = contentTypeCache;
        _values = values;
    }

    public string GetContentTypeAlias(Guid elementTypeKey) =>
        _contentTypeCache.Get(PublishedItemType.Element, elementTypeKey).Alias;

    public BlockItemResult Create(RenderRequest request)
    {
        IPublishedContent? owner = _contentCache.GetById(preview: true, request.DocumentKey);
        if (owner is null)
        {
            return new BlockItemResult(null, NotPreviewableReason: "unsavedDocument");
        }

        // From the owner itself, so the property type always matches the document being previewed.
        // Missing means the Block List lives somewhere else, e.g. nested inside a block.
        IPublishedPropertyType? propertyType = owner.ContentType.GetPropertyType(request.PropertyAlias);
        if (propertyType is null)
        {
            return new BlockItemResult(null, NotPreviewableReason: "unsupported");
        }

        // Editor values → stored values, as saving would; the converter reads the stored format.
        _values.ToStoredFormat(request.Content);
        if (request.Settings is not null)
        {
            _values.ToStoredFormat(request.Settings);
        }

        string source = BlockListValueJson.Build(request);
        object? inter = propertyType.ConvertSourceToInter(owner, source, preview: true);
        object? converted = propertyType.ConvertInterToObject(owner, PropertyCacheLevel.None, inter, preview: true);
        BlockListItem item = ExtractItem(converted)
            ?? throw new InvalidOperationException("The block could not be converted (is it allowed in this Block List?).");

        return new BlockItemResult(item, NotPreviewableReason: null);
    }

    /// <summary>
    /// The converter returns a <see cref="BlockListModel"/>, or in (deprecated) single block mode the
    /// <see cref="BlockListItem"/> itself.
    /// </summary>
    public static BlockListItem? ExtractItem(object? converted) =>
        converted as BlockListItem ?? (converted as BlockListModel)?.FirstOrDefault();
}
