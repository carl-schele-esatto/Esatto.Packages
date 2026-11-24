using System.Net;
using System.Text.Json;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Google;
using Google.Apis.AnalyticsData.v1beta.Data;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Requests;
using Google.Apis.SearchConsole.v1.Data;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// Whatever goes wrong with a Google call becomes one of a few fixed codes plus, at most, the name of the API or the source,
/// never any text of the exception. The Google exceptions in these tests are produced the way production produces them: by a
/// real Google service whose HTTP transport answers with a status and a body, so the fields the mapper reads (the status, the
/// reasons in the error list, the status and details of the raw body, the service name) are the ones Google's client really fills.
/// </summary>
public class GoogleErrorMapperTests
{
    private const string Marker = FakeServiceAccount.LeakMarker;

    // ---- Bodies as Google writes them (see GoogleErrorBodies) --------------------------------------------------------

    private static string Disabled(bool inList = true, bool inDetails = true) => GoogleErrorBodies.Disabled(inList, inDetails);

    private static string Denied() => GoogleErrorBodies.Denied();

    private static string Error(int code, string status, string? reason = null) => GoogleErrorBodies.Error(code, status, reason);

    // ---- Exceptions from the real services ----------------------------------------------------------------------------

    private static async Task<GoogleApiException> Ga4Failure(HttpStatusCode status, string body)
    {
        var fake = new FakeAnalyticsData(new FakeGoogleTransport(_ => new FakeGoogleTransport.Answer(status, body)));
        return await Assert.ThrowsAsync<GoogleApiException>(
            () => fake.Service.Properties.RunReport(new RunReportRequest(), "properties/123456789").ExecuteAsync());
    }

    private static async Task<GoogleApiException> SearchConsoleFailure(HttpStatusCode status, string body)
    {
        var fake = new FakeSearchConsole(new FakeGoogleTransport(_ => new FakeGoogleTransport.Answer(status, body)));
        return await Assert.ThrowsAsync<GoogleApiException>(
            () => fake.Service.Searchanalytics.Query(new SearchAnalyticsQueryRequest(), "sc-domain:example.com").ExecuteAsync());
    }

    private static Dictionary<string, object?> Expect(params (string Name, object? Value)[] args) => args.ToDictionary(a => a.Name, a => a.Value);

    // ---- apiDisabled --------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task A_403_for_a_disabled_API_names_the_API_of_the_service_that_failed(bool reasonInList, bool reasonInDetails)
    {
        var ga4 = GoogleErrorMapper.Map(await Ga4Failure(HttpStatusCode.Forbidden, Disabled(reasonInList, reasonInDetails)));
        var searchConsole = GoogleErrorMapper.Map(await SearchConsoleFailure(HttpStatusCode.Forbidden, Disabled(reasonInList, reasonInDetails)));

        Assert.Equal(ErrorCodes.ApiDisabled, ga4.Code);
        Assert.Equal(Expect(("api", "analyticsData")), ga4.Args);
        Assert.Equal(ErrorCodes.ApiDisabled, searchConsole.Code);
        Assert.Equal(Expect(("api", "searchConsole")), searchConsole.Args);
    }

    [Fact]
    public async Task A_disabled_API_is_not_taken_for_a_missing_permission_although_both_are_a_403_PERMISSION_DENIED()
    {
        var disabled = GoogleErrorMapper.Map(await Ga4Failure(HttpStatusCode.Forbidden, Disabled()));
        var denied = GoogleErrorMapper.Map(await Ga4Failure(HttpStatusCode.Forbidden, Denied()));

        Assert.Equal(ErrorCodes.ApiDisabled, disabled.Code);
        Assert.Equal(ErrorCodes.NoAccess, denied.Code);
    }

    [Fact]
    public void The_API_comes_from_the_caller_when_the_exception_does_not_say_which_service_failed()
    {
        var error = new GoogleApiException("some-other-service", "m")
        {
            HttpStatusCode = HttpStatusCode.Forbidden,
            Error = new RequestError { Code = 403, Errors = [new SingleError { Reason = "accessNotConfigured" }] },
        };

        Assert.Null(GoogleErrorMapper.Map(error).Args);
        Assert.Equal(Expect(("api", "searchConsole")), GoogleErrorMapper.Map(error, GoogleErrorMapper.SearchConsole).Args);
        Assert.Equal(Expect(("api", "analyticsData")), GoogleErrorMapper.Map(error, GoogleErrorMapper.Ga4).Args);
    }

