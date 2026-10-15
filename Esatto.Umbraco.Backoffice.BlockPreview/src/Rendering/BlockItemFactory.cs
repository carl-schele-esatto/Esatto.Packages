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

    public BlockItemFactory(IPublishedContentCache contentCache, IPublishedContentTypeCache contentTypeCache)
    {
        _contentCache = contentCache;
        _contentTypeCache = contentTypeCache;
    }

    public string GetContentTypeAlias(Guid elementTypeKey) =>
        _contentTypeCache.Get(PublishedItemType.Element, elementTypeKey).Alias;

    public BlockItemResult Create(RenderRequest request)
    {
        string alias = GetContentTypeAlias(request.Content.ContentTypeKey);

        IPublishedContent? owner = _contentCache.GetById(preview: true, request.DocumentKey);
        if (owner is null)
        {
            return new BlockItemResult(null, alias, OwnerMissing: true);
        }

        IPublishedPropertyType propertyType =
            _contentTypeCache.Get(PublishedItemType.Content, request.DocumentTypeKey).GetPropertyType(request.PropertyAlias)
            ?? throw new InvalidOperationException($"The document type has no property '{request.PropertyAlias}'.");

        string source = BlockListValueJson.Build(request);
        object? inter = propertyType.ConvertSourceToInter(owner, source, preview: true);
        var model = propertyType.ConvertInterToObject(owner, PropertyCacheLevel.None, inter, preview: true) as BlockListModel;
        BlockListItem item = model?.FirstOrDefault()
            ?? throw new InvalidOperationException("The block could not be converted (is it allowed in this Block List?).");

        return new BlockItemResult(item, alias, OwnerMissing: false);
    }
}
