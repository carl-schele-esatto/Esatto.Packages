using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class VaryByConsentCookieMiddlewareTests
{
    /// <summary>
    /// DefaultHttpContext's built-in response feature silently no-ops OnStarting, so the
    /// middleware's callback would never run and every assertion would pass vacuously. This
    /// decorator records the callbacks and delegates status/headers/body to the real feature, so
    /// header writes still land on context.Response.Headers.
    /// </summary>
    private sealed class CallbackCapturingResponseFeature(IHttpResponseFeature inner) : IHttpResponseFeature
    {
        private readonly List<Func<Task>> _onStarting = [];

        public int StatusCode { get => inner.StatusCode; set => inner.StatusCode = value; }

        public string? ReasonPhrase { get => inner.ReasonPhrase; set => inner.ReasonPhrase = value; }

        public IHeaderDictionary Headers { get => inner.Headers; set => inner.Headers = value; }

#pragma warning disable CS0618 // Required to implement IHttpResponseFeature; never invoked by these tests.
        public Stream Body { get => inner.Body; set => inner.Body = value; }
#pragma warning restore CS0618

        public bool HasStarted => inner.HasStarted;

        public int RegisteredCallbacks => _onStarting.Count;

        public void OnStarting(Func<object, Task> callback, object state)
            => _onStarting.Add(() => callback(state));

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public async Task FireOnStartingAsync()
        {
            foreach (Func<Task> callback in _onStarting)
            {
                await callback();
            }
        }
    }

    private sealed record Invocation(
        DefaultHttpContext Context,
        CallbackCapturingResponseFeature Feature,
        int NextInvocations);

    private static async Task<Invocation> InvokeAsync(string path, string? contentType)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        var feature = new CallbackCapturingResponseFeature(context.Features.Get<IHttpResponseFeature>()!);
        context.Features.Set<IHttpResponseFeature>(feature);

        var nextInvocations = 0;
        var middleware = new VaryByConsentCookieMiddleware(inner =>
        {
            nextInvocations++;
            if (contentType is not null)
            {
                inner.Response.ContentType = contentType;
            }

            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        // The server fires these when the response is about to start, i.e. after the pipeline
        // above has settled the content type.
        await feature.FireOnStartingAsync();

        return new Invocation(context, feature, nextInvocations);
    }

    [Fact]
    public async Task Front_end_html_is_marked_private_and_varying_by_the_consent_cookie()
    {
        // Consent-gated markup (the banner, the gated Google tag) is baked in server-side. Without
        // these headers a shared cache could serve one visitor's consent state to another.
        Invocation invocation = await InvokeAsync("/about", "text/html; charset=utf-8");

        Assert.Equal("Cookie", invocation.Context.Response.Headers.Vary.ToString());
        Assert.Equal("private, no-cache", invocation.Context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Json_responses_are_left_untouched()
    {
        // Scoped to text/html on purpose: API and static-asset responses must keep whatever
        // caching the host chose for them.
        Invocation invocation = await InvokeAsync("/api/cookie-consent", "application/json");

        Assert.Empty(invocation.Context.Response.Headers.Vary.ToString());
        Assert.Empty(invocation.Context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Backoffice_html_is_left_untouched()
    {
        // /umbraco is excluded by path, and excluded before the callback is even registered, so
        // the backoffice pays nothing for a front-end concern.
        Invocation invocation = await InvokeAsync("/umbraco/section/content", "text/html; charset=utf-8");

        Assert.Equal(0, invocation.Feature.RegisteredCallbacks);
        Assert.Empty(invocation.Context.Response.Headers.Vary.ToString());
        Assert.Empty(invocation.Context.Response.Headers.CacheControl.ToString());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/umbraco/section/content")]
    public async Task Next_is_always_invoked(string path)
    {
        // The middleware only annotates; it must never terminate a request on any path.
        Invocation invocation = await InvokeAsync(path, "text/html");

        Assert.Equal(1, invocation.NextInvocations);
    }
}
