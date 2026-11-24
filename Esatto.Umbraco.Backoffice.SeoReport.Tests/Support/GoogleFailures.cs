using System.Net;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Google;
using Google.Apis.AnalyticsData.v1beta.Data;
using Google.Apis.SearchConsole.v1.Data;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>
/// Exceptions as Google's own client raises them: a real service object whose transport answers with a status and a body (see
/// <see cref="GoogleErrorBodies"/>, whose message carries <see cref="FakeServiceAccount.LeakMarker"/>), so the fields the error
/// mapper reads are the ones production fills.
/// </summary>
internal static class GoogleFailures
{
    public static async Task<GoogleApiException> Ga4Async(HttpStatusCode status, string body)
    {
        var fake = new FakeAnalyticsData(new FakeGoogleTransport(_ => new FakeGoogleTransport.Answer(status, body)));
        return await Assert.ThrowsAsync<GoogleApiException>(
            () => fake.Service.Properties.RunReport(new RunReportRequest(), "properties/123456789").ExecuteAsync());
    }

    public static async Task<GoogleApiException> SearchConsoleAsync(HttpStatusCode status, string body)
    {
        var fake = new FakeSearchConsole(new FakeGoogleTransport(_ => new FakeGoogleTransport.Answer(status, body)));
        return await Assert.ThrowsAsync<GoogleApiException>(
            () => fake.Service.Searchanalytics.Query(new SearchAnalyticsQueryRequest(), "sc-domain:example.com").ExecuteAsync());
    }

    /// <summary>Every kind <see cref="FailureOf"/> knows, in the order a table of answers lists them.</summary>
    public static readonly string[] Kinds =
    [
        "notConfigured", "keyUnreadable", "keyInvalid", "apiDisabled", "noAccess", "propertyNotFound", "quota", "serverError",
        "surprise", "timeout", "unlistedCode",
    ];

    /// <summary>
    /// The exceptions a call to Google or to the key can end with, by name, each as the code makes it in production (Google's own
    /// client over a fake transport), with <see cref="FakeServiceAccount.LeakMarker"/> in the text of every one that has any.
    /// </summary>
    public static async Task<Exception> FailureOf(string kind) => kind switch
    {
        "notConfigured" => new ReportFetchException(ErrorCodes.NotConfigured),
        "keyUnreadable" => new ReportFetchException(ErrorCodes.KeyUnreadable),
        "keyInvalid" => new ReportFetchException(ErrorCodes.KeyInvalid),
        "apiDisabled" => await Ga4Async(HttpStatusCode.Forbidden, GoogleErrorBodies.Disabled()),
        "noAccess" => await Ga4Async(HttpStatusCode.Forbidden, GoogleErrorBodies.Denied()),
        "propertyNotFound" => await SearchConsoleAsync(HttpStatusCode.NotFound, GoogleErrorBodies.Error(404, "NOT_FOUND")),
        "quota" => await Ga4Async(HttpStatusCode.TooManyRequests, GoogleErrorBodies.Error(429, "RESOURCE_EXHAUSTED")),
        "serverError" => await Ga4Async(HttpStatusCode.InternalServerError, GoogleErrorBodies.Error(500, "INTERNAL")),
        "surprise" => new InvalidOperationException($"surprise {FakeServiceAccount.LeakMarker}"),
        "timeout" => new HttpRequestException($"timeout {FakeServiceAccount.LeakMarker}"),
        "unlistedCode" => new ReportFetchException($"unlisted {FakeServiceAccount.LeakMarker}"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
