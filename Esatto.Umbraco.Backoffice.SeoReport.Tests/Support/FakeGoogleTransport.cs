using System.Net;
using System.Text;
using Google.Apis.Http;
using Google.Apis.Services;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>
/// The HTTP transport that stands in for the network under one of Google's real service objects (see
/// <see cref="FakeAnalyticsData"/> and <see cref="FakeSearchConsole"/>). It records every request, address and body as they
/// would go on the wire, and answers with canned JSON: from a queue (in order; <c>{}</c> once it runs dry) or from a responder
/// that sees the request. The agents are then exercised through Google's own request and response handling, without a network,
/// a key or a real response.
/// </summary>
internal sealed class FakeGoogleTransport
{
    public sealed record Call(Uri Uri, string Body);

    public sealed record Answer(HttpStatusCode Status, string Json);

    private readonly Queue<Answer> _answers = new();
    private readonly Func<Call, Answer>? _responder;

    public FakeGoogleTransport(params string[] responses)
    {
        foreach (var json in responses) Enqueue(HttpStatusCode.OK, json);
    }

    public FakeGoogleTransport(Func<Call, Answer> responder) => _responder = responder;

    public List<Call> Calls { get; } = [];

    /// <summary>Queues one more answer, for example a failure: <c>Enqueue(HttpStatusCode.TooManyRequests, ...)</c>.</summary>
    public void Enqueue(HttpStatusCode status, string json) => _answers.Enqueue(new Answer(status, json));

    /// <summary>The initializer of a Google service that talks to this transport.</summary>
    public BaseClientService.Initializer Initializer() => new()
    {
        HttpClientFactory = new Factory(this),
        ApplicationName = "tests",

        // A gzipped request body would make the recorded text unreadable.
        GZipEnabled = false,
    };

    private Answer Respond(Call call)
        => _responder is not null ? _responder(call)
            : _answers.Count > 0 ? _answers.Dequeue()
            : new Answer(HttpStatusCode.OK, "{}");

    private sealed class Factory(FakeGoogleTransport owner) : HttpClientFactory
    {
        protected override HttpMessageHandler CreateHandler(CreateHttpClientArgs args) => new Handler(owner);
    }

    private sealed class Handler(FakeGoogleTransport owner) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            var call = new Call(request.RequestUri!, body);
            owner.Calls.Add(call);

            var answer = owner.Respond(call);
            return new HttpResponseMessage(answer.Status) { Content = new StringContent(answer.Json, Encoding.UTF8, "application/json") };
        }
    }
}
