using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Google.Apis.AnalyticsData.v1beta;
using Google.Apis.SearchConsole.v1;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>
/// Two services of one <see cref="IGoogleClients"/> that answer from two separate fakes, each with its own transport (see
/// <see cref="FakeAnalyticsData"/> and <see cref="FakeSearchConsole"/>), so a test can give each source its own answer or failure and
/// count the calls each one got. <see cref="Hold"/> parks every GA4 service request until it completes, which freezes a run at its
/// first GA4 call (see <see cref="SyntheticGoogle.Hold"/>).
/// </summary>
internal sealed class SplitGoogle(FakeAnalyticsData ga4, FakeSearchConsole searchConsole) : IGoogleClients
{
    public FakeAnalyticsData Ga4 { get; } = ga4;

    public FakeSearchConsole SearchConsole { get; } = searchConsole;

    /// <summary>When set, every GA4 service request waits for it.</summary>
    public Task? Hold { get; set; }

    /// <summary>When set, a service request throws it, as the real clients do when there is no usable credential.</summary>
    public Exception? ServiceFailure { get; set; }

    /// <summary>Every call either service received.</summary>
    public int Calls => Ga4.Calls.Count + SearchConsole.Calls.Count;

    private int _ga4ServiceRequests;

    /// <summary>How many times GA4 was asked for, counted before <see cref="Hold"/> parks the request.</summary>
    public int Ga4ServiceRequests => Volatile.Read(ref _ga4ServiceRequests);

    public async Task<AnalyticsDataService> GetAnalyticsDataAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref _ga4ServiceRequests);
        if (ServiceFailure is { } failure) throw failure;
        if (Hold is { } hold) await hold.WaitAsync(ct);
        return await Ga4.GetAnalyticsDataAsync(ct);
    }

    public Task<SearchConsoleService> GetSearchConsoleAsync(CancellationToken ct)
        => ServiceFailure is { } failure ? throw failure : SearchConsole.GetSearchConsoleAsync(ct);
}
