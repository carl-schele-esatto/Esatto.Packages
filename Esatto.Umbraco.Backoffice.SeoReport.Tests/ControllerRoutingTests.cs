using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Web.Common.Authorization;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// That the controller CARRIES its attributes, and that each endpoint has the policy it was meant to have.
///
/// Attributes are silent infrastructure: they show in no signature and cannot fail to compile. A type inserted between them and the
/// class declaration moves all of them onto the wrong type (the controller then has no route and every endpoint answers 404), and a
/// forgotten <c>[Authorize]</c> on one action leaves, say, the key settings open to everyone with the Content section. So they are
/// tested, and every public action is LISTED here: a new endpoint cannot be added without deciding, in this file, who may call it.
/// </summary>
public class ControllerRoutingTests
{
    private static readonly Type Controller = typeof(SeoReportController);

    private const string Content = AuthorizationPolicies.SectionAccessContent;
    private const string Settings = AuthorizationPolicies.SectionAccessSettings;

    /// <summary>
    /// Every public action of the controller: its method, the HTTP verb and route it answers, and the policy it asks for ON TOP of the
    /// class's (Content): null for the endpoints every user with the Content section may call, Settings for the ones only people with the
    /// Settings section may. Both policies must pass for a Settings action, which is the intent.
    /// </summary>
    // Internal and not private: ApiLeakTests holds its list of endpoints against this one, so an endpoint added here has to be added there.
    internal static readonly (string Method, string Verb, string Route, string? ExtraPolicy)[] Actions =
    [
        ("GetStatus", "GET", "status", null),
        ("GetReport", "GET", "report", null),
        ("Refresh", "POST", "refresh", null),
        ("StartFetch", "POST", "fetch", null),
        ("GetFetchProgress", "GET", "fetch/progress", null),
        ("GetAnalysis", "GET", "analysis", null),
        ("GetDaily", "GET", "daily", null),
        ("GetDailySources", "GET", "daily/sources", null),
        ("PutNote", "PUT", "note", null),
        ("GetTargets", "GET", "targets", null),
        ("PutTargets", "PUT", "targets", null),
        ("GetSettings", "GET", "settings", Settings),
        ("PutSettings", "PUT", "settings", Settings),
        ("TestConnection", "POST", "settings/test", Settings),
        ("GetUserGroups", "GET", "user-groups", Settings),
    ];

    private static IEnumerable<MethodInfo> PublicActions
        => Controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<NonActionAttribute>() is null && !m.IsSpecialName);

    [Fact]
    public void The_controller_is_an_api_controller()
        => Assert.NotNull(Controller.GetCustomAttribute<ApiControllerAttribute>(inherit: false));

    [Fact]
    public void The_controller_has_the_management_api_route_for_esatto_seo_report()
    {
        var route = Controller.GetCustomAttribute<VersionedApiBackOfficeRouteAttribute>(inherit: false);

        Assert.NotNull(route); // without it no endpoint is mapped and every one answers 404
        Assert.EndsWith("/management/api/v{version:apiVersion}/esatto/seo-report", route.Template); // the prefix is Umbraco's backoffice path token
    }

    [Fact]
    public void The_controller_is_in_the_esatto_seo_report_api_group()
        => Assert.Equal("Esatto SEO Report", Controller.GetCustomAttribute<ApiExplorerSettingsAttribute>(inherit: false)?.GroupName);

    [Fact]
    public void The_controller_requires_the_content_section_on_the_class_itself()
    {
        // inherit: false on purpose. ManagementApiControllerBase has [Authorize] attributes of its own, and the declaration that
        // can slide off this class is the one worth testing.
        Assert.Contains(
            Controller.GetCustomAttributes<AuthorizeAttribute>(inherit: false),
            authorize => authorize.Policy == Content);
    }

    [Fact]
    public void The_class_does_not_ask_for_the_settings_section_which_would_close_the_report_to_editors()
        => Assert.DoesNotContain(Controller.GetCustomAttributes<AuthorizeAttribute>(inherit: false), authorize => authorize.Policy == Settings);

    [Fact]
    public void No_endpoint_can_be_called_anonymously()
    {
        Assert.Null(Controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: false));
        Assert.All(PublicActions, action => Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>()));
    }

    [Fact]
    public void Every_public_action_is_listed_here_with_its_policy_decision()
    {
        var actual = PublicActions.Select(m => m.Name).Order().ToArray();
        var listed = Actions.Select(a => a.Method).Order().ToArray();

        Assert.Equal(listed, actual);
    }

    [Theory]
    [MemberData(nameof(ListedActions))]
    public void Each_action_answers_its_verb_and_route(string method, string verb, string route)
    {
        var action = Controller.GetMethod(method)!;
        var http = Assert.Single(action.GetCustomAttributes<HttpMethodAttribute>());

        Assert.Equal(verb, Assert.Single(http.HttpMethods));
        Assert.Equal(route, http.Template);
    }

    [Theory]
    [MemberData(nameof(SettingsActions))]
    public void A_settings_action_asks_for_the_settings_section_as_well(string method)
    {
        var policies = Controller.GetMethod(method)!.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Select(a => a.Policy).ToArray();

        Assert.Contains(Settings, policies);
        Assert.DoesNotContain(Content, policies); // that is the class's; repeating it here would hide a class that lost it
    }

    [Theory]
    [MemberData(nameof(ContentActions))]
    public void A_content_action_asks_for_nothing_beyond_the_class(string method)
        => Assert.Empty(Controller.GetMethod(method)!.GetCustomAttributes<AuthorizeAttribute>(inherit: true));

    [Fact]
    public void The_request_and_response_models_carry_no_controller_attributes()
    {
        // The bug this file exists for: the attributes slid down onto a type declared between them and the class.
        var models = Controller.Assembly.GetTypes()
            .Where(t => t != Controller && t.Namespace is { } ns && ns.StartsWith("Esatto.Umbraco.Backoffice.SeoReport", StringComparison.Ordinal))
            .Where(t => t.Name.EndsWith("Request", StringComparison.Ordinal) || t.Name.EndsWith("Response", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(models);
        Assert.All(models, model =>
        {
            Assert.Null(model.GetCustomAttribute<VersionedApiBackOfficeRouteAttribute>());
            Assert.Null(model.GetCustomAttribute<AuthorizeAttribute>());
            Assert.Null(model.GetCustomAttribute<ApiControllerAttribute>());
        });
    }

    public static IEnumerable<object[]> ListedActions => Actions.Select(a => new object[] { a.Method, a.Verb, a.Route });

    public static IEnumerable<object[]> SettingsActions => Actions.Where(a => a.ExtraPolicy == Settings).Select(a => new object[] { a.Method });

    public static IEnumerable<object[]> ContentActions => Actions.Where(a => a.ExtraPolicy is null).Select(a => new object[] { a.Method });
}
