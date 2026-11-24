using System.Text.Json;
using Esatto.Umbraco.Backoffice.SeoReport.Api;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Jobs;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The JSON the dashboard receives, pinned with the Management API's real options (see <see cref="ManagementJson"/>): the property
/// names the client reads, the enums as camelCase words, and the null values inside a problem's or a text's <c>args</c>, which the
/// client's texts rely on ("an argument that is there and null" is not "no argument").
/// </summary>
public class ResponseJsonTests
{
    private static string[] Names(JsonElement element) => element.EnumerateObject().Select(p => p.Name).ToArray();

    private static StatusResponse Status(SetupState setup = SetupState.Ready) => new(
        setup, ["key"], new FetchStatusResponse(true, 1, 13, "2026-06", null, ErrorCodes.NoAccess, null, null), false, ["2026-05", "2026-06"], "2026-07", "Example");

    [Fact]
    public void The_host_writes_an_enum_by_its_C_sharp_name_which_is_why_the_responses_choose_their_words_themselves()
    {
        // The finding behind the explicit converters: Umbraco adds JsonStringEnumConverter() with no naming policy to the options.
        Assert.Equal("\"Ready\"", JsonSerializer.Serialize(SetupState.Ready, ManagementJson.Options));
        Assert.Equal("\"Database\"", JsonSerializer.Serialize(KeySourceKind.Database, ManagementJson.Options));
    }

    [Theory]
    [InlineData(SetupState.NotConfigured, "notConfigured")]
    [InlineData(SetupState.KeyUnreadable, "keyUnreadable")]
    [InlineData(SetupState.Ready, "ready")]
    public void Status_names_the_setup_state_as_a_camelCase_word(SetupState setup, string word)
    {
        using var json = ManagementJson.Parse(Status(setup));

        Assert.Equal(word, json.RootElement.GetProperty("setup").GetString());
    }