    // ---- noAccess -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_403_PERMISSION_DENIED_is_no_access_and_names_the_source()
    {
        var ga4 = GoogleErrorMapper.Map(await Ga4Failure(HttpStatusCode.Forbidden, Denied()));
        var searchConsole = GoogleErrorMapper.Map(await SearchConsoleFailure(HttpStatusCode.Forbidden, Denied()));

        Assert.Equal(ErrorCodes.NoAccess, ga4.Code);
        Assert.Equal(Expect(("source", "ga4")), ga4.Args);
        Assert.Equal(ErrorCodes.NoAccess, searchConsole.Code);
        Assert.Equal(Expect(("source", "searchConsole")), searchConsole.Args);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "UNAUTHENTICATED")]
    [InlineData(HttpStatusCode.Forbidden, "FORBIDDEN")]
    public async Task Any_other_401_or_403_is_no_access_too(HttpStatusCode status, string rpcStatus)
    {
        var info = GoogleErrorMapper.Map(await Ga4Failure(status, Error((int)status, rpcStatus)));

        Assert.Equal(ErrorCodes.NoAccess, info.Code);
        Assert.Equal(Expect(("source", "ga4")), info.Args);
    }

    [Fact]
    public async Task The_source_the_caller_knows_is_used_and_beats_the_one_the_service_name_implies()
    {
        var fromGa4Service = await Ga4Failure(HttpStatusCode.Forbidden, Denied());

        Assert.Equal(Expect(("source", "searchConsole")), GoogleErrorMapper.Map(fromGa4Service, GoogleErrorMapper.SearchConsole).Args);
    }

    // ---- propertyNotFound ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_404_is_property_not_found_and_names_the_source()
    {
        var ga4 = GoogleErrorMapper.Map(await Ga4Failure(HttpStatusCode.NotFound, Error(404, "NOT_FOUND", "notFound")));
        var searchConsole = GoogleErrorMapper.Map(await SearchConsoleFailure(HttpStatusCode.NotFound, Error(404, "NOT_FOUND")));

        Assert.Equal(ErrorCodes.PropertyNotFound, ga4.Code);
        Assert.Equal(Expect(("source", "ga4")), ga4.Args);
        Assert.Equal(ErrorCodes.PropertyNotFound, searchConsole.Code);
        Assert.Equal(Expect(("source", "searchConsole")), searchConsole.Args);
    }

    // ---- quota --------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_429_is_quota()
    {
        var info = GoogleErrorMapper.Map(await Ga4Failure(HttpStatusCode.TooManyRequests, Error(429, "RESOURCE_EXHAUSTED")));

        Assert.Equal(ErrorCodes.Quota, info.Code);
        Assert.Null(info.Args);
    }

    [Theory]
    [InlineData("rateLimitExceeded")]
    [InlineData("userRateLimitExceeded")]
    [InlineData("quotaExceeded")]
    [InlineData("dailyLimitExceeded")]
    public async Task A_403_that_Google_gives_for_a_limit_is_quota_and_not_a_missing_permission(string reason)
    {
        var info = GoogleErrorMapper.Map(await SearchConsoleFailure(HttpStatusCode.Forbidden, Error(403, "PERMISSION_DENIED", reason)));

        Assert.Equal(ErrorCodes.Quota, info.Code);
        Assert.Null(info.Args);
    }

    [Fact]
    public async Task RESOURCE_EXHAUSTED_is_quota_whatever_the_HTTP_status()
    {
        var info = GoogleErrorMapper.Map(await Ga4Failure(HttpStatusCode.BadRequest, Error(400, "RESOURCE_EXHAUSTED")));

        Assert.Equal(ErrorCodes.Quota, info.Code);
    }

    // ---- unknown ------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "INVALID_ARGUMENT")]
    [InlineData(HttpStatusCode.InternalServerError, "INTERNAL")]
    [InlineData(HttpStatusCode.Conflict, "ABORTED")]
    public async Task Any_other_Google_failure_is_unknown_and_carries_nothing(HttpStatusCode status, string rpcStatus)
    {
        var info = GoogleErrorMapper.Map(await Ga4Failure(status, Error((int)status, rpcStatus)), GoogleErrorMapper.Ga4);

        Assert.Equal(ErrorCodes.Unknown, info.Code);
        Assert.Null(info.Args);
    }

    [Fact]
    public async Task A_body_that_is_not_JSON_is_read_by_its_status_alone()
    {
        var info = GoogleErrorMapper.Map(await Ga4Failure(HttpStatusCode.Forbidden, "<html>forbidden " + Marker + "</html>"));

        Assert.Equal(ErrorCodes.NoAccess, info.Code);
        Assert.Equal(Expect(("source", "ga4")), info.Args);
    }

    [Fact]
    public void The_status_comes_from_the_error_when_the_exception_has_none()
    {
        var error = new GoogleApiException("analyticsdata", "m") { Error = new RequestError { Code = 404 } };

        Assert.Equal(ErrorCodes.PropertyNotFound, GoogleErrorMapper.Map(error, GoogleErrorMapper.Ga4).Code);
    }

    [Fact]
    public void A_Google_exception_with_no_status_and_no_error_is_unknown()
        => Assert.Equal(ErrorCodes.Unknown, GoogleErrorMapper.Map(new GoogleApiException("analyticsdata", "m")).Code);

    [Theory]
    [MemberData(nameof(NonGoogleExceptions))]
    public void Every_other_kind_of_exception_is_unknown_and_carries_nothing(Exception error)
    {
        var info = GoogleErrorMapper.Map(error, GoogleErrorMapper.Ga4);

        Assert.Equal(ErrorCodes.Unknown, info.Code);
        Assert.Null(info.Args);
    }

    // The mapper does not treat a cancellation as one: a caller that cancels must not hand the exception to it.
    public static TheoryData<Exception> NonGoogleExceptions() => new()
    {
        new ArgumentException("m"),
        new InvalidOperationException("m"),
        new ArgumentOutOfRangeException("p"),
        new NullReferenceException("m"),
        new HttpRequestException("m"),
        new OperationCanceledException("m"),
        new TaskCanceledException("m"),
        new TimeoutException("m"),
    };

    // ---- The token endpoint refuses the key ---------------------------------------------------------------------------

    /// <summary>What Google's credential throws when the token endpoint answers a failure, with the marker in every text of it.</summary>
    internal static TokenResponseException Refused(string? error, HttpStatusCode status = HttpStatusCode.BadRequest)
        => new(new TokenErrorResponse { Error = error, ErrorDescription = Marker, ErrorUri = Marker }, status);

    [Theory]
    [InlineData("invalid_grant")]
    [InlineData("invalid_client")]
    [InlineData("unauthorized_client")]
    public void A_key_the_token_endpoint_refuses_is_keyInvalid_whichever_source_was_being_called(string error)
    {
        foreach (var source in new string?[] { null, GoogleErrorMapper.Ga4, GoogleErrorMapper.SearchConsole })
        {
            var info = GoogleErrorMapper.Map(Refused(error), source);

            Assert.Equal(ErrorCodes.KeyInvalid, info.Code);
            Assert.Null(info.Args);
        }
    }

    [Theory]
    [InlineData("temporarily_unavailable")]
    [InlineData("server_error")]
    [InlineData("access_denied")]
    [InlineData("invalid_scope")]
    [InlineData("")]
    [InlineData(null)]
    public void Any_other_token_endpoint_failure_is_unknown_because_the_key_may_be_fine(string? error)
    {
        var info = GoogleErrorMapper.Map(Refused(error, HttpStatusCode.ServiceUnavailable));

        Assert.Equal(ErrorCodes.Unknown, info.Code);
        Assert.Null(info.Args);
    }

    // ---- ReportFetchException -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllCodes))]
    public void A_fetch_exception_with_a_known_code_keeps_it(string code)
    {
        var info = GoogleErrorMapper.Map(new ReportFetchException(code));

        Assert.Equal(code, info.Code);
        Assert.Null(info.Args);
    }

    public static TheoryData<string> AllCodes()
    {
        var data = new TheoryData<string>();
        foreach (var code in ErrorCodes.All) data.Add(code);
        return data;
    }

    [Fact]
    public void A_fetch_exception_with_a_code_nobody_knows_is_unknown_and_the_code_is_not_passed_on()
    {
        var info = GoogleErrorMapper.Map(new ReportFetchException(Marker));

        Assert.Equal(ErrorCodes.Unknown, info.Code);
        Assert.DoesNotContain(Marker, JsonSerializer.Serialize(info));
    }

    // ---- What may be in Args ------------------------------------------------------------------------------------------

    [Fact]
    public void The_API_and_the_source_are_fixed_words_chosen_from_a_whitelist_never_text_taken_from_the_exception()
    {
        var wild = new GoogleApiException(Marker, "m")
        {
            HttpStatusCode = HttpStatusCode.Forbidden,
            Error = new RequestError { Code = 403, Errors = [new SingleError { Reason = "accessNotConfigured" }] },
        };

        Assert.Null(GoogleErrorMapper.Map(wild).Args);
        Assert.Null(GoogleErrorMapper.Map(wild, Marker).Args);
        Assert.Equal(Expect(("api", "analyticsData")), GoogleErrorMapper.Map(wild, "ga4").Args);
    }

    [Fact]
    public void A_source_the_caller_names_wrongly_is_ignored()
    {
        var error = new GoogleApiException("other", "m") { HttpStatusCode = HttpStatusCode.NotFound };

        Assert.Null(GoogleErrorMapper.Map(error, "GA4").Args);
        Assert.Null(GoogleErrorMapper.Map(error, "").Args);
    }

    [Fact]
    public void No_data_names_the_source_and_nothing_else()
    {
        Assert.Equal(ErrorCodes.NoData, GoogleErrorMapper.NoData(GoogleErrorMapper.Ga4).Code);
        Assert.Equal(Expect(("source", "ga4")), GoogleErrorMapper.NoData(GoogleErrorMapper.Ga4).Args);
        Assert.Equal(ErrorCodes.NoData, GoogleErrorMapper.NoData(GoogleErrorMapper.SearchConsole).Code);
        Assert.Equal(Expect(("source", "searchConsole")), GoogleErrorMapper.NoData(GoogleErrorMapper.SearchConsole).Args);
    }

    // ---- No leak ------------------------------------------------------------------------------------------------------

    /// <summary>An exception of every kind the mapper sees, carrying the marker in its message AND in an inner exception.</summary>
    public static TheoryData<string, Func<Task<Exception>>> LeakingExceptions()
    {
        static Exception Inner() => new InvalidOperationException(Marker + " inner");

        var data = new TheoryData<string, Func<Task<Exception>>>
        {
            { "ArgumentException", () => Task.FromResult<Exception>(new ArgumentException(Marker, nameof(Marker), Inner())) },
            { "InvalidOperationException", () => Task.FromResult<Exception>(new InvalidOperationException(Marker, Inner())) },
            { "ArgumentOutOfRangeException", () => Task.FromResult<Exception>(new ArgumentOutOfRangeException(Marker, Inner())) },
            { "OperationCanceledException", () => Task.FromResult<Exception>(new OperationCanceledException(Marker, Inner())) },
            { "TaskCanceledException", () => Task.FromResult<Exception>(new TaskCanceledException(Marker, Inner())) },
            { "HttpRequestException", () => Task.FromResult<Exception>(new HttpRequestException(Marker, Inner())) },
            { "ReportFetchException", () => Task.FromResult<Exception>(new ReportFetchException(ErrorCodes.NotConfigured)) },
            { "TokenResponseException invalid_grant", () => Task.FromResult<Exception>(Refused("invalid_grant")) },
            { "TokenResponseException temporarily_unavailable", () => Task.FromResult<Exception>(Refused("temporarily_unavailable")) },
            { "GoogleApiException (hand made, marker everywhere)", () => Task.FromResult<Exception>(new GoogleApiException(Marker, Marker, Inner())
                {
                    HttpStatusCode = HttpStatusCode.Forbidden,
                    Error = new RequestError { Code = 403, Message = Marker, Errors = [new SingleError { Reason = Marker, Message = Marker, Domain = Marker, Location = Marker }] },
                }) },
            { "GoogleApiException 403 disabled", async () => await Ga4Failure(HttpStatusCode.Forbidden, Disabled()) },
            { "GoogleApiException 403 denied", async () => await SearchConsoleFailure(HttpStatusCode.Forbidden, Denied()) },
            { "GoogleApiException 404", async () => await Ga4Failure(HttpStatusCode.NotFound, Error(404, "NOT_FOUND", "notFound")) },
            { "GoogleApiException 429", async () => await Ga4Failure(HttpStatusCode.TooManyRequests, Error(429, "RESOURCE_EXHAUSTED")) },
            { "GoogleApiException 400", async () => await Ga4Failure(HttpStatusCode.BadRequest, Error(400, "INVALID_ARGUMENT")) },
            { "GoogleApiException 500", async () => await Ga4Failure(HttpStatusCode.InternalServerError, Error(500, "INTERNAL")) },
        };
        return data;
    }

    [Theory]
    [MemberData(nameof(LeakingExceptions))]
    public async Task The_problem_carries_no_text_of_the_exception_or_of_the_one_inside_it(string kind, Func<Task<Exception>> make)
    {
        var error = await make();

        foreach (var info in new[] { GoogleErrorMapper.Map(error), GoogleErrorMapper.Map(error, GoogleErrorMapper.SearchConsole) })
        {
            Assert.DoesNotContain(Marker, JsonSerializer.Serialize(info));
            Assert.DoesNotContain(Marker, info.ToString());
            Assert.DoesNotContain(Marker, info.Code);
            Assert.All(info.Args ?? new Dictionary<string, object?>(), pair =>
            {
                Assert.IsType<string>(pair.Value);
                Assert.DoesNotContain(Marker, pair.Key);
            });
        }

        // The test means what it says: the marker really is in the exception (a fetch exception's message is its code alone).
        Assert.True(error is ReportFetchException || error.Message.Contains(Marker), kind);
    }

    [Fact]
    public void The_problem_serializes_as_a_code_and_primitive_arguments()
    {
        var json = JsonSerializer.Serialize(GoogleErrorMapper.NoData(GoogleErrorMapper.Ga4), JsonSerializerOptions.Web);

        Assert.Equal("""{"code":"noData","args":{"source":"ga4"}}""", json);
    }

    // ---- The vocabulary -----------------------------------------------------------------------------------------------

    [Fact]
    public void The_codes_are_the_fixed_words_the_texts_and_the_client_know()
    {
        Assert.Equal(
            new[] { "notConfigured", "keyUnreadable", "keyInvalid", "apiDisabled", "noAccess", "propertyNotFound", "noData", "quota", "unknown", "ga4PropertyInvalid", "searchConsoleInvalid" },
            ErrorCodes.All);
        Assert.Equal(KeyErrorCodesValues(), ErrorCodes.All.Take(3));
    }

    private static string[] KeyErrorCodesValues() =>
    [
        Esatto.Umbraco.Backoffice.SeoReport.Settings.KeyErrorCodes.NotConfigured,
        Esatto.Umbraco.Backoffice.SeoReport.Settings.KeyErrorCodes.KeyUnreadable,
        Esatto.Umbraco.Backoffice.SeoReport.Settings.KeyErrorCodes.KeyInvalid,
    ];

    [Theory]
    [InlineData("notConfigured", "not_configured")]
    [InlineData("apiDisabled", "api_disabled")]
    [InlineData("noData", "no_data")]
    [InlineData("quota", "quota")]
    [InlineData("ga4PropertyInvalid", "ga4_property_invalid")]
    [InlineData("searchConsoleInvalid", "search_console_invalid")]
    public void A_code_becomes_its_key_id_in_snake_case(string code, string expected)
        => Assert.Equal(expected, ErrorCodes.BaseId(code));

    [Fact]
    public void Every_code_is_registered_as_a_title_and_a_text_and_the_client_can_derive_the_key_from_the_code_alone()
    {
        foreach (var code in ErrorCodes.All)
        {
            // The client builds the key as "error" + the code with its first letter capitalised + the part.
            var capitalised = char.ToUpperInvariant(code[0]) + code[1..];

            Assert.Contains($"error{capitalised}Title", ReportKeys.All);
            Assert.Contains($"error{capitalised}Text", ReportKeys.All);
            Assert.Equal($"error{capitalised}Title", ReportKeys.Lookup("error", ErrorCodes.BaseId(code), "Title"));
        }

        // Google's codes and the API's own (ApiCodes), a title and a text each, and nothing else in area error.
        Assert.Equal((ErrorCodes.All.Count + Api.ApiCodes.All.Count) * 2, ReportKeys.All.Count(key => key.StartsWith("error", StringComparison.Ordinal)));
    }
}
