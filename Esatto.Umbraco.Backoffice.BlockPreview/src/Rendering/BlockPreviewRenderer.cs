using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Rendering;

/// <summary>
/// Request → block item → partial inside the preview layout → HTML with the frame script.
/// Every failure becomes a <see cref="RenderOutcome"/>; nothing is thrown to the client.
/// </summary>
public sealed class BlockPreviewRenderer
{
    public const string FrameView = "/Views/EsattoBlockPreview/Frame.cshtml";
    public const string DefaultLayoutView = "/Views/EsattoBlockPreview/_DefaultLayout.cshtml";
    private const string PartialPathFormat = "~/Views/Partials/blocklist/Components/{0}.cshtml";

    private readonly IBlockItemFactory _items;
    private readonly IViewRenderer _views;
    private readonly IVariationContextAccessor _variation;
    private readonly IOptions<BlockPreviewOptions> _options;
    private readonly ILogger<BlockPreviewRenderer> _logger;

    public BlockPreviewRenderer(IBlockItemFactory items, IViewRenderer views, IVariationContextAccessor variation,
        IOptions<BlockPreviewOptions> options, ILogger<BlockPreviewRenderer> logger)
    {
        _items = items;
        _views = views;
        _variation = variation;
        _options = options;
        _logger = logger;
    }

    public async Task<RenderOutcome> RenderAsync(RenderRequest request, ActionContext actionContext)
    {
        string alias = "(unknown)";
        try
        {
            BlockPreviewOptions options = _options.Value;
            alias = _items.GetContentTypeAlias(request.Content.ContentTypeKey);

            // Checked before converting: an ignored block must show the plain row even if it can't convert.
            if (options.BlockList.IgnoredContentTypes.Contains(alias, StringComparer.OrdinalIgnoreCase))
            {
                return RenderOutcome.NotPreviewable("ignored");
            }

            BlockItemResult block = _items.Create(request);
            if (block.OwnerMissing)
            {
                return RenderOutcome.NotPreviewable("unsavedDocument");
            }

            string partial = string.Format(CultureInfo.InvariantCulture, PartialPathFormat, alias);
            if (!_views.ViewExists(partial))
            {
                return RenderOutcome.NotPreviewable("noPartial");
            }

            string layout = _views.ViewExists(options.Layout) ? options.Layout : DefaultLayoutView;
            var model = new FrameModel(partial, block.Item!, layout, options.Stylesheets, options.Scripts);

            string html = await RenderInCultureAsync(request, () => _views.RenderAsync(FrameView, model, actionContext));
            return RenderOutcome.Rendered(FrameScript.Inject(html, request.Nonce));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Block preview failed for block type {ContentTypeAlias}.", alias);
            return RenderOutcome.Error(ex.Message);
        }
    }

    // Dictionary lookups, number/date formatting and variant property values must follow the
    // culture being edited, not the backoffice user's. Restored afterwards.
    private async Task<string> RenderInCultureAsync(RenderRequest request, Func<Task<string>> render)
    {
        if (request.Culture is null)
        {
            return await render();
        }

        VariationContext? previousVariation = _variation.VariationContext;
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            _variation.VariationContext = new VariationContext(request.Culture, request.Segment);
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(request.Culture);
            return await render();
        }
        finally
        {
            _variation.VariationContext = previousVariation;
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }
}
