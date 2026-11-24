using Esatto.Umbraco.Backoffice.SeoReport.Api;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Jobs;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The service-account key is write-only. With a marker and the private key's PEM inside a saved key, every response of every
/// endpoint (the successes and the error of each failure path) and every line the controller and the services log must be free of
/// both. The responses are serialized with the Management API's own JSON options, the way the browser receives them (see
/// <see cref="ManagementJson"/>), and the answers of the failure paths are produced by exceptions that carry the marker in their text.
/// </summary>
public class ApiLeakTests
{
    private const string Marker = FakeServiceAccount.LeakMarker;

    private static readonly CancellationToken Ct = CancellationToken.None;

    // A key that is valid and has the marker in every field Google does not need, so a leak of ANY part of it shows.
    private static string LeakyKey()
    {
        var key = FakeServiceAccount.Object();
        key["project_id"] = Marker;
        key["private_key_id"] = Marker;
        key["client_id"] = Marker;
        key["client_x509_cert_url"] = $"https://www.googleapis.com/robot/v1/metadata/x509/{Marker}";
        return key.ToJsonString();
    }

    // What must never appear: the marker, the PEM header, and a stretch of the PEM body (one with no '+' or '/', which JSON may escape).
    private static readonly string[] Secrets = [Marker, "PRIVATE KEY", PemChunk()];

    private static string PemChunk()
    {
        var body = string.Concat(FakeServiceAccount.PrivateKeyPem.Split('\n').Where(line => line.Length > 0 && !line.StartsWith("-----", StringComparison.Ordinal)));
        for (var start = 0; start + 16 <= body.Length; start++)
        {
            var window = body.Substring(start, 16);
            if (!window.Contains('+') && !window.Contains('/')) return window;
        }

        throw new InvalidOperationException("the synthetic key has no plain stretch");
    }

    private static void AssertFree(string what, string text)
    {
        foreach (var secret in Secrets) Assert.False(text.Contains(secret, StringComparison.Ordinal), $"{what} leaks '{secret}'");
    }

    private sealed class Responses
    {
        private readonly List<(string Name, string Json)> _all = [];

        public int Count => _all.Count;

        public async Task Add(string name, Task<IActionResult> call) => _all.Add((name, ManagementJson.Body(await call) is { Length: > 0 } json ? json : "<no body>"));

        public void AssertAllFree()
        {
            Assert.All(_all, response => AssertFree($"The response of '{response.Name}'", response.Json));
        }

        public IEnumerable<string> Names => _all.Select(r => r.Name);
    }