    [Fact]
    public void Status_has_exactly_the_properties_the_client_reads()
    {
        using var json = ManagementJson.Parse(Status());
        var root = json.RootElement;

        Assert.Equal(["setup", "missing", "fetch", "upToDate", "periods", "expectedLatestPeriod", "displayName"], Names(root));
        Assert.Equal(["running", "done", "total", "currentPeriod", "errorCode", "lastErrorCode", "lastSuccessUtc", "lastFailureUtc"], Names(root.GetProperty("fetch")));
        Assert.Equal(["2026-05", "2026-06"], root.GetProperty("periods").EnumerateArray().Select(p => p.GetString()!).ToArray());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("fetch").GetProperty("lastSuccessUtc").ValueKind); // a null is written, not left out
    }

    [Fact]
    public void A_report_response_keeps_the_reports_own_snake_case_names_and_names_its_envelope_in_camelCase()
    {
        var fetched = new DateTimeOffset(2026, 8, 3, 6, 30, 0, TimeSpan.Zero);

        using var json = ManagementJson.Parse(new ReportResponse(TestReports.With("2026-07"), "A note.", fetched, "2026-07"));
        var root = json.RootElement;

        Assert.Equal(["report", "note", "fetchedUtc", "period"], Names(root));
        Assert.Equal("2026-07", root.GetProperty("report").GetProperty("report_period").GetString());
        Assert.Equal("1.0.0", root.GetProperty("report").GetProperty("schema_version").GetString());
        Assert.Equal("A note.", root.GetProperty("note").GetString());
        Assert.Equal(fetched, root.GetProperty("fetchedUtc").GetDateTimeOffset());
    }

    [Fact]
    public void An_empty_report_response_is_four_nulls()
    {
        using var json = ManagementJson.Parse(new ReportResponse(null, null, null, null));

        Assert.All(json.RootElement.EnumerateObject(), p => Assert.Equal(JsonValueKind.Null, p.Value.ValueKind));
        Assert.Equal(4, json.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public void The_settings_response_names_the_key_source_as_a_camelCase_word_and_has_no_key()
    {
        var settings = new SeoReportSettings { Ga4PropertyId = "properties/1", BrandTerms = ["acme"], FindingsUserGroups = ["seo"] };

        using var json = ManagementJson.Parse(new SettingsResponse(settings, ["displayName"], new KeyStatus(true, KeySourceKind.Configuration, "svc@proj.iam.gserviceaccount.com", false)));
        var root = json.RootElement;

        Assert.Equal(["settings", "locked", "key"], Names(root));
        Assert.Equal(["ga4PropertyId", "searchConsoleSite", "brandTerms", "displayName", "findingsUserGroups", "autoFetch"], Names(root.GetProperty("settings")));
        Assert.Equal(["hasKey", "source", "serviceAccountEmail", "unreadable"], Names(root.GetProperty("key")));
        Assert.Equal("configuration", root.GetProperty("key").GetProperty("source").GetString());
        Assert.Equal(["displayName"], root.GetProperty("locked").EnumerateArray().Select(p => p.GetString()!).ToArray());
    }

    [Theory]
    [InlineData(KeySourceKind.None, "none")]
    [InlineData(KeySourceKind.Configuration, "configuration")]
    [InlineData(KeySourceKind.Database, "database")]
    public void Every_key_source_is_a_camelCase_word(KeySourceKind source, string word)
    {
        using var json = ManagementJson.Parse(new SettingsResponse(new SeoReportSettings(), [], new KeyStatus(false, source, null, false)));

        Assert.Equal(word, json.RootElement.GetProperty("key").GetProperty("source").GetString());
    }

    [Fact]
    public void Test_connection_writes_its_args_with_the_null_values_in_them()
    {
        var args = new Dictionary<string, object?> { ["source"] = "ga4", ["email"] = null };
        var result = new ConnectionTestResult("svc@proj.iam.gserviceaccount.com", SourceTest.Passed, new SourceTest(false, ErrorCodes.NoAccess, args), SourceTest.NotTested);

        using var json = ManagementJson.Parse(result);
        var root = json.RootElement;

        Assert.Equal(["serviceAccountEmail", "key", "ga4", "searchConsole"], Names(root));
        Assert.Equal(["ok", "errorCode", "args"], Names(root.GetProperty("ga4")));
        var written = root.GetProperty("ga4").GetProperty("args");
        Assert.Equal("ga4", written.GetProperty("source").GetString());
        Assert.Equal(JsonValueKind.Null, written.GetProperty("email").ValueKind); // there, and null: the texts tell that from no argument
    }

    [Fact]
    public void A_problem_has_the_code_the_args_with_their_null_values_and_the_trace_id_by_those_names()
    {
        var http = new DefaultHttpContext();
        var args = new Dictionary<string, object?> { ["source"] = "searchConsole", ["email"] = null };
        var problem = ApiProblems.Create(http, 502, ErrorCodes.NoAccess, args);

        using var json = ManagementJson.Parse(problem);
        var root = json.RootElement;

        Assert.Equal(ErrorCodes.NoAccess, root.GetProperty("code").GetString());
        Assert.Equal(http.TraceIdentifier, root.GetProperty("traceId").GetString());
        Assert.Equal(502, root.GetProperty("status").GetInt32());
        Assert.Equal("searchConsole", root.GetProperty("args").GetProperty("source").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("args").GetProperty("email").ValueKind);
    }

    [Fact]
    public void A_problem_without_args_has_none()
    {
        using var json = ManagementJson.Parse(ApiProblems.Create(new DefaultHttpContext(), 409, ApiCodes.AlreadyRunning));

        Assert.False(json.RootElement.TryGetProperty("args", out _));
    }

    [Fact]
    public void Only_primitive_args_go_into_a_problem()
    {
        var args = new Dictionary<string, object?> { ["source"] = "ga4", ["count"] = 3, ["flag"] = true, ["nested"] = new { secret = "x" }, ["list"] = new[] { "a" } };

        var problem = ApiProblems.Create(new DefaultHttpContext(), 502, ErrorCodes.Unknown, args);

        Assert.Equal(["source", "count", "flag"], ((IReadOnlyDictionary<string, object?>)problem.Extensions["args"]!).Keys.ToArray());
    }

    [Fact]
    public void The_fetch_answers_the_notes_the_targets_and_the_groups_are_camelCase_too()
    {
        var fetch = new FetchStatusResponse(false, 0, 0, null, null, null, null, null);

        Assert.Equal(["result", "progress"], Names(ManagementJson.Parse(new StartFetchResponse("started", fetch)).RootElement));
        Assert.Equal(["note"], Names(ManagementJson.Parse(new NoteResponse("x")).RootElement));
        Assert.Equal(["terms"], Names(ManagementJson.Parse(new TargetsResponse(["a"])).RootElement));
        Assert.Equal(["alias", "name"], Names(ManagementJson.Parse(new UserGroupItem("seo", "SEO")).RootElement));
    }

    // ---- what the browser sends -----------------------------------------------------------------------------------------

    [Fact]
    public void The_requests_are_read_from_the_camelCase_JSON_the_client_sends()
    {
        var settings = JsonSerializer.Deserialize<PutSettingsRequest>(
            """{"settings":{"ga4PropertyId":"123","searchConsoleSite":"example.com","brandTerms":["acme"],"displayName":"X","findingsUserGroups":["seo"],"autoFetch":false},"serviceAccountJson":"{}","removeKey":true}""",
            ManagementJson.Options)!;
        var test = JsonSerializer.Deserialize<ConnectionTestRequest>("""{"ga4PropertyId":"123","searchConsoleSite":"example.com","serviceAccountJson":"{}"}""", ManagementJson.Options)!;
        var note = JsonSerializer.Deserialize<NoteRequest>("""{"text":"hello"}""", ManagementJson.Options)!;
        var targets = JsonSerializer.Deserialize<TargetsRequest>("""{"terms":["a","b"]}""", ManagementJson.Options)!;

        Assert.Equal("123", settings.Settings.Ga4PropertyId);
        Assert.Equal(["acme"], settings.Settings.BrandTerms);
        Assert.False(settings.Settings.AutoFetch);
        Assert.Equal("{}", settings.ServiceAccountJson);
        Assert.True(settings.RemoveKey);
        Assert.Equal("{}", test.ServiceAccountJson);
        Assert.Equal("hello", note.Text);
        Assert.Equal(["a", "b"], targets.Terms!);
    }

    [Fact]
    public void A_request_that_is_serialized_never_writes_the_key()
    {
        var put = ManagementJson.Serialize(new PutSettingsRequest(new SeoReportSettings(), "SECRET", false));
        var test = ManagementJson.Serialize(new ConnectionTestRequest("1", "x", "SECRET"));

        Assert.DoesNotContain("SECRET", put);
        Assert.DoesNotContain("SECRET", test);
    }
}
