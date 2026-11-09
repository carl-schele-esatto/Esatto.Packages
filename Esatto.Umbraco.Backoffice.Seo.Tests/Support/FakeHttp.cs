using System.Net;
using System.Text;
using Esatto.Umbraco.Backoffice.Seo.Scanning;
using Esatto.Umbraco.Backoffice.Seo.Settings;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace Esatto.Umbraco.Backoffice.Seo.Tests.Support;

/// <summary>An in-memory web: URL → response. Unknown URLs answer 404; <see cref="Down"/> hosts fail to connect.</summary>
internal sealed class FakeHttp : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _routes = new(StringComparer.OrdinalIgnoreCase);

    public List<HttpRequestMessage> Requests { get; } = [];
    public HashSet<string> Down { get; } = new(StringComparer.OrdinalIgnoreCase);

    public FakeHttp Html(string url, string html = "<html><head><title>t</title></head><body>x</body></html>", HttpStatusCode status = HttpStatusCode.OK, string? xRobots = null)
        => On(url, _ =>
        {
            var r = new HttpResponseMessage(status) { Content = new StringContent(html, Encoding.UTF8, "text/html") };
            if (xRobots is not null) r.Headers.Add("X-Robots-Tag", xRobots);
            return r;
        });

    public FakeHttp Text(string url, string text, string contentType = "text/plain")
        => On(url, _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, contentType) });

    public FakeHttp Bytes(string url, byte[] bytes, string contentType)
        => On(url, _ =>
        {
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

    public FakeHttp Redirect(string from, string to, HttpStatusCode status = HttpStatusCode.MovedPermanently)
        => On(from, _ =>
        {
            var r = new HttpResponseMessage(status);
            r.Headers.Location = new Uri(to, UriKind.RelativeOrAbsolute);
            return r;
        });

    public FakeHttp On(string url, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _routes[url] = (req, _) => Task.FromResult(respond(req));
        return this;
    }

    public FakeHttp OnAsync(string url, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        _routes[url] = respond;
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (Requests) Requests.Add(request);
        if (Down.Contains(request.RequestUri!.Host)) throw new HttpRequestException("No such host is known.");
        return _routes.TryGetValue(request.RequestUri!.ToString(), out var respond)
            ? respond(request, cancellationToken)
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("not found", Encoding.UTF8, "text/html") });
    }

    public int Count(string url, HttpMethod? method = null)
    {
        lock (Requests) return Requests.Count(r => r.RequestUri!.ToString().Equals(url, StringComparison.OrdinalIgnoreCase) && (method is null || r.Method == method));
    }

    public PageFetcher Fetcher(Dictionary<string, string?>? config = null)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(this, disposeHandler: false));
        return new PageFetcher(factory, new SeoEnvironment(new ConfigurationBuilder().AddInMemoryCollection(config ?? new()).Build()));
    }
}
