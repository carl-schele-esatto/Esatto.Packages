using Backoffice.CrossContent;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Web;
using Xunit;

namespace Backoffice.CrossContent.Tests;

public class CrossContentTeaserControllerTests
{
    private const string Key = "secret-key";

    private static (CrossContentTeaserController controller, IUmbracoContextAccessor ctxAccessor, ICrossContentTeaserMapper mapper)
        CreateController(string configuredKey)
    {
        var ctxAccessor = Substitute.For<IUmbracoContextAccessor>();
        var mapper = Substitute.For<ICrossContentTeaserMapper>();
        var options = Options.Create(new DeliveryApiSettings { ApiKey = configuredKey });
        var controller = new CrossContentTeaserController(ctxAccessor, options, mapper)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        return (controller, ctxAccessor, mapper);
    }

    [Fact]
    public void Get_ReturnsUnauthorized_WhenNoApiKeyHeader()
    {
        var (controller, _, _) = CreateController(configuredKey: Key);
        Assert.IsType<UnauthorizedResult>(controller.Get(Guid.NewGuid()));
    }

    [Fact]
    public void Get_ReturnsUnauthorized_WhenApiKeyMismatch()
    {
        var (controller, _, _) = CreateController(configuredKey: Key);
        controller.HttpContext.Request.Headers["Api-Key"] = "wrong-key";
        Assert.IsType<UnauthorizedResult>(controller.Get(Guid.NewGuid()));
    }

    [Fact]
    public void Get_ReturnsUnauthorized_WhenConfiguredKeyEmpty()
    {
        var (controller, _, _) = CreateController(configuredKey: "");
        controller.HttpContext.Request.Headers["Api-Key"] = "anything";
        Assert.IsType<UnauthorizedResult>(controller.Get(Guid.NewGuid()));
    }

    [Fact]
    public void Get_ReturnsNotFound_WhenKeyValidButNodeMissing()
    {
        var (controller, ctxAccessor, _) = CreateController(configuredKey: Key);
        controller.HttpContext.Request.Headers["Api-Key"] = Key;

        var cache = Substitute.For<IPublishedContentCache>();
        cache.GetById(Arg.Any<Guid>()).Returns((IPublishedContent?)null);
        var umbracoContext = Substitute.For<IUmbracoContext>();
        umbracoContext.Content.Returns(cache);
        ctxAccessor.TryGetUmbracoContext(out Arg.Any<IUmbracoContext?>())
            .Returns(x => { x[0] = umbracoContext; return true; });

        Assert.IsType<NotFoundResult>(controller.Get(Guid.NewGuid()));
    }

    [Fact]
    public void Get_ReturnsNotFound_WhenMapperReturnsNull()
    {
        var (controller, ctxAccessor, mapper) = CreateController(configuredKey: Key);
        controller.HttpContext.Request.Headers["Api-Key"] = Key;

        var node = Substitute.For<IPublishedContent>();
        node.UpdateDate.Returns(new DateTime(2026, 1, 1));
        var cache = Substitute.For<IPublishedContentCache>();
        cache.GetById(Arg.Any<Guid>()).Returns(node);
        var umbracoContext = Substitute.For<IUmbracoContext>();
        umbracoContext.Content.Returns(cache);
        ctxAccessor.TryGetUmbracoContext(out Arg.Any<IUmbracoContext?>())
            .Returns(x => { x[0] = umbracoContext; return true; });
        mapper.Map(node, Arg.Any<string?>()).Returns((CrossContentTeaser?)null);

        Assert.IsType<NotFoundResult>(controller.Get(Guid.NewGuid()));
    }
}
