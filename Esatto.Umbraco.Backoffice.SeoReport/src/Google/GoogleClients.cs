using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Google.Apis.AnalyticsData.v1beta;
using Google.Apis.Auth.OAuth2;
using Google.Apis.SearchConsole.v1;
using Google.Apis.Services;

namespace Esatto.Umbraco.Backoffice.SeoReport.Google;

/// <summary>
/// The Google services for the credential in force. The services are built once per credential and reused, and built again
/// when <see cref="IGoogleCredentialSource"/> hands back a different credential (a key was saved or removed). With no usable
/// credential every call throws <see cref="ReportFetchException"/> carrying the credential layer's code, and no service is built.
/// </summary>
public sealed class GoogleClients(IGoogleCredentialSource credentials, IGoogleClientFactory factory) : IGoogleClients
{
    private readonly object _gate = new();

    // The pair is replaced only when the credential source hands back a different credential, which it does only when the key or
    // where it comes from really changes (it returns the same instance for the same key, however often it resolves again). The
    // replaced pair is not disposed: another call may have taken it just before the swap and still be using it, and disposing
    // would cancel that call. It holds only an HTTP client, which the garbage collector reclaims; a key changes rarely.
    private Built? _current;

    public async Task<AnalyticsDataService> GetAnalyticsDataAsync(CancellationToken ct)
    {
        var clients = await CurrentAsync(ct).ConfigureAwait(false);
        return await clients.GetAnalyticsDataAsync(ct).ConfigureAwait(false);
    }

    public async Task<SearchConsoleService> GetSearchConsoleAsync(CancellationToken ct)
    {
        var clients = await CurrentAsync(ct).ConfigureAwait(false);
        return await clients.GetSearchConsoleAsync(ct).ConfigureAwait(false);
    }

    private async Task<IGoogleClients> CurrentAsync(CancellationToken ct)
    {
        var result = await credentials.GetAsync(ct).ConfigureAwait(false);
        if (result.ErrorCode is not null || result.Credential is not { } credential)
        {
            throw new ReportFetchException(result.ErrorCode ?? KeyErrorCodes.NotConfigured);
        }

        lock (_gate)
        {
            if (_current is { } current && ReferenceEquals(current.Credential, credential)) return current.Clients;

            var created = factory.Create(credential);
            _current = new Built(credential, created);
            return created;
        }
    }

    private sealed record Built(GoogleCredential Credential, IGoogleClients Clients);
}

/// <summary>The default factory: the real Google services, named after this package, authorised with the credential.</summary>
public sealed class GoogleClientFactory : IGoogleClientFactory
{
    /// <summary>The application name Google sees in the user agent of every call.</summary>
    public const string ApplicationName = "Esatto.Umbraco.Backoffice.SeoReport";

    public IGoogleClients Create(GoogleCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        return new GoogleServices(credential);
    }
}

/// <summary>The two services for one fixed credential, each created on first use and then kept.</summary>
internal sealed class GoogleServices(GoogleCredential credential) : IGoogleClients
{
    private readonly Lazy<AnalyticsDataService> _analyticsData = new(() => new AnalyticsDataService(Initializer(credential)));

    private readonly Lazy<SearchConsoleService> _searchConsole = new(() => new SearchConsoleService(Initializer(credential)));

    public Task<AnalyticsDataService> GetAnalyticsDataAsync(CancellationToken ct) => Task.FromResult(_analyticsData.Value);

    public Task<SearchConsoleService> GetSearchConsoleAsync(CancellationToken ct) => Task.FromResult(_searchConsole.Value);

    private static BaseClientService.Initializer Initializer(GoogleCredential credential) => new()
    {
        HttpClientInitializer = credential,
        ApplicationName = GoogleClientFactory.ApplicationName,
    };
}
