using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Google.Apis.AnalyticsData.v1beta;
using Google.Apis.SearchConsole.v1;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>
/// A real <see cref="AnalyticsDataService"/> whose HTTP transport is replaced (see <see cref="FakeGoogleTransport"/>): it records
/// every request (address and body, as they would go on the wire) and answers with canned JSON, so the agents are exercised
/// through Google's own request and response handling without a network, a key or a real response.
/// </summary>
internal sealed class FakeAnalyticsData : IGoogleClients
{
    public FakeAnalyticsData(FakeGoogleTransport transport)
    {
        Transport = transport;
        Service = new AnalyticsDataService(transport.Initializer());
    }

    public FakeAnalyticsData(params string[] responses) : this(new FakeGoogleTransport(responses))
    {
    }

    public FakeGoogleTransport Transport { get; }

    public AnalyticsDataService Service { get; }

    public List<FakeGoogleTransport.Call> Calls => Transport.Calls;

    /// <summary>How many times an agent asked for the service, whether or not it then made a call.</summary>
    public int ServiceRequests { get; private set; }

    public Task<AnalyticsDataService> GetAnalyticsDataAsync(CancellationToken ct)
    {
        ServiceRequests++;
        return Task.FromResult(Service);
    }

    public Task<SearchConsoleService> GetSearchConsoleAsync(CancellationToken ct) => throw new NotSupportedException();
}
