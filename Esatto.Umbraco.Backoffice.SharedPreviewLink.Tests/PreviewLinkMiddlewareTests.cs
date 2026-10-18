using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
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

    private static Task InvokeAsync(
        PreviewLinkMiddleware mw, HttpContext ctx, IPreviewTokenGenerator? previewTokenGenerator = null) =>
        mw.InvokeAsync(
            ctx,
            Substitute.For<IUmbracoContextFactory>(),
            Substitute.For<IPublishedUrlProvider>(),
            previewTokenGenerator ?? Substitute.For<IPreviewTokenGenerator>(),
            Substitute.For<IDocumentNavigationQueryService>(),
            Substitute.For<IDocumentUrlService>());

    // Generator that verifies only the given cookie values; everything else is
    // undecryptable (e.g. minted under a previous DataProtection key ring).
    private static IPreviewTokenGenerator GeneratorAccepting(params string[] validTokens)
    {
        var generator = Substitute.For<IPreviewTokenGenerator>();
        generator.VerifyAsync(Arg.Any<string>())
            .Returns(call => Task.FromResult(validTokens.Contains(call.Arg<string>())
                ? Attempt.Succeed((Guid?)Guid.NewGuid())
                : Attempt.Fail<Guid?>(null)));
        return generator;
    }

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

    // The browser sends the share link's path-scoped UMB_PREVIEW first and a stale
    // site-wide one (left by the backoffice "Preview" button) after it. ASP.NET Core
    // keeps the LAST duplicate, so without intervention Umbraco sees the stale cookie,
    // fails to verify it and renders the draft as a 404.
    [Fact]
    public async Task Duplicate_preview_cookies_keep_the_verifiable_one_when_it_comes_first()
    {
        var (mw, next, _) = CreateSut();
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/test";
        ctx.Request.Headers.Cookie = "UMB_PREVIEW=share; UMB_PREVIEW=stale; other=1";

        await InvokeAsync(mw, ctx, GeneratorAccepting("share"));

        Assert.True(next.WasCalled);
        Assert.Equal("share", ctx.Request.Cookies[Constants.Web.PreviewCookieName]);
        Assert.Equal("1", ctx.Request.Cookies["other"]);
    }

    [Fact]
    public async Task Duplicate_preview_cookies_keep_the_first_verifiable_one_not_just_the_first()
    {
        var (mw, next, _) = CreateSut();
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/test";
        ctx.Request.Headers.Cookie = "UMB_PREVIEW=stale1; UMB_PREVIEW=share; UMB_PREVIEW=stale2";

        await InvokeAsync(mw, ctx, GeneratorAccepting("share"));

        Assert.True(next.WasCalled);
        Assert.Equal("share", ctx.Request.Cookies[Constants.Web.PreviewCookieName]);
    }

    [Fact]
    public async Task Single_preview_cookie_is_left_alone_without_verifying()
    {
        var (mw, next, _) = CreateSut();
        var generator = GeneratorAccepting();
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/test";
        ctx.Request.Headers.Cookie = "UMB_PREVIEW=only; other=1";

        await InvokeAsync(mw, ctx, generator);

        Assert.True(next.WasCalled);
        Assert.Equal("only", ctx.Request.Cookies[Constants.Web.PreviewCookieName]);
        await generator.DidNotReceive().VerifyAsync(Arg.Any<string>());
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
