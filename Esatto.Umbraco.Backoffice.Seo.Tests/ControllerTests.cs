using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Esatto.Umbraco.Backoffice.Seo.Checks;
using Esatto.Umbraco.Backoffice.Seo.Knowledge;
using Esatto.Umbraco.Backoffice.Seo.Reporting;
using Esatto.Umbraco.Backoffice.Seo.Scanning;
using Esatto.Umbraco.Backoffice.Seo.Settings;
using Esatto.Umbraco.Backoffice.Seo.Tests.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.Common.Authorization;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Seo.Tests;

public class ControllerTests
{
    [Fact]
    public void Controller_requires_content_section()
        => Assert.Equal(AuthorizationPolicies.SectionAccessContent, typeof(SeoHealthController).GetCustomAttribute<AuthorizeAttribute>(inherit: false)!.Policy);

    [Fact]
    public void Put_settings_requires_settings_section()
        => Assert.Equal(AuthorizationPolicies.SectionAccessSettings,
            typeof(SeoHealthController).GetMethod(nameof(SeoHealthController.PutSettings))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy);

    [Fact]
    public void Route_is_esatto_seo_health()
        => Assert.EndsWith("/esatto/seo-health", typeof(SeoHealthController).GetCustomAttribute<VersionedApiBackOfficeRouteAttribute>()!.Template.TrimEnd('/'));

    private static (SeoHealthController Controller, Rig Rig, ScanCoordinator Coordinator) Create()
    {
        var rig = new Rig().Page("/", root: true);
        var env = new SeoEnvironment(rig.Configuration);
        var services = new ServiceCollection();
        // A scan that never finishes on its own, so "already running" can be observed.
        rig.Http.OnAsync("https://esatto.se/", async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return new HttpResponseMessage(); });
        services.AddScoped(_ => rig.Runner());
        var coordinator = new ScanCoordinator(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), NullLogger<ScanCoordinator>.Instance);
        var loader = new SnapshotLoader(rig.Store, env);
        var controller = new SeoHealthController(new KnowledgeBase(), rig.Settings(), env, rig.Source(), coordinator,
            new ReportService(rig.Store, coordinator, env, rig.Cache), new IgnoreService(rig.Store, loader, rig.Cache, rig.RowLock),
            rig.Recheck(), Substitute.For<IBackOfficeSecurityAccessor>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        controller.HttpContext.Request.Scheme = "https";
        controller.HttpContext.Request.Host = new HostString("esatto.se");
        return (controller, rig, coordinator);
    }

    [Fact]
    public async Task Start_scan_twice_returns_409_and_cancel_stops_it()
    {
        var (controller, _, coordinator) = Create();
        Assert.IsType<OkObjectResult>(await controller.StartScan(Rig.Site));
        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ConflictObjectResult>(await controller.StartScan(Rig.Site)).StatusCode);

        Assert.IsType<NoContentResult>(controller.CancelScan(Rig.Site));
        for (var i = 0; i < 100 && coordinator.IsRunning(Rig.Site); i++) await Task.Delay(20);
        Assert.False(coordinator.IsRunning(Rig.Site));
    }

    // Minor #12: a key that isn't a site's root writes nothing (no junk failed scan, no orphan ignore).

    [Fact]
    public async Task Start_scan_of_unknown_site_is_404_and_writes_nothing()
    {
        var (controller, rig, _) = Create();
        Assert.IsType<NotFoundObjectResult>(await controller.StartScan(Guid.NewGuid()));
        Assert.Empty(rig.Store.Scans);
    }

    [Fact]
    public async Task Ignore_for_unknown_site_is_404_and_writes_nothing()
    {
        var (controller, rig, _) = Create();
        Assert.IsType<NotFoundObjectResult>(await controller.AddIgnore(Guid.NewGuid(), new IgnoreRequest("title-missing", null, null, "note")));
        Assert.Empty(rig.Store.Ignores);
    }

    [Fact]
    public void Cancel_without_scan_is_404() => Assert.IsType<NotFoundResult>(Create().Controller.CancelScan(Rig.Site));

    [Fact]
    public async Task Add_ignore_bad_input_is_400()
        => Assert.IsType<BadRequestObjectResult>(await Create().Controller.AddIgnore(Rig.Site, new IgnoreRequest("title-missing", null, null, " ")));

    [Fact]
    public async Task Recheck_without_scan_is_409()
        => Assert.IsType<ConflictObjectResult>(await Create().Controller.Recheck(Rig.Site, new RecheckRequest(Guid.NewGuid(), "en"), CancellationToken.None));
}

public class ApiJsonContractTests
{
    // The dashboard expects camelCase enum names ("warning", "editor"). Umbraco's management API writes enums with a
    // plain JsonStringEnumConverter, i.e. as declared ("Warning"), which left every severity badge and owner label
    // empty. The names must not depend on the host's JSON settings at all.
    public static TheoryData<string, JsonSerializerOptions> Hosts => new()
    {
        { "Umbraco management API", new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } } },
        { "no enum converter", new JsonSerializerOptions(JsonSerializerDefaults.Web) },
    };

    [Theory]
    [MemberData(nameof(Hosts))]
    public void Enums_are_written_in_camel_case_whatever_the_host_configures(string host, JsonSerializerOptions options)
    {
        var json = JsonSerializer.Serialize(new CheckInfo("x", Severity.Warning, Owner.Developer, CheckScope.CrossPage), options);

        Assert.True(json.Contains("\"severity\":\"warning\"") && json.Contains("\"owner\":\"developer\"") && json.Contains("\"scope\":\"crossPage\""), $"{host}: {json}");
    }

    [Fact]
    public void Enums_read_back_from_their_json_names()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

        var info = JsonSerializer.Deserialize<CheckInfo>("{\"id\":\"x\",\"severity\":\"critical\",\"owner\":\"editor\",\"scope\":\"site\"}", options)!;

        Assert.Equal((Severity.Critical, Owner.Editor, CheckScope.Site), (info.Severity, info.Owner, info.Scope));
    }
}
