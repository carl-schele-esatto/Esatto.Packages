using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// Test connection: the key, then GA4, then Search Console, each reported on its own and never as an exception. These tests run the
/// real tester over real Google service objects whose transports are fakes (see <see cref="ConnectionRig"/>), so what is checked is
/// what would go on the wire and how Google's own error handling reaches the result. The run's single-flight and its five seconds
/// are in <c>ConnectionTesterTests.Flight.cs</c>.
/// </summary>
public partial class ConnectionTesterTests
{
    private const string Marker = FakeServiceAccount.LeakMarker;

    private static FakeGoogleTransport.Answer Answer(HttpStatusCode status, string body) => new(status, body);

    private static void AssertArgs(IReadOnlyDictionary<string, object?>? actual, params (string Name, object Value)[] expected)
        => Assert.Equal(
            expected.OrderBy(pair => pair.Name).Select(pair => $"{pair.Name}={pair.Value}"),
            (actual ?? new Dictionary<string, object?>()).OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));

    private static JsonNode Body(FakeGoogleTransport.Call call) => JsonNode.Parse(call.Body)!;

    // ---- Everything works ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task With_the_saved_key_and_both_sources_working_everything_is_ok_and_the_email_is_the_keys()
    {
        var rig = await ConnectionRig.CreateAsync();

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.Equal(FakeServiceAccount.DefaultClientEmail, result.ServiceAccountEmail);
        Assert.Equal(new SourceTest(true, null, null), result.Key);
        Assert.Equal(new SourceTest(true, null, null), result.Ga4);
        Assert.Equal(new SourceTest(true, null, null), result.SearchConsole);
        rig.Factory.DidNotReceiveWithAnyArgs().Create(default!);
        Assert.Equal(0, rig.Typed.Calls);
    }

    [Fact]
    public async Task The_GA4_check_is_a_tiny_report_of_one_metric_for_yesterday_on_the_property()
    {
        var rig = await ConnectionRig.CreateAsync();

        await rig.Tester.TestAsync(ConnectionRig.Request(ga4: " Properties/123456789 "), CancellationToken.None);

        var call = Assert.Single(rig.Saved.Ga4.Calls);
        Assert.EndsWith("/properties/123456789:runReport", call.Uri.AbsolutePath);
        var body = Body(call);
        Assert.Equal(["yesterday"], body["dateRanges"]!.AsArray().Select(range => range!["startDate"]!.GetValue<string>()));
        Assert.Equal(["yesterday"], body["dateRanges"]!.AsArray().Select(range => range!["endDate"]!.GetValue<string>()));
        Assert.Equal(["sessions"], body["metrics"]!.AsArray().Select(metric => metric!["name"]!.GetValue<string>()));
        Assert.Equal("1", body["limit"]!.ToString());
        Assert.Null(body["property"]);
    }

    [Fact]
    public async Task The_Search_Console_check_asks_for_the_site_and_then_sends_the_probe_the_fetch_sends()
    {
        var rig = await ConnectionRig.CreateAsync();

        await rig.Tester.TestAsync(ConnectionRig.Request(site: "EXAMPLE.com/"), CancellationToken.None);

        Assert.Equal(2, rig.Saved.SearchConsole.Calls.Count);
        var site = rig.Saved.SearchConsole.Calls[0];
        Assert.False(ConnectionRig.IsProbe(site));
        Assert.Equal("sc-domain:example.com", Uri.UnescapeDataString(site.Uri.AbsolutePath.Split('/').Last()));
        Assert.Equal("", site.Body);

        var probe = rig.Saved.SearchConsole.Calls[1];
        Assert.True(ConnectionRig.IsProbe(probe));
        Assert.Contains("sc-domain%3Aexample.com", probe.Uri.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("2026-05-31", Body(probe)["startDate"]!.ToString()); // 45 days before the clock's today
        Assert.Equal("2026-07-15", Body(probe)["endDate"]!.ToString());
        Assert.Equal("date", Body(probe)["dimensions"]![0]!.ToString());
        Assert.Equal("final", Body(probe)["dataState"]!.ToString());
    }

    // ---- The key ------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"type\":\"service_account\"}")]
    [InlineData("[]")]
    public async Task A_key_that_is_not_a_service_account_key_is_keyInvalid_and_nothing_else_is_tried(string pasted)
    {
        var rig = await ConnectionRig.CreateAsync();

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(pasted + Marker), CancellationToken.None);

        Assert.Equal(new SourceTest(false, ErrorCodes.KeyInvalid, null), result.Key);
        Assert.Equal(SourceTest.NotTested, result.Ga4);
        Assert.Equal(SourceTest.NotTested, result.SearchConsole);
        Assert.Null(result.ServiceAccountEmail);
        NothingWasCalled(rig);
    }

    [Fact]
    public async Task A_key_Googles_loader_refuses_is_keyInvalid_too()
    {
        var rig = await ConnectionRig.CreateAsync();
        var notRsa = FakeServiceAccount.Json(privateKey: "-----BEGIN PRIVATE KEY-----\nAAAA\n-----END PRIVATE KEY-----\n");

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(notRsa), CancellationToken.None);

        Assert.Equal(ErrorCodes.KeyInvalid, result.Key.ErrorCode);
        Assert.False(result.Key.Ok);
        Assert.Equal(SourceTest.NotTested, result.Ga4);
        Assert.Equal(SourceTest.NotTested, result.SearchConsole);
        Assert.Null(result.ServiceAccountEmail);
        NothingWasCalled(rig);
    }

    [Theory]
    [InlineData("http://127.0.0.1:9/x")]
    [InlineData("https://evil.example/token")]
    [InlineData("http://oauth2.googleapis.com/token")]
    [InlineData("https://oauth2.googleapis.com/token/")]
    public async Task A_pasted_key_whose_token_endpoint_is_not_Googles_is_keyInvalid_and_nothing_is_sent_anywhere(string tokenUri)
    {
        var rig = await ConnectionRig.CreateAsync();
        var key = FakeServiceAccount.Object();
        key["token_uri"] = tokenUri;

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(key.ToJsonString()), CancellationToken.None);

        Assert.Equal(new SourceTest(false, ErrorCodes.KeyInvalid, null), result.Key);
        Assert.Equal(SourceTest.NotTested, result.Ga4);
        Assert.Equal(SourceTest.NotTested, result.SearchConsole);
        Assert.Null(result.ServiceAccountEmail);
        NothingWasCalled(rig);
    }

    [Fact]
    public async Task A_pasted_key_is_tested_with_a_credential_that_asks_Googles_own_token_endpoint()
    {
        var rig = await ConnectionRig.CreateAsync();
        var key = FakeServiceAccount.Object();
        key["universe_domain"] = "internal.example";
        key["auth_uri"] = "http://127.0.0.1:9/auth";

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(key.ToJsonString()), CancellationToken.None);

        Assert.True(result.Key.Ok);
        var account = Assert.IsType<ServiceAccountCredential>(rig.FactoryCredential!.UnderlyingCredential);
        Assert.Equal("https://oauth2.googleapis.com/token", account.TokenServerUrl);
        Assert.Equal("googleapis.com", await rig.FactoryCredential.GetUniverseDomainAsync(CancellationToken.None));
    }

    private static void NothingWasCalled(ConnectionRig rig)
    {
        Assert.Equal(0, rig.Saved.Calls);
        Assert.Equal(0, rig.Typed.Calls);
        Assert.Equal(0, rig.Saved.Ga4ServiceRequests + rig.Typed.Ga4ServiceRequests);
        Assert.Equal(0, rig.Saved.SearchConsole.ServiceRequests + rig.Typed.SearchConsole.ServiceRequests);
        rig.Factory.DidNotReceiveWithAnyArgs().Create(default!);
    }

    [Fact]
    public async Task A_key_that_is_typed_in_is_tested_with_clients_the_factory_builds_and_the_saved_key_is_left_alone()
    {
        var rig = await ConnectionRig.CreateAsync();
        var typedEmail = "typed@proj.iam.gserviceaccount.com";

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(FakeServiceAccount.Json(typedEmail)), CancellationToken.None);

        Assert.Equal(typedEmail, result.ServiceAccountEmail);
        Assert.True(result.Key.Ok);
        Assert.True(result.Ga4.Ok);
        Assert.True(result.SearchConsole.Ok);
        rig.Factory.Received(1).Create(Arg.Any<GoogleCredential>());
        Assert.NotNull(rig.FactoryCredential);
        await rig.Credentials.DidNotReceiveWithAnyArgs().GetAsync(default);
        Assert.Equal(0, rig.Saved.Calls);
        Assert.Single(rig.Typed.Ga4.Calls);
        Assert.Equal(2, rig.Typed.SearchConsole.Calls.Count);
    }

    [Fact]
    public async Task A_pasted_key_with_a_BOM_and_escaped_newlines_is_repaired_before_it_is_used()
    {
        var rig = await ConnectionRig.CreateAsync();
        var escaped = FakeServiceAccount.Object();
        escaped["private_key"] = FakeServiceAccount.PrivateKeyPem.Replace("\n", "\\n", StringComparison.Ordinal);
        var pasted = (char)0xFEFF + "  " + escaped.ToJsonString() + "\r\n";

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(pasted), CancellationToken.None);

        Assert.True(result.Key.Ok);
        Assert.Equal(FakeServiceAccount.DefaultClientEmail, result.ServiceAccountEmail);
    }

    [Theory]
    [InlineData(KeyErrorCodes.NotConfigured)]
    [InlineData(KeyErrorCodes.KeyUnreadable)]
    [InlineData(KeyErrorCodes.KeyInvalid)]
    public async Task With_no_usable_saved_key_the_credential_layers_code_is_the_keys_and_nothing_else_is_tried(string code)
    {
        var rig = await ConnectionRig.CreateAsync(source: new CredentialResult(null, null, KeySourceKind.None, code));

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.Equal(new SourceTest(false, code, null), result.Key);
        Assert.Equal(SourceTest.NotTested, result.Ga4);
        Assert.Equal(SourceTest.NotTested, result.SearchConsole);
        Assert.Null(result.ServiceAccountEmail);
        NothingWasCalled(rig);
    }

    [Fact]
    public async Task A_saved_key_is_not_replaced_by_a_blank_or_white_space_box()
    {
        var rig = await ConnectionRig.CreateAsync();

        var result = await rig.Tester.TestAsync(ConnectionRig.Request("  \r\n "), CancellationToken.None);

        Assert.True(result.Key.Ok);
        await rig.Credentials.Received(1).GetAsync(Arg.Any<CancellationToken>());
        rig.Factory.DidNotReceiveWithAnyArgs().Create(default!);
    }

    [Fact]
    public async Task A_credential_source_that_throws_is_an_unknown_key_problem_and_not_an_exception()
    {
        var rig = await ConnectionRig.CreateAsync();
        rig.Credentials.GetAsync(Arg.Any<CancellationToken>()).Returns<Task<CredentialResult>>(_ => throw new InvalidOperationException(Marker));

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.Equal(new SourceTest(false, ErrorCodes.Unknown, null), result.Key);
        Assert.Equal(SourceTest.NotTested, result.Ga4);
        Assert.DoesNotContain(Marker, Everything(result));
    }

    // ---- The two sources are independent ------------------------------------------------------------------------------

    [Fact]
    public async Task GA4_refusing_the_service_account_is_noAccess_and_Search_Console_is_still_tested()
    {
        var rig = await ConnectionRig.CreateAsync(ga4: _ => GoogleErrorBodies.DeniedAnswer());

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.False(result.Ga4.Ok);
        Assert.Equal(ErrorCodes.NoAccess, result.Ga4.ErrorCode);
        AssertArgs(result.Ga4.Args, ("source", "ga4"));
        Assert.True(result.SearchConsole.Ok);
        Assert.Null(result.SearchConsole.ErrorCode);
        Assert.Equal(2, rig.Saved.SearchConsole.Calls.Count);
        Assert.True(result.Key.Ok);
    }

    [Fact]
    public async Task Search_Console_refusing_the_service_account_leaves_the_GA4_result_alone()
    {
        var rig = await ConnectionRig.CreateAsync(searchConsole: call => ConnectionRig.IsProbe(call) ? GoogleErrorBodies.DeniedAnswer() : ConnectionRig.SearchConsoleOk(call));

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.True(result.Ga4.Ok);
        Assert.False(result.SearchConsole.Ok);
        Assert.Equal(ErrorCodes.NoAccess, result.SearchConsole.ErrorCode);
        AssertArgs(result.SearchConsole.Args, ("source", "searchConsole"));
        Assert.Single(rig.Saved.Ga4.Calls);
    }

    [Fact]
    public async Task A_switched_off_API_is_apiDisabled_and_names_which_one()
    {
        var ga4Off = await ConnectionRig.CreateAsync(ga4: _ => GoogleErrorBodies.DisabledAnswer());
        var searchConsoleOff = await ConnectionRig.CreateAsync(searchConsole: _ => GoogleErrorBodies.DisabledAnswer());

        var first = await ga4Off.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);
        var second = await searchConsoleOff.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.Equal(ErrorCodes.ApiDisabled, first.Ga4.ErrorCode);
        AssertArgs(first.Ga4.Args, ("api", "analyticsData"));
        Assert.True(first.SearchConsole.Ok);
        Assert.Equal(ErrorCodes.ApiDisabled, second.SearchConsole.ErrorCode);
        AssertArgs(second.SearchConsole.Args, ("api", "searchConsole"));
        Assert.True(second.Ga4.Ok);
    }

    [Fact]
    public async Task Any_other_failure_of_one_source_is_unknown_for_that_source_only()
    {
        var rig = await ConnectionRig.CreateAsync(ga4: _ => Answer(HttpStatusCode.BadRequest, GoogleErrorBodies.Error(400, "INVALID_ARGUMENT")));

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.Equal(new SourceTest(false, ErrorCodes.Unknown, null), result.Ga4);
        Assert.True(result.SearchConsole.Ok);
    }

    // ---- Connected, but empty -----------------------------------------------------------------------------------------

    [Fact]
    public async Task A_Search_Console_probe_with_no_rows_for_a_site_that_exists_is_ok_with_noData_and_not_a_failure()
    {
        var rig = await ConnectionRig.CreateAsync(searchConsole: call => ConnectionRig.IsProbe(call) ? Answer(HttpStatusCode.OK, "{}") : ConnectionRig.SearchConsoleOk(call));

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.True(result.SearchConsole.Ok);
        Assert.Equal(ErrorCodes.NoData, result.SearchConsole.ErrorCode);
        AssertArgs(result.SearchConsole.Args, ("source", "searchConsole"));
        Assert.True(result.Ga4.Ok);
        Assert.Null(result.Ga4.ErrorCode);
    }

    [Fact]
    public async Task A_GA4_report_with_no_rows_is_ok_with_noData_too()
    {
        var rig = await ConnectionRig.CreateAsync(ga4: _ => Answer(HttpStatusCode.OK, "{}"));

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.True(result.Ga4.Ok);
        Assert.Equal(ErrorCodes.NoData, result.Ga4.ErrorCode);
        AssertArgs(result.Ga4.Args, ("source", "ga4"));
        Assert.True(result.SearchConsole.Ok);
        Assert.Null(result.SearchConsole.ErrorCode);
    }

    // ---- A property that is not there is never a silent success -------------------------------------------------------

    [Fact]
    public async Task A_site_Search_Console_does_not_know_is_propertyNotFound_and_the_probe_is_never_sent()
    {
        var rig = await ConnectionRig.CreateAsync(searchConsole: call => ConnectionRig.IsProbe(call) ? ConnectionRig.SearchConsoleOk(call) : GoogleErrorBodies.NotFoundAnswer());

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.False(result.SearchConsole.Ok);
        Assert.Equal(ErrorCodes.PropertyNotFound, result.SearchConsole.ErrorCode);
        AssertArgs(result.SearchConsole.Args, ("source", "searchConsole"));
        Assert.Single(rig.Saved.SearchConsole.Calls);
    }

    [Fact]
    public async Task A_403_from_sites_get_for_the_wrong_form_of_a_property_is_propertyNotFound_and_the_probe_is_not_sent()
    {
        // Search Console answers 403, not 404, for a property the account does not hold. The account holds the URL-prefix property
        // only; the person typed the bare domain, which becomes sc-domain:example.com.
        var rig = await ConnectionRig.CreateAsync(searchConsole: call =>
            call.Uri.AbsolutePath.Contains("sc-domain", StringComparison.OrdinalIgnoreCase)
                ? Answer(HttpStatusCode.Forbidden, GoogleErrorBodies.Error(403, "PERMISSION_DENIED"))
                : ConnectionRig.SearchConsoleOk(call));

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(site: "example.com"), CancellationToken.None);

        Assert.False(result.SearchConsole.Ok);
        Assert.Equal(ErrorCodes.PropertyNotFound, result.SearchConsole.ErrorCode);
        AssertArgs(result.SearchConsole.Args, ("source", "searchConsole"));
        Assert.Single(rig.Saved.SearchConsole.Calls);
    }

    [Fact]
    public async Task A_sites_get_403_for_a_switched_off_API_or_a_limit_is_still_apiDisabled_or_quota_and_not_a_property_problem()
    {
        var off = await ConnectionRig.CreateAsync(searchConsole: _ => GoogleErrorBodies.DisabledAnswer());
        var limit = await ConnectionRig.CreateAsync(searchConsole: _ => Answer(HttpStatusCode.Forbidden, GoogleErrorBodies.Error(403, "PERMISSION_DENIED", "rateLimitExceeded")));

        var first = await off.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);
        var second = await limit.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.Equal(ErrorCodes.ApiDisabled, first.SearchConsole.ErrorCode);
        AssertArgs(first.SearchConsole.Args, ("api", "searchConsole"));
        Assert.Equal(ErrorCodes.Quota, second.SearchConsole.ErrorCode);
    }

    [Fact]
    public async Task A_sites_get_401_is_no_access_because_it_says_nothing_about_the_property()
    {
        var rig = await ConnectionRig.CreateAsync(searchConsole: _ => Answer(HttpStatusCode.Unauthorized, GoogleErrorBodies.Error(401, "UNAUTHENTICATED")));

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.Equal(ErrorCodes.NoAccess, result.SearchConsole.ErrorCode);
    }

    [Fact]
    public async Task A_site_that_is_known_but_refuses_the_probe_is_a_failure_too()
    {
        var rig = await ConnectionRig.CreateAsync(searchConsole: call => ConnectionRig.IsProbe(call) ? GoogleErrorBodies.DeniedAnswer() : ConnectionRig.SearchConsoleOk(call));

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.False(result.SearchConsole.Ok);
        Assert.Equal(ErrorCodes.NoAccess, result.SearchConsole.ErrorCode);
        Assert.Equal(2, rig.Saved.SearchConsole.Calls.Count);
    }

    [Fact]
    public async Task A_GA4_property_Google_does_not_know_is_propertyNotFound()
    {
        var rig = await ConnectionRig.CreateAsync(ga4: _ => GoogleErrorBodies.NotFoundAnswer());

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.False(result.Ga4.Ok);
        Assert.Equal(ErrorCodes.PropertyNotFound, result.Ga4.ErrorCode);
        AssertArgs(result.Ga4.Args, ("source", "ga4"));
    }

    // ---- The two identifiers ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("abc")]
    [InlineData("G-ABC123")]
    [InlineData("properties/")]
    [InlineData("")]
    [InlineData(null)]
    public async Task A_GA4_id_that_is_not_valid_is_ga4PropertyInvalid_and_GA4_is_not_tried_but_Search_Console_is(string? id)
    {
        var rig = await ConnectionRig.CreateAsync();

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(ga4: id), CancellationToken.None);

        Assert.Equal(new SourceTest(false, ErrorCodes.Ga4PropertyInvalid, null), result.Ga4);
        Assert.Empty(rig.Saved.Ga4.Calls);
        Assert.Equal(0, rig.Saved.Ga4ServiceRequests);
        Assert.True(result.SearchConsole.Ok);
        Assert.True(result.Key.Ok);
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("has spaces.com")]
    [InlineData("")]
    [InlineData(null)]
    public async Task A_site_that_is_not_valid_is_searchConsoleInvalid_and_Search_Console_is_not_tried_but_GA4_is(string? site)
    {
        var rig = await ConnectionRig.CreateAsync();

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(site: site), CancellationToken.None);

        Assert.Equal(new SourceTest(false, ErrorCodes.SearchConsoleInvalid, null), result.SearchConsole);
        Assert.Empty(rig.Saved.SearchConsole.Calls);
        Assert.Equal(0, rig.Saved.SearchConsole.ServiceRequests);
        Assert.True(result.Ga4.Ok);
    }

    [Fact]
    public async Task An_invalid_identifier_is_reported_whatever_state_the_key_is_in_because_it_is_checked_without_google()
    {
        var rig = await ConnectionRig.CreateAsync();

        var result = await rig.Tester.TestAsync(ConnectionRig.Request("not a key", ga4: "abc", site: null), CancellationToken.None);

        Assert.Equal(ErrorCodes.KeyInvalid, result.Key.ErrorCode);
        Assert.Equal(ErrorCodes.Ga4PropertyInvalid, result.Ga4.ErrorCode);
        Assert.Equal(ErrorCodes.SearchConsoleInvalid, result.SearchConsole.ErrorCode);
        NothingWasCalled(rig);
    }

    // ---- A key Google refuses -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("invalid_grant")]
    [InlineData("invalid_client")]
    [InlineData("unauthorized_client")]
    public async Task A_key_that_parses_but_the_token_endpoint_refuses_is_keyInvalid_on_the_key_and_the_sources_are_not_tested(string error)
    {
        var rig = await ConnectionRig.CreateAsync(ga4: _ => throw GoogleErrorMapperTests.Refused(error), searchConsole: _ => throw GoogleErrorMapperTests.Refused(error));

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.Equal(new SourceTest(false, ErrorCodes.KeyInvalid, null), result.Key);
        Assert.Equal(SourceTest.NotTested, result.Ga4);
        Assert.Equal(SourceTest.NotTested, result.SearchConsole);
        Assert.Equal(FakeServiceAccount.DefaultClientEmail, result.ServiceAccountEmail);
        Assert.DoesNotContain(Marker, Everything(result));
        Assert.DoesNotContain(Marker, rig.Log.AllText);
    }

    [Fact]
    public async Task A_pasted_key_the_token_endpoint_refuses_is_keyInvalid_with_the_email_still_shown()
    {
        var rig = await ConnectionRig.CreateAsync(ga4: _ => throw GoogleErrorMapperTests.Refused("invalid_grant"), searchConsole: _ => throw GoogleErrorMapperTests.Refused("invalid_grant"));

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(FakeServiceAccount.Json("typed@proj.iam.gserviceaccount.com")), CancellationToken.None);

        Assert.Equal(ErrorCodes.KeyInvalid, result.Key.ErrorCode);
        Assert.False(result.Key.Ok);
        Assert.Equal("typed@proj.iam.gserviceaccount.com", result.ServiceAccountEmail);
        Assert.Equal(SourceTest.NotTested, result.Ga4);
        Assert.Equal(SourceTest.NotTested, result.SearchConsole);
    }

    [Fact]
    public async Task One_source_that_reports_the_key_refused_is_enough_to_blame_the_key()
    {
        var rig = await ConnectionRig.CreateAsync(ga4: _ => throw GoogleErrorMapperTests.Refused("invalid_grant"));

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.Equal(ErrorCodes.KeyInvalid, result.Key.ErrorCode);
        Assert.Equal(SourceTest.NotTested, result.Ga4);
        Assert.Equal(SourceTest.NotTested, result.SearchConsole);
    }

    [Fact]
    public async Task An_identifier_that_cannot_be_written_is_still_reported_when_the_key_is_refused()
    {
        var rig = await ConnectionRig.CreateAsync(searchConsole: _ => throw GoogleErrorMapperTests.Refused("invalid_grant"));

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(ga4: "abc"), CancellationToken.None);

        Assert.Equal(ErrorCodes.KeyInvalid, result.Key.ErrorCode);
        Assert.Equal(ErrorCodes.Ga4PropertyInvalid, result.Ga4.ErrorCode);
        Assert.Equal(SourceTest.NotTested, result.SearchConsole);
    }

    [Fact]
    public async Task A_token_endpoint_that_is_only_unavailable_does_not_blame_the_key()
    {
        var rig = await ConnectionRig.CreateAsync(
            ga4: _ => throw GoogleErrorMapperTests.Refused("temporarily_unavailable", HttpStatusCode.ServiceUnavailable),
            searchConsole: _ => throw GoogleErrorMapperTests.Refused("server_error", HttpStatusCode.ServiceUnavailable));

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.True(result.Key.Ok);
        Assert.Equal(new SourceTest(false, ErrorCodes.Unknown, null), result.Ga4);
        Assert.Equal(new SourceTest(false, ErrorCodes.Unknown, null), result.SearchConsole);
    }

    [Fact]
    public async Task A_result_whose_key_is_not_ok_is_never_given_again()
    {
        var rig = await ConnectionRig.CreateAsync();
        var pasted = "not a key";

        var first = await rig.Tester.TestAsync(ConnectionRig.Request(pasted), CancellationToken.None);
        var second = await rig.Tester.TestAsync(ConnectionRig.Request(pasted), CancellationToken.None);

        Assert.Equal(ErrorCodes.KeyInvalid, second.Key.ErrorCode);
        Assert.NotSame(first, second);
    }

    // ---- What is logged -----------------------------------------------------------------------------------------------

    private const string PlainBadRequest = """{"error":{"code":400,"message":"bad request","status":"INVALID_ARGUMENT"}}""";

    [Fact]
    public async Task A_failure_with_no_code_is_logged_as_a_warning_naming_the_source_the_exception_type_and_the_status()
    {
        var rig = await ConnectionRig.CreateAsync(ga4: _ => Answer(HttpStatusCode.BadRequest, PlainBadRequest));

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.Equal(ErrorCodes.Unknown, result.Ga4.ErrorCode);
        var warning = Assert.Single(rig.Log.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains(GoogleErrorMapper.Ga4, warning.Text);
        Assert.Contains("GoogleApiException", warning.Text);
        Assert.Contains("400", warning.Text);
    }

    [Fact]
    public async Task An_exception_that_is_not_Googles_is_logged_with_its_type()
    {
        var rig = await ConnectionRig.CreateAsync();
        rig.Saved.ServiceFailure = new InvalidOperationException("made up");

        await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.Contains(rig.Log.Entries, entry => entry.Level == LogLevel.Warning && entry.Text.Contains(GoogleErrorMapper.Ga4) && entry.Text.Contains("InvalidOperationException"));
        Assert.Contains(rig.Log.Entries, entry => entry.Level == LogLevel.Warning && entry.Text.Contains(GoogleErrorMapper.SearchConsole) && entry.Text.Contains("InvalidOperationException"));
    }

    [Fact]
    public async Task A_refusal_that_has_a_code_is_not_a_warning_but_is_still_logged_for_whoever_looks()
    {
        var rig = await ConnectionRig.CreateAsync(ga4: _ => GoogleErrorBodies.DeniedAnswer());

        await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.DoesNotContain(rig.Log.Entries, entry => entry.Level >= LogLevel.Warning);
        var debug = Assert.Single(rig.Log.Entries, entry => entry.Level == LogLevel.Debug);
        Assert.Contains(GoogleErrorMapper.Ga4, debug.Text);
        Assert.Contains(ErrorCodes.NoAccess, debug.Text);
        Assert.Contains("GoogleApiException", debug.Text);
        Assert.Contains("403", debug.Text);
    }

    [Fact]
    public async Task A_successful_run_logs_nothing_above_debug()
    {
        var rig = await ConnectionRig.CreateAsync();

        await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.DoesNotContain(rig.Log.Entries, entry => entry.Level > LogLevel.Debug);
    }

    [Fact]
    public async Task A_credential_layer_that_throws_is_logged_by_exception_type_only()
    {
        var rig = await ConnectionRig.CreateAsync();
        rig.Credentials.GetAsync(Arg.Any<CancellationToken>()).Returns<Task<CredentialResult>>(_ => throw new InvalidOperationException(Marker));

        await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        var warning = Assert.Single(rig.Log.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains("InvalidOperationException", warning.Text);
        Assert.DoesNotContain(Marker, rig.Log.AllText);
    }

    [Fact]
    public async Task The_log_never_holds_the_key_whatever_failed()
    {
        var rig = await ConnectionRig.CreateAsync(
            ga4: _ => Answer(HttpStatusCode.BadRequest, PlainBadRequest),
            searchConsole: _ => Answer(HttpStatusCode.InternalServerError, PlainBadRequest));
        var pemBody = FakeServiceAccount.PrivateKeyPem.Split('\n')[1];

        await rig.Tester.TestAsync(ConnectionRig.Request(KeyWithMarkers()), CancellationToken.None);
        await rig.Tester.TestAsync(ConnectionRig.Request("{ not a key " + Marker), CancellationToken.None);

        Assert.NotEmpty(rig.Log.Entries);
        Assert.DoesNotContain(Marker, rig.Log.AllText);
        Assert.DoesNotContain("PRIVATE KEY", rig.Log.AllText);
        Assert.DoesNotContain(pemBody, rig.Log.AllText);
        Assert.DoesNotContain("private_key", rig.Log.AllText);
    }

    // ---- What the result may contain ----------------------------------------------------------------------------------

    private static string Everything(ConnectionTestResult result)
        => string.Join("\n", result.ToString(), result.Key, result.Ga4, result.SearchConsole, result.ServiceAccountEmail, JsonSerializer.Serialize(result, JsonSerializerOptions.Web));

    private static string KeyWithMarkers()
    {
        var key = FakeServiceAccount.Object();
        key["private_key_id"] = Marker;
        key["project_id"] = Marker;
        return key.ToJsonString();
    }

    [Fact]
    public async Task The_result_never_contains_the_key_in_any_property_or_in_its_text_or_JSON()
    {
        var rig = await ConnectionRig.CreateAsync();
        var pemBody = FakeServiceAccount.PrivateKeyPem.Split('\n')[1];

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(KeyWithMarkers()), CancellationToken.None);

        Assert.True(result.Key.Ok);
        var text = Everything(result);
        Assert.DoesNotContain(Marker, text);
        Assert.DoesNotContain("PRIVATE KEY", text);
        Assert.DoesNotContain(pemBody, text);
        Assert.Contains(FakeServiceAccount.DefaultClientEmail, text);
    }

    [Fact]
    public async Task A_failure_that_quotes_the_key_or_the_response_reaches_the_result_as_a_code_only()
    {
        var rig = await ConnectionRig.CreateAsync();
        rig.Typed.ServiceFailure = new InvalidOperationException(Marker + " " + FakeServiceAccount.PrivateKeyPem, new ArgumentException(Marker));

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(KeyWithMarkers()), CancellationToken.None);

        Assert.Equal(ErrorCodes.Unknown, result.Ga4.ErrorCode);
        Assert.Equal(ErrorCodes.Unknown, result.SearchConsole.ErrorCode);
        Assert.DoesNotContain(Marker, Everything(result));
        Assert.DoesNotContain("PRIVATE KEY", Everything(result));
    }

    [Fact]
    public async Task A_response_that_quotes_a_secret_in_its_message_does_not_reach_the_result()
    {
        var rig = await ConnectionRig.CreateAsync(
            ga4: _ => Answer(HttpStatusCode.Forbidden, GoogleErrorBodies.Body(403, "PERMISSION_DENIED")),
            searchConsole: _ => Answer(HttpStatusCode.InternalServerError, GoogleErrorBodies.Body(500, "INTERNAL")));

        var result = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.Equal(ErrorCodes.NoAccess, result.Ga4.ErrorCode);
        Assert.DoesNotContain(Marker, Everything(result));
    }

    [Fact]
    public async Task Every_code_in_a_result_is_one_the_texts_know()
    {
        var failing = await ConnectionRig.CreateAsync(
            ga4: _ => GoogleErrorBodies.DisabledAnswer(),
            searchConsole: _ => GoogleErrorBodies.NotFoundAnswer());

        var result = await failing.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.All(
            new[] { result.Key, result.Ga4, result.SearchConsole }.Select(test => test.ErrorCode).OfType<string>(),
            code => Assert.Contains(code, ErrorCodes.All));
    }

    // ---- The request --------------------------------------------------------------------------------------------------

    [Fact]
    public void The_request_prints_without_the_key_and_without_the_identifiers()
    {
        var request = ConnectionRig.Request(KeyWithMarkers(), ga4: "123456789", site: "sc-domain:example.com");

        var text = request.ToString();

        Assert.DoesNotContain(Marker, text);
        Assert.DoesNotContain("PRIVATE KEY", text);
        Assert.DoesNotContain("123456789", text);
        Assert.DoesNotContain("example.com", text);
        Assert.Contains("ConnectionTestRequest", text);
    }

    [Fact]
    public void The_request_cannot_echo_the_key_when_it_is_serialized_by_accident_but_still_reads_it_from_a_body()
    {
        var request = ConnectionRig.Request(KeyWithMarkers());

        foreach (var options in new[] { JsonSerializerOptions.Default, JsonSerializerOptions.Web })
        {
            var json = JsonSerializer.Serialize(request, options);

            Assert.DoesNotContain(Marker, json);
            Assert.DoesNotContain("PRIVATE KEY", json);
            Assert.Contains("123456789", json);
        }

        var body = JsonSerializer.Serialize(new { ga4PropertyId = "123456789", searchConsoleSite = "example.com", serviceAccountJson = request.ServiceAccountJson }, JsonSerializerOptions.Web);
        var read = JsonSerializer.Deserialize<ConnectionTestRequest>(body, JsonSerializerOptions.Web)!;

        Assert.Equal(request.ServiceAccountJson, read.ServiceAccountJson);
        Assert.Equal("123456789", read.Ga4PropertyId);
        Assert.Equal("example.com", read.SearchConsoleSite);
    }

    [Fact]
    public void A_source_test_that_was_not_made_is_neither_ok_nor_a_failure()
    {
        Assert.False(SourceTest.NotTested.Ok);
        Assert.Null(SourceTest.NotTested.ErrorCode);
        Assert.Null(SourceTest.NotTested.Args);
    }

    [Fact]
    public async Task A_missing_request_is_refused()
    {
        var rig = await ConnectionRig.CreateAsync();

        await Assert.ThrowsAsync<ArgumentNullException>(() => rig.Tester.TestAsync(null!, CancellationToken.None));
    }
}
