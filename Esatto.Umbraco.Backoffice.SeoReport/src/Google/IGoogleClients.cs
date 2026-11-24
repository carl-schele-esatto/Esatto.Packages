using Google.Apis.AnalyticsData.v1beta;
using Google.Apis.Auth.OAuth2;
using Google.Apis.SearchConsole.v1;

namespace Esatto.Umbraco.Backoffice.SeoReport.Google;

/// <summary>
/// The two Google services the report reads from, ready to call with the credential in force. The agents ask for a service
/// on every call and never keep it, so a changed key takes effect on the next call. When there is no usable credential the
/// methods throw <see cref="Reporting.ReportFetchException"/> carrying the credential layer's error code.
/// </summary>
public interface IGoogleClients
{
    Task<AnalyticsDataService> GetAnalyticsDataAsync(CancellationToken ct);

    Task<SearchConsoleService> GetSearchConsoleAsync(CancellationToken ct);
}

/// <summary>
/// Builds the pair of services for one credential. It exists so a key that is typed in but not saved yet can be tested with
/// exactly the services a saved key would get.
/// </summary>
public interface IGoogleClientFactory
{
    IGoogleClients Create(GoogleCredential credential);
}
