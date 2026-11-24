using System.Reflection;
using Esatto.Umbraco.Backoffice.SeoReport.Api;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Jobs;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// What each endpoint DECLARES it answers: the contract the OpenAPI document and the client generated from it are built from. The
/// declarations are attributes, silent like the others, and ASP.NET's ApiExplorer keeps ONE entry per status code (the last
/// attribute wins), so a second type declared for the same status silently replaces the first in the document while the runtime
/// JSON stays correct and no behavioural test can see it. So every action is held to one declared type per status code, and what
/// ApiExplorer finally lists for it is pinned here against what the action returns.
/// </summary>
public class ResponseTypesTests
{
    private const int Ok = StatusCodes.Status200OK;
    private const int Accepted = StatusCodes.Status202Accepted;
    private const int BadRequest = StatusCodes.Status400BadRequest;
    private const int Conflict = StatusCodes.Status409Conflict;
    private const int BadGateway = StatusCodes.Status502BadGateway;

    private static readonly Type Problem = typeof(ProblemDetails);

    // Every public action: the type of its success answer and the problems it can answer with (all ProblemDetails with a code).
    private static readonly Dictionary<string, (int Status, Type Type)[]> Declared = new()
    {
        ["GetStatus"] = [(Ok, typeof(StatusResponse))],
        ["GetReport"] = [(Ok, typeof(ReportResponse)), (BadRequest, Problem)],
        ["Refresh"] = [(Ok, typeof(ReportResponse)), (BadRequest, Problem), (Conflict, Problem), (BadGateway, Problem)],
        ["StartFetch"] = [(Accepted, typeof(StartFetchResponse)), (Conflict, Problem)],
        ["GetFetchProgress"] = [(Ok, typeof(FetchStatusResponse))],
        ["GetAnalysis"] = [(Ok, typeof(AnalysisResponse)), (BadRequest, Problem)],
        ["GetDaily"] = [(Ok, typeof(DailyResponse)), (BadRequest, Problem), (Conflict, Problem), (BadGateway, Problem)],
        ["GetDailySources"] = [(Ok, typeof(DailySourcesResponse)), (BadRequest, Problem), (Conflict, Problem), (BadGateway, Problem)],
        ["PutNote"] = [(Ok, typeof(NoteResponse)), (BadRequest, Problem)],
        ["GetTargets"] = [(Ok, typeof(TargetsResponse))],
        ["PutTargets"] = [(Ok, typeof(TargetsResponse)), (BadRequest, Problem)],
        ["GetSettings"] = [(Ok, typeof(SettingsResponse))],
        ["PutSettings"] = [(Ok, typeof(SettingsResponse)), (BadRequest, Problem)],
        ["TestConnection"] = [(Ok, typeof(ConnectionTestResult)), (BadRequest, Problem)],
        ["GetUserGroups"] = [(Ok, typeof(UserGroupItem[]))],
    };

    private static readonly Type[] NotAContract = [typeof(object), typeof(IActionResult), typeof(ActionResult), typeof(void)];

    public static TheoryData<string> Methods => new(Declared.Keys);

    private static MethodInfo ActionOf(string method) => typeof(SeoReportController).GetMethod(method)!;

    [Fact]
    public void Every_public_action_is_in_the_table_so_a_new_endpoint_has_to_declare_what_it_answers()
    {
        var actions = typeof(SeoReportController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<NonActionAttribute>() is null && !m.IsSpecialName)
            .Select(m => m.Name);

        Assert.Equal(Declared.Keys.Order(), actions.Order());
        Assert.Equal(ControllerRoutingTests.Actions.Select(a => a.Method).Order(), Declared.Keys.Order());
    }

    [Theory]
    [MemberData(nameof(Methods))]
    public void An_action_declares_one_type_per_status_code(string method)
    {
        // The attributes themselves, because ApiExplorer would already have dropped the duplicate.
        var declared = ActionOf(method).GetCustomAttributes<ProducesResponseTypeAttribute>(inherit: true).ToList();

        var repeated = declared.GroupBy(a => a.StatusCode).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        Assert.True(repeated.Count == 0, $"{method} declares more than one type for status {string.Join(", ", repeated)}");
        Assert.Equal(Declared[method].OrderBy(d => d.Status).Select(d => (d.Status, d.Type)), declared.OrderBy(a => a.StatusCode).Select(a => (a.StatusCode, a.Type)));
    }

