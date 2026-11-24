using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Esatto.Umbraco.Backoffice.SeoReport.Api;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Esatto.Umbraco.Backoffice.SeoReport.Jobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using static Esatto.Umbraco.Backoffice.SeoReport.Tests.Support.ApiRig;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// A problem has to REACH THE CLIENT WITH ITS CODE. The backoffice's own HTTP interceptor (Umbraco's <c>addErrorInterceptor</c>) keeps the
/// body of an error answer only when it looks like ProblemDetails, and otherwise replaces it with a generic problem of its own, in which
/// our <c>code</c>, <c>args</c> and <c>traceId</c> are gone (every refusal would then read "unknown"). What "looks like" means is
/// <c>isProblemDetailsLike</c> in <c>node_modules/@umbraco-cms/backoffice/dist-cms/packages/core/resources/apiTypeValidators.function.js</c>,
/// copied in <see cref="LooksLikeProblemDetails"/>: the members <c>type</c>, <c>title</c> and <c>status</c> are present, and <c>detail</c>,
/// <c>instance</c> and <c>operationStatus</c> are absent or text.
///
/// <c>Type</c> is the one that is easy to lose: ASP.NET's <c>ProblemDetails</c> leaves it null, a null is not written, and nothing in
/// a unit test fills it in (the framework only does so while it formats the response). So <see cref="ApiProblems.Create"/> sets it itself.
/// <c>Client/src/testing/problem-bodies.json</c> is the shared truth: these tests hold it to what the server writes, and the client's
/// test (api.interceptor.test.ts) runs each of its bodies through Umbraco's real interceptor.
/// </summary>
public class ProblemShapeTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    /// <summary>
    /// Umbraco's <c>isProblemDetailsLike</c> (apiTypeValidators.function.js), line for line: <c>'type' in error</c> (the member is there, even
    /// if null), likewise <c>title</c> and <c>status</c>; <c>detail</c>, <c>instance</c>, <c>operationStatus</c> undefined or a string;
    /// <c>errors</c> undefined or an object (a null is an object in JavaScript).
    /// </summary>
    private static bool LooksLikeProblemDetails(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object) return false;
        if (!body.TryGetProperty("type", out _) || !body.TryGetProperty("title", out _) || !body.TryGetProperty("status", out _)) return false;

        foreach (var text in new[] { "detail", "instance", "operationStatus" })
        {
            if (body.TryGetProperty(text, out var value) && value.ValueKind != JsonValueKind.String) return false;
        }

        return !body.TryGetProperty("errors", out var errors) || errors.ValueKind is JsonValueKind.Object or JsonValueKind.Null;
    }

    /// <summary>What a client would receive, as the checks above and the client's fixtures see it.</summary>
    private static JsonDocument Wire(object problem) => ManagementJson.Parse(problem);

    /// <summary>The shape every problem must have, and the code it must carry.</summary>
    private static void AssertReachesTheClient(JsonDocument wire, int status, string code)
    {
        var root = wire.RootElement;

        Assert.True(LooksLikeProblemDetails(root), $"Umbraco's interceptor would replace this body, and the code would be lost: {root}");
        Assert.Equal(JsonValueKind.String, root.GetProperty("type").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("type").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        Assert.Equal(JsonValueKind.Number, root.GetProperty("status").ValueKind);
        Assert.Equal(status, root.GetProperty("status").GetInt32());
        Assert.Equal(code, root.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
    }

    private static string FixturePath() => Path.Combine(ClientFiles.ClientDirectory(), "src", "testing", "problem-bodies.json");

    private static List<JsonObject> Fixtures() => [.. JsonNode.Parse(File.ReadAllText(FixturePath()))!.AsArray().Select(row => row!.AsObject())];

    private static Dictionary<string, object?>? ArgsOf(JsonObject row)
        => row["args"] is JsonObject args
            ? args.ToDictionary(pair => pair.Key, pair => pair.Value is null ? null : (object?)pair.Value.GetValue<string>())
            : null;

    // ---- the shape --------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_predicate_copied_from_Umbraco_tells_a_body_with_a_type_from_one_without()
    {
        using var withType = JsonDocument.Parse("""{"type":"t","title":"x","status":502,"code":"quota"}""");
        using var withoutType = JsonDocument.Parse("""{"title":"x","status":502,"code":"quota"}""");
        using var nullDetail = JsonDocument.Parse("""{"type":"t","title":"x","status":502,"detail":null}""");
        using var nullType = JsonDocument.Parse("""{"type":null,"title":"x","status":502}""");

        Assert.True(LooksLikeProblemDetails(withType.RootElement));
        Assert.False(LooksLikeProblemDetails(withoutType.RootElement)); // what the server wrote before: the code was lost
        Assert.False(LooksLikeProblemDetails(nullDetail.RootElement));
        Assert.True(LooksLikeProblemDetails(nullType.RootElement)); // Umbraco only asks that the member is there
    }

    [Fact]
    public void Every_code_in_every_status_the_server_answers_with_is_written_with_a_type_a_title_a_status_and_the_code()
    {
        foreach (var status in new[] { 400, 409, 502 })
        {
            foreach (var code in ErrorCodes.All.Concat(ApiCodes.All))
            {
                using var wire = Wire(ApiProblems.Create(new DefaultHttpContext(), status, code));

                AssertReachesTheClient(wire, status, code);
            }
        }
    }

    [Fact]
    public void A_problem_is_written_without_a_detail_or_an_instance_which_Umbraco_would_refuse_as_null()
    {
        using var wire = Wire(ApiProblems.Create(new DefaultHttpContext(), 502, ErrorCodes.NoAccess, new Dictionary<string, object?> { ["source"] = "ga4" }));

        Assert.False(wire.RootElement.TryGetProperty("detail", out _));
        Assert.False(wire.RootElement.TryGetProperty("instance", out _));
    }

    [Fact]
    public void The_type_names_the_code_so_that_two_problems_of_one_status_are_told_apart()
    {
        var quota = ApiProblems.Create(new DefaultHttpContext(), 502, ErrorCodes.Quota);
        var unknown = ApiProblems.Create(new DefaultHttpContext(), 502, ErrorCodes.Unknown);

        Assert.Equal("urn:esatto:seo-report:problem:quota", quota.Type);
        Assert.NotEqual(quota.Type, unknown.Type);
    }

    // ---- the client's fixtures are what the server writes ---------------------------------------------------------------------

    [Fact]
    public void The_shared_fixtures_are_exactly_what_the_server_writes()
    {
        var rows = Fixtures();

        Assert.True(rows.Count >= 15, $"{rows.Count} fixtures");
        foreach (var row in rows)
        {
            var http = new DefaultHttpContext { TraceIdentifier = row["traceId"]!.GetValue<string>() };
            var status = row["status"]!.GetValue<int>();
            var code = row["code"]!.GetValue<string>();

            var written = JsonNode.Parse(ManagementJson.Serialize(ApiProblems.Create(http, status, code, ArgsOf(row))))!;

            Assert.True(JsonNode.DeepEquals(row, written), $"{status} {code}: the fixture is\n{row.ToJsonString()}\nthe server writes\n{written.ToJsonString()}");
        }
    }

    [Fact]
    public void The_shared_fixtures_name_each_status_and_code_once()
    {
        var pairs = Fixtures().Select(row => (row["status"]!.GetValue<int>(), row["code"]!.GetValue<string>())).ToList();

        Assert.Equal(pairs.Count, pairs.Distinct().Count());
    }

    // ---- real controller calls ----------------------------------------------------------------------------------------------------

    private static (int Status, string Code) Pair(JsonObject row) => (row["status"]!.GetValue<int>(), row["code"]!.GetValue<string>());

    /// <summary>The (status, code) of the problem a real controller call answered with, checked for the shape on the way.</summary>
    private static (int Status, string Code) Produced(IActionResult result)
    {
        var status = Assert.IsAssignableFrom<ObjectResult>(result).StatusCode!.Value;
        using var wire = JsonDocument.Parse(ManagementJson.Body(result));
        var code = wire.RootElement.GetProperty("code").GetString()!;

        AssertReachesTheClient(wire, status, code);
        return (status, code);
    }

    [Fact]
    public async Task Every_failure_the_controller_can_answer_with_reaches_the_client_with_its_code_and_is_in_the_fixtures()
    {
        var produced = new List<(int Status, string Code)>();

        // 400: the month, the day, the body.
        var rig = await CreateAsync();
        produced.Add(Produced(await rig.Controller.GetReport("2026-13", Ct)));
        produced.Add(Produced(await rig.Controller.GetAnalysis("x", Ct)));
        produced.Add(Produced(await rig.Controller.Refresh(null, Ct)));
        produced.Add(Produced(await rig.Controller.PutNote("x", new NoteRequest("a"))));
        produced.Add(Produced(await rig.Controller.PutNote("2026-07", null)));
        produced.Add(Produced(await rig.Controller.PutTargets(null)));
        produced.Add(Produced(await rig.Controller.PutSettings(null, Ct)));
        produced.Add(Produced(await rig.Controller.GetDaily("2025-09", Ct)));
        produced.Add(Produced(await rig.Controller.GetDaily("x", Ct)));
        produced.Add(Produced(await rig.Controller.GetDailySources("x", Ct)));
        produced.Add(Produced(await rig.Controller.TestConnection(null, Ct)));

        rig.Reports.OnRefresh = (_, _) => throw new ArgumentOutOfRangeException("period", "outside");
        produced.Add(Produced(await rig.Controller.Refresh("2020-01", Ct)));

        // 400: a save that is refused.
        var empty = await CreateAsync(new Options(Ready: false));
        foreach (var (ga4, site, key) in new[] { ("abc", "example.com", (string?)null), ("123456789", "has spaces.com", null), ("123456789", "example.com", "not a key") })
        {
            produced.Add(Produced(await empty.Controller.PutSettings(new PutSettingsRequest(new SeoReportSettings { Ga4PropertyId = ga4, SearchConsoleSite = site }, key, false), Ct)));
        }

        // 409: Fetch now.
        var fetching = await CreateAsync();
        fetching.Spy.Result = FetchStartResult.AlreadyRunning;
        produced.Add(Produced(await fetching.Controller.StartFetch()));
        var notReady = await CreateAsync(new Options(Ready: false));
        notReady.Spy.Result = FetchStartResult.NotConfigured;
        produced.Add(Produced(await notReady.Controller.StartFetch()));

        // 409 and 502: whatever a fetch can end with.
        foreach (var kind in GoogleFailures.Kinds)
        {
            var refresh = await CreateAsync();
            var exception = await GoogleFailures.FailureOf(kind);
            refresh.Reports.OnRefresh = (_, _) => throw exception;
            produced.Add(Produced(await refresh.Controller.Refresh("2026-07", Ct)));
        }

        var fixtures = Fixtures().Select(Pair).ToHashSet();
        Assert.All(produced.Distinct(), pair => Assert.True(fixtures.Contains(pair), $"{pair} is a problem the controller answers with and no fixture covers it"));
        Assert.True(produced.Distinct().Count() >= 14, $"{produced.Distinct().Count()} distinct problems reached");
    }

    [Fact]
    public void Nothing_but_ApiProblems_makes_a_problem()
    {
        // Every other way of answering with a problem body (ProblemDetails of its own, Problem(), ValidationProblem()) would skip the type.
        var src = Path.Combine(ClientFiles.ClientDirectory(), "..", "src");
        var offenders = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.EndsWith("ApiProblems.cs", StringComparison.Ordinal))
            .Where(file => Regex.IsMatch(File.ReadAllText(file), @"new\s+(Validation)?ProblemDetails\b|(?<![.\w])(Validation)?Problem\("))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }
}
