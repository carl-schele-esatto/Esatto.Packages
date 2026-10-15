using System.Globalization;
using Esatto.Umbraco.Backoffice.BlockPreview;
using Esatto.Umbraco.Backoffice.BlockPreview.Rendering;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Umbraco.Cms.Core.Models.PublishedContent;
using Xunit;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Tests;

public class BlockPreviewRendererTests
{
    private const string Partial = "~/Views/Partials/blocklist/Components/carouselBlock.cshtml";
    private readonly IBlockItemFactory _items = Substitute.For<IBlockItemFactory>();
    private readonly IViewRenderer _views = Substitute.For<IViewRenderer>();
    private readonly VariationAccessor _variation = new();
    private readonly object _item = new();

    // A real accessor: the test reads what the renderer set while rendering.
    private sealed class VariationAccessor : IVariationContextAccessor
    {
        public VariationContext? VariationContext { get; set; }
    }

    private BlockPreviewRenderer Renderer(BlockPreviewOptions? options = null) =>
        new(_items, _views, _variation, Options.Create(options ?? new BlockPreviewOptions()), NullLogger<BlockPreviewRenderer>.Instance);

    private static RenderRequest Request(string? culture = null) => new() { Nonce = "n1", Culture = culture, Content = new() { ContentTypeKey = Guid.NewGuid() } };

    private void ItemConverts()
    {
        _items.GetContentTypeAlias(Arg.Any<Guid>()).Returns("carouselBlock");
        _items.Create(Arg.Any<RenderRequest>()).Returns(new BlockItemResult(_item, "carouselBlock", NotPreviewableReason: null));
    }

    [Fact]
    public async Task Ignored_content_type_is_not_previewable_and_not_converted()
    {
        ItemConverts();
        var options = new BlockPreviewOptions { BlockList = new() { IgnoredContentTypes = ["carouselBlock"] } };
        var outcome = await Renderer(options).RenderAsync(Request(), new ActionContext());
        Assert.Equal(RenderOutcome.NotPreviewable("ignored"), outcome);
        _items.DidNotReceive().Create(Arg.Any<RenderRequest>());
    }

    [Fact]
    public async Task Missing_partial_is_not_previewable()
    {
        ItemConverts();
        _views.ViewExists(Partial).Returns(false);
        Assert.Equal(RenderOutcome.NotPreviewable("noPartial"), await Renderer().RenderAsync(Request(), new ActionContext()));
    }

    [Fact]
    public async Task Unsaved_document_is_not_previewable()
    {
        _items.Create(Arg.Any<RenderRequest>()).Returns(new BlockItemResult(null, "carouselBlock", NotPreviewableReason: "unsavedDocument"));
        Assert.Equal(RenderOutcome.NotPreviewable("unsavedDocument"), await Renderer().RenderAsync(Request(), new ActionContext()));
    }

    [Fact]
    public async Task Uses_the_site_layout_when_it_exists()
    {
        ItemConverts();
        _views.ViewExists(Arg.Any<string>()).Returns(true);
        _views.RenderAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<ActionContext>()).Returns("<body></body>");

        await Renderer().RenderAsync(Request(), new ActionContext());

        await _views.Received().RenderAsync(BlockPreviewRenderer.FrameView,
            Arg.Is<FrameModel>(m => m.LayoutPath == "~/Views/BlockPreview.cshtml" && m.PartialPath == Partial && m.Item == _item),
            Arg.Any<ActionContext>());
    }

    [Fact]
    public async Task Falls_back_to_the_built_in_layout()
    {
        ItemConverts();
        _views.ViewExists(Partial).Returns(true);
        _views.ViewExists("~/Views/BlockPreview.cshtml").Returns(false);
        _views.RenderAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<ActionContext>()).Returns("<body></body>");

        await Renderer(new BlockPreviewOptions { Stylesheets = ["/a.css"] }).RenderAsync(Request(), new ActionContext());

        await _views.Received().RenderAsync(BlockPreviewRenderer.FrameView,
            Arg.Is<FrameModel>(m => m.LayoutPath == BlockPreviewRenderer.DefaultLayoutView && m.Stylesheets.SequenceEqual(new[] { "/a.css" })),
            Arg.Any<ActionContext>());
    }

    [Fact]
    public async Task Rendered_html_gets_the_frame_script()
    {
        ItemConverts();
        _views.ViewExists(Arg.Any<string>()).Returns(true);
        _views.RenderAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<ActionContext>()).Returns("<body>ok</body>");

        var outcome = await Renderer().RenderAsync(Request(), new ActionContext());

        Assert.Equal("rendered", outcome.Status);
        Assert.Contains("esatto-block-preview:height", outcome.Html);
        Assert.Contains("\"n1\"", outcome.Html);
    }

    [Fact]
    public async Task Exceptions_become_error_outcomes()
    {
        _items.Create(Arg.Any<RenderRequest>()).Throws(new InvalidOperationException("boom"));
        var outcome = await Renderer().RenderAsync(Request(), new ActionContext());
        Assert.Equal("error", outcome.Status);
        Assert.Contains("boom", outcome.Message);
    }

    // Final review, Important 3: a nested Block List (inside a block) or a property the owner's type
    // doesn't have can't be converted against the document — show the plain row, not an error.
    [Fact]
    public async Task Unsupported_context_is_not_previewable()
    {
        _items.Create(Arg.Any<RenderRequest>()).Returns(new BlockItemResult(null, "carouselBlock", NotPreviewableReason: "unsupported"));
        Assert.Equal(RenderOutcome.NotPreviewable("unsupported"), await Renderer().RenderAsync(Request(), new ActionContext()));
    }

    // Final review, Critical 1: element types that vary by culture are only exposed for the edited
    // culture, so the CONVERSION (not just the rendering) must run under the request's culture.
    [Fact]
    public async Task Converts_the_block_under_the_requested_culture()
    {
        _items.GetContentTypeAlias(Arg.Any<Guid>()).Returns("carouselBlock");
        string? variationDuringConvert = null;
        _items.Create(Arg.Any<RenderRequest>()).Returns(_ =>
        {
            variationDuringConvert = _variation.VariationContext?.Culture;
            return new BlockItemResult(_item, "carouselBlock", NotPreviewableReason: null);
        });
        _views.ViewExists(Arg.Any<string>()).Returns(true);
        _views.RenderAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<ActionContext>()).Returns("<body></body>");

        await Renderer().RenderAsync(Request(culture: "en-GB"), new ActionContext());

        Assert.Equal("en-GB", variationDuringConvert);
        Assert.Null(_variation.VariationContext);
    }

    // Review Focus 1.
    [Fact]
    public async Task Sets_variation_context_and_culture_while_rendering()
    {
        ItemConverts();
        _views.ViewExists(Arg.Any<string>()).Returns(true);
        string? cultureDuringRender = null;
        string? variationDuringRender = null;
        _views.RenderAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<ActionContext>()).Returns(_ =>
        {
            cultureDuringRender = CultureInfo.CurrentUICulture.Name;
            variationDuringRender = _variation.VariationContext?.Culture;
            return "<body></body>";
        });
        var before = CultureInfo.CurrentUICulture;

        await Renderer().RenderAsync(Request(culture: "sv"), new ActionContext());

        Assert.Equal("sv", cultureDuringRender);
        Assert.Equal("sv", variationDuringRender);
        Assert.Equal(before, CultureInfo.CurrentUICulture);
    }
}