    [Fact]
    public async Task No_response_and_no_log_line_of_any_endpoint_holds_the_key()
    {
        var key = LeakyKey();
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: false, Groups: ["editors"], FindingsGroups: ["seo"]));
        rig.Reports.ArchiveReport(ApiRig.ReportWithFindings("2026-07"));
        DailyCurves.Serve(rig);
        var responses = new Responses();
        var c = rig.Controller;

        // Saving the key, and the answers to everything about settings.
        await responses.Add("PUT settings (the key)", c.PutSettings(new PutSettingsRequest(new SeoReportSettings { Ga4PropertyId = "123456789", SearchConsoleSite = "example.com" }, key, false), Ct));
        await responses.Add("GET settings", c.GetSettings());
        await responses.Add("POST settings/test (blank key)", c.TestConnection(new ConnectionTestRequest("123456789", "example.com", null), Ct));
        await responses.Add("POST settings/test (typed key)", c.TestConnection(new ConnectionTestRequest("123456789", "example.com", key), Ct));
        await responses.Add("POST settings/test (key that is not one)", c.TestConnection(new ConnectionTestRequest("123456789", "example.com", $"{{\"type\":\"authorized_user\",\"note\":\"{Marker}\"}}"), Ct));
        await responses.Add("POST settings/test (bad ids)", c.TestConnection(new ConnectionTestRequest("abc", "has spaces", key), Ct));
        await responses.Add("PUT settings (bad ga4, key in the body)", c.PutSettings(new PutSettingsRequest(new SeoReportSettings { Ga4PropertyId = "abc" }, key, false), Ct));
        await responses.Add("PUT settings (bad site)", c.PutSettings(new PutSettingsRequest(new SeoReportSettings { SearchConsoleSite = "has spaces" }, key, false), Ct));
        await responses.Add("PUT settings (key that is not one)", c.PutSettings(new PutSettingsRequest(new SeoReportSettings(), $"{{\"type\":\"authorized_user\",\"note\":\"{Marker}\"}}", false), Ct));
        await responses.Add("PUT settings (no body)", c.PutSettings(null, Ct));
        await responses.Add("PUT settings (remove)", c.PutSettings(new PutSettingsRequest(new SeoReportSettings { Ga4PropertyId = "123456789", SearchConsoleSite = "example.com" }, null, true), Ct));
        await responses.Add("PUT settings (the key again)", c.PutSettings(new PutSettingsRequest(new SeoReportSettings { Ga4PropertyId = "123456789", SearchConsoleSite = "example.com" }, key, false), Ct));
        await responses.Add("GET user-groups", c.GetUserGroups());

        // Everything the dashboard reads and does.
        await responses.Add("GET status", c.GetStatus(Ct));
        await responses.Add("GET report", c.GetReport("2026-07", Ct));
        await responses.Add("GET report (invalid)", c.GetReport("2026-13", Ct));
        await responses.Add("POST refresh", c.Refresh("2026-07", Ct));
        await responses.Add("POST refresh (invalid)", c.Refresh(null, Ct));
        await responses.Add("PUT note", c.PutNote("2026-07", new NoteRequest("A note.")));
        await responses.Add("GET targets", c.GetTargets());
        await responses.Add("PUT targets", c.PutTargets(new TargetsRequest(["red shoes"])));
        await responses.Add("GET fetch/progress", c.GetFetchProgress());
        foreach (var result in new[] { FetchStartResult.Started, FetchStartResult.AlreadyRunning, FetchStartResult.NotConfigured })
        {
            rig.Spy.Result = result;
            await responses.Add($"POST fetch ({result})", c.StartFetch());
        }

        // Every way a refresh can fail, with the marker in the exception's own text.
        foreach (var kind in GoogleFailures.Kinds)
        {
            var failure = await GoogleFailures.FailureOf(kind);
            rig.Reports.OnRefresh = (_, _) => throw failure;
            await responses.Add($"POST refresh ({kind})", c.Refresh("2026-07", Ct));
        }

        rig.Reports.OnRefresh = (_, _) => throw new ArgumentOutOfRangeException("period", $"outside {Marker}");
        await responses.Add("POST refresh (out of range)", c.Refresh("2020-01", Ct));

        // The analysis and the daily curve: what they answer, what they refuse, and every way the data behind them can fail.
        await responses.Add("GET analysis", c.GetAnalysis(null, Ct));
        await responses.Add("GET analysis (invalid)", c.GetAnalysis("2026-13", Ct));
        await responses.Add("GET daily", c.GetDaily(null, Ct));
        await responses.Add("GET daily (a month)", c.GetDaily("2026-06", Ct));
        await responses.Add("GET daily (invalid)", c.GetDaily("2026-13", Ct));
        await responses.Add("GET daily (out of range)", c.GetDaily("2020-01", Ct));
        await responses.Add("GET daily/sources", c.GetDailySources("2026-08-05", Ct));
        await responses.Add("GET daily/sources (invalid)", c.GetDailySources("nonsense", Ct));
        await responses.Add("GET daily/sources (out of range)", c.GetDailySources("2020-01-01", Ct));

        foreach (var kind in GoogleFailures.Kinds)
        {
            var failure = await GoogleFailures.FailureOf(kind);
            rig.Daily.GetSeriesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).ThrowsAsync(failure);
            rig.Daily.GetSourcesAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).ThrowsAsync(failure);
            await responses.Add($"GET daily ({kind})", c.GetDaily(null, Ct));
            await responses.Add($"GET daily/sources ({kind})", c.GetDailySources("2026-08-05", Ct));
        }

        rig.Daily.GetSeriesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ArgumentException($"rejected {Marker}", "month"));
        await responses.Add("GET daily (the service refuses the month)", c.GetDaily("2026-06", Ct));

        Assert.Equal(69, responses.Count); // every call above answered
        responses.AssertAllFree();
        AssertFree("The log", rig.AllLogText);

        // No endpoint escapes this test: the ones exercised are exactly the ones the controller has (ControllerRoutingTests lists them).
        var exercised = responses.Names.Select(name => name.Split(" (")[0]).Distinct().Order();
        var endpoints = ControllerRoutingTests.Actions.Select(action => $"{action.Verb} {action.Route}").Order();
        Assert.Equal(endpoints, exercised);

        // The requests themselves cannot give the key away either: a record's own text and its serialization leave it out.
        AssertFree("PutSettingsRequest.ToString()", new PutSettingsRequest(new SeoReportSettings(), key, false).ToString());
        AssertFree("PutSettingsRequest as JSON", ManagementJson.Serialize(new PutSettingsRequest(new SeoReportSettings(), key, false)));
        AssertFree("ConnectionTestRequest.ToString()", new ConnectionTestRequest("123456789", "example.com", key).ToString());
        AssertFree("ConnectionTestRequest as JSON", ManagementJson.Serialize(new ConnectionTestRequest("123456789", "example.com", key)));
    }

    [Fact]
    public async Task A_saved_key_that_cannot_be_read_leaks_nothing_either()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: false));
        await rig.Harness.StoreRawProtectedKeyAsync(LeakyKey(), new SecretProtector(new EphemeralDataProtectionProvider()));
        rig.Harness.Time.Advance(TimeSpan.FromMinutes(2)); // the settings are cached for a minute
        var responses = new Responses();

        await responses.Add("GET status", rig.Controller.GetStatus(Ct));
        await responses.Add("GET settings", rig.Controller.GetSettings());
        await responses.Add("POST settings/test", rig.Controller.TestConnection(new ConnectionTestRequest("123456789", "example.com", null), Ct));
        await responses.Add("POST refresh", rig.Controller.Refresh("2026-07", Ct));
        rig.Spy.Result = FetchStartResult.NotConfigured;
        await responses.Add("POST fetch", rig.Controller.StartFetch());

        responses.AssertAllFree();
        AssertFree("The log", rig.AllLogText);
    }

    [Fact]
    public async Task The_log_of_a_real_backfill_holds_no_key_either()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: true, KeyJson: LeakyKey(), RealCoordinator: true));

        await rig.Controller.StartFetch();
        await rig.Real.CurrentRun!.WaitAsync(TimeSpan.FromSeconds(30));
        await rig.Controller.GetFetchProgress();

        AssertFree("The log", rig.AllLogText);
        Assert.NotEmpty(rig.Reports.Refreshed); // it did run
    }
}
