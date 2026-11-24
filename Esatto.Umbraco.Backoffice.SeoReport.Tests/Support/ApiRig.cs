using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Jobs;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>
/// The controller wired the way the host wires it, over fakes: the real settings, notes, targets and Test connection services over
/// an in-memory store and fake Google transports, a report service and a coordinator a test can watch, and a signed-in user whose
/// groups a test chooses. Every log the controller and the services write is captured, so a test can read them.
/// </summary>
internal sealed class ApiRig
{
    private ApiRig(SettingsHarness harness, FakeReportService reports, Func<ApiRig, IFetchCoordinator> coordinator, string[]? groups, bool signedIn, bool realDaily)
    {
        Harness = harness;
        Reports = reports;

        Notes = new NoteService(harness.Store);
        Targets = new TargetQueryService(harness.Store);

        if (realDaily)
        {
            // Data up to yesterday, as GA4 has it; the real service over real Google service objects with a fake transport.
            DailyGoogle = new SyntheticGoogle(DateOnly.FromDateTime(harness.Time.GetUtcNow().UtcDateTime).AddDays(-1));
            Daily = new DailyTrafficService(harness.Settings, DailyGoogle, new MemoryCache(new MemoryCacheOptions()), harness.Time);
        }
        else
        {
            Daily = Substitute.For<IDailyTrafficService>();
        }

        Google = Pair();
        var typed = Pair();
        var factory = Substitute.For<IGoogleClientFactory>();
        factory.Create(Arg.Any<GoogleCredential>()).Returns(typed);
        Tester = new ConnectionTester(harness.Credentials, Google, factory, harness.Time, TesterLog);

        Coordinator = coordinator(this);

        // A substitute answers an interface-typed property with another substitute, so "nobody signed in" has to be said: the security
        // object is there and has no current user, which is what the host answers when it cannot find one.
        Security = Substitute.For<IBackOfficeSecurityAccessor>();
        if (!signedIn)
        {
            var nobody = Substitute.For<IBackOfficeSecurity>();
            nobody.CurrentUser.Returns((IUser?)null);
            Security.BackOfficeSecurity.Returns(nobody);
        }
        else
        {
            var memberships = new List<IReadOnlyUserGroup>();
            foreach (var alias in groups ?? [])
            {
                var group = Substitute.For<IReadOnlyUserGroup>();
                group.Alias.Returns(alias);
                memberships.Add(group);
            }

            var user = Substitute.For<IUser>();
            user.Groups.Returns(memberships);

            var security = Substitute.For<IBackOfficeSecurity>();
            security.CurrentUser.Returns(user);
            Security.BackOfficeSecurity.Returns(security);
        }

        UserGroups = Substitute.For<IUserGroupService>();
        UserGroups.GetAllAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(new PagedModel<IUserGroup>(0, []));

        Http = new DefaultHttpContext();
        Controller = new SeoReportController(
            Reports, Daily, Notes, Targets, harness.Settings, Coordinator, Tester, harness.Store, Security, UserGroups, harness.Time, ControllerLog)
        {
            ControllerContext = new ControllerContext { HttpContext = Http },
        };
    }

    public SettingsHarness Harness { get; }

    public FakeReportService Reports { get; }

    public IFetchCoordinator Coordinator { get; }

    /// <summary>The coordinator when it is the spy (the default).</summary>
    public SpyCoordinator Spy => (SpyCoordinator)Coordinator;

    /// <summary>The coordinator when it is the real one (see <see cref="Options.RealCoordinator"/>).</summary>
    public FetchCoordinator Real => (FetchCoordinator)Coordinator;

    public INoteService Notes { get; }

    public ITargetQueryService Targets { get; }

    /// <summary>A substitute (what the controller was told, see <see cref="DailyCurves"/>), or the real service when <see cref="Options.RealDaily"/>.</summary>
    public IDailyTrafficService Daily { get; }

    /// <summary>The Google the real daily service talks to; null unless <see cref="Options.RealDaily"/>.</summary>
    public SyntheticGoogle? DailyGoogle { get; }

    public ConnectionTester Tester { get; }

    /// <summary>The saved key's clients: what Test connection and the check after saving talk to.</summary>
    public SplitGoogle Google { get; }

    public IBackOfficeSecurityAccessor Security { get; }

    public IUserGroupService UserGroups { get; }

    public DefaultHttpContext Http { get; }

    public SeoReportController Controller { get; }

    public CapturingLogger<SeoReportController> ControllerLog { get; } = new();

    public CapturingLogger<ConnectionTester> TesterLog { get; } = new();

    public CapturingLogger<FetchCoordinator> CoordinatorLog { get; } = new();

    /// <summary>How the GA4 transport answers. Change it per test.</summary>
    public Func<FakeGoogleTransport.Call, FakeGoogleTransport.Answer> Ga4Answer { get; set; } = ConnectionRig.Ga4Ok;

    /// <summary>How the Search Console transport answers. Change it per test.</summary>
    public Func<FakeGoogleTransport.Call, FakeGoogleTransport.Answer> SearchConsoleAnswer { get; set; } = ConnectionRig.SearchConsoleOk;

