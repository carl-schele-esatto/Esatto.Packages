using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Google.Apis.AnalyticsData.v1beta;
using Google.Apis.SearchConsole.v1;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>
/// A real <see cref="SearchConsoleService"/> whose HTTP transport is replaced (see <see cref="FakeGoogleTransport"/>): the same
/// idea as <see cref="FakeAnalyticsData"/>, for the Search Console agent.
/// </summary>
internal sealed class FakeSearchConsole : IGoogleClients
{
    public FakeSearchConsole(FakeGoogleTransport transport)
    {
        Transport = transport;
        Service = new SearchConsoleService(transport.Initializer());
    }

    public FakeSearchConsole(params string[] responses) : this(new FakeGoogleTransport(responses))
    {
    }

    public FakeGoogleTransport Transport { get; }

    public SearchConsoleService Service { get; }

    public List<FakeGoogleTransport.Call> Calls => Transport.Calls;

    /// <summary>How many times an agent asked for the service, whether or not it then made a call.</summary>
    public int ServiceRequests { get; private set; }

    public Task<AnalyticsDataService> GetAnalyticsDataAsync(CancellationToken ct) => throw new NotSupportedException();

    public Task<SearchConsoleService> GetSearchConsoleAsync(CancellationToken ct)
    {
        ServiceRequests++;
        return Task.FromResult(Service);
    }
}
