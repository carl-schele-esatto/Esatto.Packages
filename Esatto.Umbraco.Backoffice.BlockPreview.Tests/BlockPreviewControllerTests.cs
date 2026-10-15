using System.Security.Claims;
using Esatto.Umbraco.Backoffice.BlockPreview;
using Esatto.Umbraco.Backoffice.BlockPreview.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core.Models.PublishedContent;
using Xunit;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Tests;

// Final review, Important 9: a backoffice user must only render previews of documents they may read
// (the partial renders draft data with preview: true).
public class BlockPreviewControllerTests
{
    private readonly IBlockItemFactory _items = Substitute.For<IBlockItemFactory>();
    private readonly IAuthorizationService _authorization = Substitute.For<IAuthorizationService>();

    private BlockPreviewController Controller()
    {
        var renderer = new BlockPreviewRenderer(_items, Substitute.For<IViewRenderer>(), Substitute.For<IVariationContextAccessor>(),
            Options.Create(new BlockPreviewOptions()), NullLogger<BlockPreviewRenderer>.Instance);
        return new BlockPreviewController(renderer, _authorization)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() } },
        };
    }

    [Fact]
    public async Task Forbids_documents_the_user_may_not_read()
    {
        _authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(AuthorizationResult.Failed());

        IActionResult result = await Controller().Render(new RenderRequest { DocumentKey = Guid.NewGuid() });

        Assert.IsNotType<OkObjectResult>(result);
        _items.DidNotReceive().Create(Arg.Any<RenderRequest>());
    }

    [Fact]
    public async Task Renders_documents_the_user_may_read()
    {
        _authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(AuthorizationResult.Success());

        IActionResult result = await Controller().Render(new RenderRequest { DocumentKey = Guid.NewGuid() });

        Assert.IsType<OkObjectResult>(result);
    }
}