    /// <summary>
    /// Sets (or clears) <c>Esatto:SeoReport:&lt;name&gt;</c> in configuration and lets the settings service's one-minute cache lapse, as
    /// a configuration reload followed by a minute would.
    /// </summary>
    public void Configure(string name, string? value)
    {
        Harness.Configure(name, value);
        Harness.Time.Advance(TimeSpan.FromMinutes(2));
    }

    /// <summary>Everything the controller and the services behind it logged, as one text.</summary>
    public string AllLogText => string.Join("\n", Harness.Log.AllText, ControllerLog.AllText, TesterLog.AllText, CoordinatorLog.AllText);

    private SplitGoogle Pair() => new(
        new FakeAnalyticsData(new FakeGoogleTransport(call => Ga4Answer(call))),
        new FakeSearchConsole(new FakeGoogleTransport(call => SearchConsoleAnswer(call))));

    /// <param name="Ready">Whether a key and both properties are saved (see <see cref="ReadySettingsHarness"/>).</param>
    /// <param name="KeyJson">The key to save when ready; the synthetic default when null.</param>
    /// <param name="Groups">The signed-in user's group aliases. A user with none when null.</param>
    /// <param name="SignedIn">False: there is no backoffice user at all.</param>
    /// <param name="FindingsGroups">The aliases saved as <c>FindingsUserGroups</c>.</param>
    /// <param name="RealCoordinator">True: the real <see cref="FetchCoordinator"/> over the fake report service; otherwise the spy.</param>
    /// <param name="Now">What the clock says from the start (it can only be moved forward afterwards); <see cref="ReadySettingsHarness.Now"/> when null.</param>
    /// <param name="RealDaily">True: the real <see cref="DailyTrafficService"/> over <see cref="DailyGoogle"/>; otherwise a substitute.</param>
    internal sealed record Options(
        bool Ready = true,
        string? KeyJson = null,
        string[]? Groups = null,
        bool SignedIn = true,
        string[]? FindingsGroups = null,
        bool RealCoordinator = false,
        DateTimeOffset? Now = null,
        bool RealDaily = false);

    public static async Task<ApiRig> CreateAsync(Options? options = null)
    {
        options ??= new Options();

        var harness = new SettingsHarness();
        harness.Time.SetUtcNow(options.Now ?? ReadySettingsHarness.Now);

        var settings = new SeoReportSettings
        {
            Ga4PropertyId = options.Ready ? ReadySettingsHarness.Ga4Property : null,
            SearchConsoleSite = options.Ready ? ReadySettingsHarness.Site : null,
            FindingsUserGroups = options.FindingsGroups ?? [],
        };
        await harness.Settings.SaveAsync(settings, options.Ready ? options.KeyJson ?? FakeServiceAccount.Json() : null, removeKey: false);

        var reports = new FakeReportService();
        return new ApiRig(
            harness,
            reports,
            rig => options.RealCoordinator
                ? new FetchCoordinator(reports, harness.Settings, harness.Store, harness.Time, rig.CoordinatorLog)
                : new SpyCoordinator(),
            options.Groups,
            options.SignedIn,
            options.RealDaily);
    }

    /// <summary>A report with a warning and an info annotation in its search block (the findings), for <paramref name="period"/>.</summary>
    public static Report ReportWithFindings(string period = "2026-07")
    {
        var report = TestReports.With(period, queries: [], pages: [], queryPages: []);
        var search = report.Traffic.Search!;

        return report with
        {
            Traffic = report.Traffic with
            {
                Search = search with
                {
                    Annotations =
                    [
                        new Annotation(period, "warning", new TextRef("brandDominated")),
                        new Annotation(period, "info", new TextRef("anonymizedQueries")),
                        new Annotation(period, "warning", new TextRef("noKeyEvents")),
                    ],
                },
            },
        };
    }
}

/// <summary>Reading an action result the way the framework would.</summary>
internal static class ApiResults
{
    /// <summary>The body of a 200 result, as <typeparamref name="T"/>.</summary>
    public static T Ok<T>(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return Assert.IsType<T>(ok.Value);
    }

    /// <summary>The body of a result with the given status (202 for an accepted one), as <typeparamref name="T"/>.</summary>
    public static T Body<T>(IActionResult result, int status)
    {
        var body = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(status, body.StatusCode);
        return Assert.IsType<T>(body.Value);
    }

    /// <summary>The problem a result carries, which must have the given status.</summary>
    public static ProblemDetails Problem(IActionResult result, int status)
    {
        var problem = Body<ProblemDetails>(result, status);
        Assert.Equal(status, problem.Status);
        return problem;
    }

    /// <summary>The <c>code</c> of a problem.</summary>
    public static string? Code(ProblemDetails problem)
        => problem.Extensions.TryGetValue("code", out var code) ? code as string : null;

    /// <summary>The <c>args</c> of a problem, or null when it has none.</summary>
    public static IReadOnlyDictionary<string, object?>? Args(ProblemDetails problem)
        => problem.Extensions.TryGetValue("args", out var args) ? args as IReadOnlyDictionary<string, object?> : null;
}
