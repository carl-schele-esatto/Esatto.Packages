using System.Net;
using Esatto.Umbraco.Backoffice.SeoReport.Api;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Jobs;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>
/// One answer of every kind the controller gives, each produced by the controller itself over the test rig: the real settings,
/// notes, targets, Test connection and daily card over fake Google transports, the analysis over the synthetic archive, and the
/// report <see cref="ReportMapper.Build"/> makes for the golden scenario "full" (a brand split, and warnings on the charts). Synthetic
/// data only. For tests that must see exactly what a browser would receive.
/// </summary>
internal static class RealResponses
{
    /// <summary>The month the report rig's clock and archive are set to: the golden scenario's month is the newest.</summary>
    public static readonly DateTimeOffset ReportNow = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

    // The mapper stamps a report with the real time it was built; a fixed stamp keeps every answer the same from run to run.
    private const string GeneratedAt = "2026-11-06T06:00:00Z";

    private static readonly CancellationToken Ct = CancellationToken.None;

    /// <summary>Every answer by name, in a fixed order. The value is what the action result carries, as the output formatter gets it.</summary>
    public static async Task<IReadOnlyList<(string Name, object Value)>> AllAsync()
    {
        var answers = new List<(string Name, object Value)>();

        // The report, its status and the settings: the golden report archived as the newest month, a fetch that failed after one that
        // worked, a display name set in configuration (so it is also a locked field).
        var report = ReportMapperTests.GoldenReport("full") with { GeneratedAt = GeneratedAt };
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Now: ReportNow));
        rig.Reports.ExpectedLatestPeriod = Period.Parse(report.ReportPeriod);
        rig.Reports.ArchiveReport(report, ReportNow.AddDays(-4));
        await rig.Notes.SaveAsync(Period.Parse(report.ReportPeriod), "A campaign started on the 3rd.");
        await rig.Targets.SaveAsync(["red shoes", "blue hat"]);
        await rig.Harness.Store.SaveDocumentAsync(
            DocumentKeys.FetchState,
            new FetchState(ReportNow.AddHours(-2), ReportNow.AddDays(-3), ReportNow.AddHours(-2), ErrorCodes.NoAccess, FetchState.Manual).ToJson());
        rig.Configure("DisplayName", "Example site");

        answers.Add(("status", Value(await rig.Controller.GetStatus(Ct))));
        answers.Add(("report", Value(await rig.Controller.GetReport(report.ReportPeriod, Ct))));
        answers.Add(("settings", Value(await rig.Controller.GetSettings())));
        answers.Add(("fetchProgress", Value(await rig.Controller.GetFetchProgress())));
        answers.Add(("startFetch", Value(await rig.Controller.StartFetch())));
        answers.Add(("note", Value(await rig.Controller.PutNote(report.ReportPeriod, new NoteRequest("A campaign started on the 3rd.")))));
        answers.Add(("targets", Value(await rig.Controller.GetTargets())));

        var group = Substitute.For<IUserGroup>();
        group.Alias.Returns("editor");
        group.Name.Returns("Editors");
        rig.UserGroups.GetAllAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(new PagedModel<IUserGroup>(1, [group]));
        answers.Add(("userGroups", Value(await rig.Controller.GetUserGroups())));

        // Test connection: the key and GA4 work, the Search Console API is switched off (a code with its arguments).
        rig.SearchConsoleAnswer = _ => GoogleErrorBodies.DisabledAnswer();
        answers.Add(("connectionTest", Value(await rig.Controller.TestConnection(
            new ConnectionTestRequest(ReadySettingsHarness.Ga4Property, ReadySettingsHarness.Site, null), Ct))));

        // Problems: a refused month (a code), and a fetch Google refused (a code with its arguments).
        answers.Add(("periodProblem", Value(await rig.Controller.GetReport("2026-13", Ct))));
        var disabled = await GoogleFailures.Ga4Async(HttpStatusCode.Forbidden, GoogleErrorBodies.Disabled());
        rig.Reports.OnRefresh = (_, _) => throw disabled;
        answers.Add(("fetchProblem", Value(await rig.Controller.Refresh(report.ReportPeriod, Ct))));

        // The analysis over the synthetic archive, findings on the newest month; and the answer when nothing is archived.
        var archive = await ApiRig.CreateAsync();
        foreach (var month in AnalysisArchive.Reports())
        {
            archive.Reports.ArchiveReport(month.ReportPeriod == AnalysisArchive.Newest ? AnalysisArchive.WithFindings(month) : month);
        }

        await archive.Targets.SaveAsync(AnalysisArchive.Terms);
        await archive.Notes.SaveAsync(Period.Parse(AnalysisArchive.Newest), "A note.");
        answers.Add(("analysis", Value(await archive.Controller.GetAnalysis(null, Ct))));
        answers.Add(("noAnalysis", Value(await (await ApiRig.CreateAsync()).Controller.GetAnalysis(null, Ct))));

        // The daily card over the synthetic curve.
        var daily = await ApiRig.CreateAsync();
        DailyCurves.Serve(daily);
        answers.Add(("daily", Value(await daily.Controller.GetDaily(null, Ct))));
        answers.Add(("dailySources", Value(await daily.Controller.GetDailySources("2026-08-05", Ct))));

        return answers;
    }

    private static object Value(IActionResult result)
        => result is ObjectResult { Value: { } value } ? value : throw new InvalidOperationException($"{result.GetType().Name} has no body.");
}
