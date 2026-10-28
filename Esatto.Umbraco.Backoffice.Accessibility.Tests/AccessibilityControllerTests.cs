using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Web.Common.Authorization;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Accessibility.Tests;

public class AccessibilityControllerTests
{
    [Fact]
    public void Controller_requires_content_section()
        // inherit: false — ManagementApiControllerBase carries its own BackOfficeAccess policy.
        => Assert.Equal(AuthorizationPolicies.SectionAccessContent, typeof(AccessibilityController).GetCustomAttribute<AuthorizeAttribute>(inherit: false)!.Policy);

    [Fact]
    public void Put_settings_requires_settings_section()
        => Assert.Equal(AuthorizationPolicies.SectionAccessSettings,
            typeof(AccessibilityController).GetMethod(nameof(AccessibilityController.PutSettings))!.GetCustomAttribute<AuthorizeAttribute>()!.Policy);

    [Fact]
    public void Route_is_esatto_accessibility()
        => Assert.EndsWith("/esatto/accessibility", typeof(AccessibilityController).GetCustomAttribute<VersionedApiBackOfficeRouteAttribute>()!.Template.TrimEnd('/'));
}
