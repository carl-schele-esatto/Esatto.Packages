using System.Globalization;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Google.Apis.AnalyticsData.v1beta.Data;

namespace Esatto.Umbraco.Backoffice.SeoReport.Google;

/// <summary>What the monthly and the daily GA4 agents share: the names of the fields they ask for, the pieces of a request, and the one place a request is sent.</summary>
internal static class Ga4Requests
{
    public const string SessionsMetric = "sessions";

    public const string ChannelGroupDimension = "sessionDefaultChannelGroup";

    public const string SourceDimension = "sessionSource";

    /// <summary>Refuses a property ID that is missing, or still the <c>XXXX</c> placeholder of a template configuration, before any call.</summary>
    public static void RequirePropertyId(string propertyId)
    {
        if (string.IsNullOrWhiteSpace(propertyId) || propertyId.Contains("XXXX", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The GA4 property ID is not set. Enter it on the Settings tab, or set the configuration value "
                + $"'{SeoReportSettings.SectionName}:{nameof(SeoReportSettings.Ga4PropertyId)}'. It has the form 'properties/123456789'.");
        }
    }

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateRange Range(DateOnly start, DateOnly end) => new() { StartDate = Iso(start), EndDate = Iso(end) };

    public static OrderBy ByDimension(string name) => new() { Dimension = new DimensionOrderBy { DimensionName = name } };

    /// <summary>A response's rows. Google's REST API leaves <c>rows</c> out of an empty report, so the list can be null.</summary>
    public static IList<Row> RowsOf(RunReportResponse? response) => response?.Rows ?? [];

    /// <summary>
    /// Sends <paramref name="request"/> to the property it names. Google documents the property as specified in the address of
    /// the call (<c>properties/123456789:runReport</c>) and not in the body, so the request's own <c>Property</c> moves to the
    /// address and is cleared: the body is then exactly what Google's documentation shows. The request is spent afterwards.
    /// </summary>
    public static async Task<RunReportResponse> RunAsync(IGoogleClients clients, RunReportRequest request, CancellationToken ct)
    {
        var service = await clients.GetAnalyticsDataAsync(ct).ConfigureAwait(false);

        var property = request.Property;
        request.Property = null;
        return await service.Properties.RunReport(request, property).ExecuteAsync(ct).ConfigureAwait(false);
    }
}