    // ---- what ApiExplorer finally lists --------------------------------------------------------------------------------------

    private static readonly Lazy<Dictionary<string, List<ApiResponseType>>> Listed = new(Explore);

    private static Dictionary<string, List<ApiResponseType>> Explore()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers(options => options.Conventions.Add(new BackOfficeTokenConvention())).AddApplicationPart(typeof(SeoReportController).Assembly);

        using var provider = services.BuildServiceProvider();
        var descriptions = provider.GetRequiredService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items
            .SelectMany(group => group.Items)
            .Where(item => item.ActionDescriptor is ControllerActionDescriptor { ControllerTypeInfo: var type } && type == typeof(SeoReportController));

        return descriptions.ToDictionary(
            item => ((ControllerActionDescriptor)item.ActionDescriptor).MethodInfo.Name,
            item => item.SupportedResponseTypes.ToList());
    }

    // The route template starts with the token [umbracoBackOffice], which the host fills in (it is the backoffice path, "umbraco" by
    // default); without a value for it the framework refuses to build the actions at all.
    private sealed class BackOfficeTokenConvention : IControllerModelConvention
    {
        public void Apply(ControllerModel controller) => controller.RouteValues["umbracoBackOffice"] = "umbraco";
    }

    [Fact]
    public void ApiExplorer_lists_every_action_of_the_controller()
        => Assert.Equal(Declared.Keys.Order(), Listed.Value.Keys.Order());

    [Theory]
    [MemberData(nameof(Methods))]
    public void ApiExplorer_lists_each_declared_status_once_with_its_type(string method)
    {
        var listed = Listed.Value[method];

        foreach (var (status, type) in Declared[method])
        {
            var entry = Assert.Single(listed, r => r.StatusCode == status);
            Assert.Equal(type, entry.Type);
        }

        Assert.Equal(listed.Count, listed.Select(r => r.StatusCode).Distinct().Count()); // one entry per status code, whatever else the framework added
    }

    [Theory]
    [MemberData(nameof(Methods))]
    public void The_success_answer_of_every_action_is_a_response_record_and_not_an_object_or_an_action_result(string method)
    {
        var success = Listed.Value[method].Where(r => r.StatusCode is >= 200 and < 300).ToList();

        var entry = Assert.Single(success);
        Assert.NotNull(entry.Type);
        Assert.DoesNotContain(entry.Type, NotAContract);
        Assert.Equal(Declared[method].Single(d => d.Status < 300).Type, entry.Type);
    }

    [Fact]
    public void The_analysis_is_documented_as_the_analysis_and_not_as_the_empty_answer()
    {
        var entry = Assert.Single(Listed.Value["GetAnalysis"], r => r.StatusCode == Ok);

        Assert.Equal(typeof(AnalysisResponse), entry.Type);
    }

    // ---- what the actions really return ----------------------------------------------------------------------------------------

    private static readonly CancellationToken Ct = CancellationToken.None;

    private static ApiRig WithReports(ApiRig rig)
    {
        foreach (var report in AnalysisArchive.Reports()) rig.Reports.ArchiveReport(report);
        return rig;
    }

    private static Task<IActionResult> DailyFails(ApiRig rig, Exception failure, Func<ApiRig, Task<IActionResult>> call)
    {
        DailyCurves.Serve(rig);
        rig.Daily.GetSeriesAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).ThrowsAsync(failure);
        rig.Daily.GetSourcesAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).ThrowsAsync(failure);
        return call(rig);
    }

    private static Task<IActionResult> RefreshFails(ApiRig rig, Exception failure)
    {
        rig.Reports.OnRefresh = (_, _) => throw failure;
        return rig.Controller.Refresh("2026-07", Ct);
    }

    // One call for every (action, status) the table declares, producing that very answer.
    private static readonly Dictionary<(string Method, int Status), Func<ApiRig, Task<IActionResult>>> Calls = new()
    {
        [("GetStatus", Ok)] = rig => rig.Controller.GetStatus(Ct),
        [("GetReport", Ok)] = rig => rig.Controller.GetReport(null, Ct),
        [("GetReport", BadRequest)] = rig => rig.Controller.GetReport("2026-13", Ct),
        [("Refresh", Ok)] = rig => rig.Controller.Refresh("2026-07", Ct),
        [("Refresh", BadRequest)] = rig => rig.Controller.Refresh(null, Ct),
        [("Refresh", Conflict)] = rig => RefreshFails(rig, new ReportFetchException(ErrorCodes.NotConfigured)),
        [("Refresh", BadGateway)] = rig => RefreshFails(rig, new InvalidOperationException("no code names this")),
        [("StartFetch", Accepted)] = rig => rig.Controller.StartFetch(),
        [("StartFetch", Conflict)] = rig =>
        {
            rig.Spy.Result = FetchStartResult.AlreadyRunning;
            return rig.Controller.StartFetch();
        },
        [("GetFetchProgress", Ok)] = rig => rig.Controller.GetFetchProgress(),
        [("GetAnalysis", Ok)] = rig => WithReports(rig).Controller.GetAnalysis(null, Ct),
        [("GetAnalysis", BadRequest)] = rig => rig.Controller.GetAnalysis("2026-13", Ct),
        [("GetDaily", Ok)] = rig =>
        {
            DailyCurves.Serve(rig);
            return rig.Controller.GetDaily(null, Ct);
        },
        [("GetDaily", BadRequest)] = rig => rig.Controller.GetDaily("2026-13", Ct),
        [("GetDaily", Conflict)] = rig => DailyFails(rig, new ReportFetchException(ErrorCodes.NotConfigured), r => r.Controller.GetDaily(null, Ct)),
        [("GetDaily", BadGateway)] = rig => DailyFails(rig, new InvalidOperationException("no code names this"), r => r.Controller.GetDaily(null, Ct)),
        [("GetDailySources", Ok)] = rig =>
        {
            DailyCurves.Serve(rig);
            return rig.Controller.GetDailySources("2026-08-05", Ct);
        },
        [("GetDailySources", BadRequest)] = rig => rig.Controller.GetDailySources("nonsense", Ct),
        [("GetDailySources", Conflict)] = rig => DailyFails(rig, new ReportFetchException(ErrorCodes.NotConfigured), r => r.Controller.GetDailySources("2026-08-05", Ct)),
        [("GetDailySources", BadGateway)] = rig => DailyFails(rig, new InvalidOperationException("no code names this"), r => r.Controller.GetDailySources("2026-08-05", Ct)),
        [("PutNote", Ok)] = rig => rig.Controller.PutNote("2026-07", new NoteRequest("A note.")),
        [("PutNote", BadRequest)] = rig => rig.Controller.PutNote("2026-13", new NoteRequest("A note.")),
        [("GetTargets", Ok)] = rig => rig.Controller.GetTargets(),
        [("PutTargets", Ok)] = rig => rig.Controller.PutTargets(new TargetsRequest(["red shoes"])),
        [("PutTargets", BadRequest)] = rig => rig.Controller.PutTargets(null),
        [("GetSettings", Ok)] = rig => rig.Controller.GetSettings(),
        [("PutSettings", Ok)] = rig => rig.Controller.PutSettings(new PutSettingsRequest(new SeoReportSettings(), null, false), Ct),
        [("PutSettings", BadRequest)] = rig => rig.Controller.PutSettings(null, Ct),
        [("TestConnection", Ok)] = rig => rig.Controller.TestConnection(new ConnectionTestRequest("123456789", "example.com", null), Ct),
        [("TestConnection", BadRequest)] = rig => rig.Controller.TestConnection(null, Ct),
        [("GetUserGroups", Ok)] = rig => rig.Controller.GetUserGroups(),
    };

    public static TheoryData<string, int> DeclaredAnswers
    {
        get
        {
            var data = new TheoryData<string, int>();
            foreach (var (method, responses) in Declared)
            {
                foreach (var (status, _) in responses) data.Add(method, status);
            }

            return data;
        }
    }

    [Fact]
    public void Every_declared_answer_has_a_call_that_produces_it()
        => Assert.Equal(
            Declared.SelectMany(d => d.Value.Select(r => (d.Key, r.Status))).Order(),
            Calls.Keys.Order());

    [Theory]
    [MemberData(nameof(DeclaredAnswers))]
    public async Task What_an_action_returns_for_a_status_is_the_type_it_declares_for_it(string method, int status)
    {
        var rig = await ApiRig.CreateAsync();

        var result = await Calls[(method, status)](rig);

        var body = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(status, body.StatusCode);
        Assert.Equal(Declared[method].Single(d => d.Status == status).Type, body.Value!.GetType());
    }
}
