using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Core.Preview;
using Esatto.Umbraco.Backoffice.SharedPreviewLink;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SharedPreviewLink.Tests;

public class PreviewLinkMiddlewareTests
{
    private static (PreviewLinkMiddleware mw, RequestDelegateProbe next, ITimeLimitedDataProtector protector)
        CreateSut()
    {
        var provider = new EphemeralDataProtectionProvider();
        var protector = provider.CreateProtector(PreviewLinkController.ProtectorPurpose).ToTimeLimitedDataProtector();
        var next = new RequestDelegateProbe();
        var mw = new PreviewLinkMiddleware(next.Invoke, provider, NullLogger<PreviewLinkMiddleware>.Instance);
        return (mw, next, protector);
    }

    private static Task InvokeAsync(PreviewLinkMiddleware mw, HttpContext ctx) =>
        mw.InvokeAsync(
            ctx,
            Substitute.For<IUmbracoContextFactory>(),
            Substitute.For<IPublishedUrlProvider>(),
            Substitute.For<IPreviewTokenGenerator>(),
            Substitute.For<IDocumentNavigationQueryService>());

    [Fact]
    public async Task No_preview_token_calls_next_and_does_nothing()
    {
        var (mw, next, _) = CreateSut();
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/some/page";

        await InvokeAsync(mw, ctx);

        Assert.True(next.WasCalled);
        Assert.False(ctx.Items.ContainsKey(PreviewLinkMiddleware.ErrorItemsKey));
    }

    [Fact]
    public async Task Garbage_token_renders_expired_410()
    {
        // A malformed/garbage token surfaces as CryptographicException, which the
        // middleware maps (along with tampered AND genuinely-expired tokens) to the
        // single "expired" / 410 error page.
        var (mw, next, _) = CreateSut();
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/some/page";
        ctx.Request.QueryString = QueryString.Create(PreviewLinkMiddleware.QueryParam, "garbage-token");

        await InvokeAsync(mw, ctx);

        Assert.Equal("expired", ctx.Items[PreviewLinkMiddleware.ErrorItemsKey]);
        Assert.Equal(StatusCodes.Status410Gone, ctx.Response.StatusCode);
        Assert.Equal("/preview-link-error", ctx.Request.Path);
        Assert.True(next.WasCalled);
    }

    [Fact]
    public async Task Non_guid_payload_renders_invalid_400()
    {
        var (mw, next, protector) = CreateSut();
        var token = protector.Protect("not-a-guid", TimeSpan.FromDays(7));
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/some/page";
        ctx.Request.QueryString = QueryString.Create(PreviewLinkMiddleware.QueryParam, token);

        await InvokeAsync(mw, ctx);

        Assert.Equal("invalid", ctx.Items[PreviewLinkMiddleware.ErrorItemsKey]);
        Assert.Equal(StatusCodes.Status400BadRequest, ctx.Response.StatusCode);
        Assert.Equal("/preview-link-error", ctx.Request.Path);
        Assert.True(next.WasCalled);
    }
}

// Minimal RequestDelegate that records whether it ran.
internal sealed class RequestDelegateProbe
{
    public bool WasCalled { get; private set; }
    public Task Invoke(HttpContext context)
    {
        WasCalled = true;
        return Task.CompletedTask;
    }
}
