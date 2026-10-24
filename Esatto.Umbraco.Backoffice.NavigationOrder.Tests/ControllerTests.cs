using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.Common.Authorization;
using Xunit;

namespace Esatto.Umbraco.Backoffice.NavigationOrder.Tests;

public class ControllerTests
{
    private readonly INavigationOrderStore _store = Substitute.For<INavigationOrderStore>();
    private readonly IBackOfficeSecurityAccessor _security = Substitute.For<IBackOfficeSecurityAccessor>();

    private NavigationOrderController Controller() => new(_store, _security);

    private static readonly StoredNavigationOrder Stored = new(
        new NavigationOrderSettings(["B", "A"], [new TabOrder("Umb.Section.Settings", ["X", "Y"])]),
        new DateTime(2026, 10, 24, 8, 0, 0, DateTimeKind.Utc),
        "Carl");

    [Fact]
    public void Get_returns_the_stored_order()
    {
        _store.Get().Returns(Stored);

        var result = Assert.IsType<OkObjectResult>(Controller().Get());

        var response = Assert.IsType<NavigationOrderResponse>(result.Value);
        Assert.Equal(["B", "A"], response.Sections);
        var tab = Assert.Single(response.Tabs);
        Assert.Equal("Umb.Section.Settings", tab.Section);
        Assert.Equal(["X", "Y"], tab.Aliases);
        Assert.Equal(Stored.UpdatedUtc, response.UpdatedUtc);
        Assert.Equal("Carl", response.UpdatedBy);
    }

    [Fact]
    public void Save_rejects_an_invalid_order_without_storing()
    {
        var result = Controller().Save(new NavigationOrderRequest(["A", "A"], null));

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<BadRequestObjectResult>(result).Value);
        Assert.Contains("Duplicate alias", problem.Detail);
        _store.DidNotReceiveWithAnyArgs().Save(default!, default);
    }

    [Fact]
    public void Save_stores_with_the_current_user()
    {
        var user = Substitute.For<IUser>();
        user.Name.Returns("Carl");
        _security.BackOfficeSecurity!.CurrentUser.Returns(user);
        _store.Save(default!, default).ReturnsForAnyArgs(Stored);

        var result = Controller().Save(new NavigationOrderRequest(["B", "A"], [new TabOrderDto("Umb.Section.Settings", ["X", "Y"])]));

        _store.Received(1).Save(
            Arg.Is<NavigationOrderSettings>(s => s.Sections!.SequenceEqual(new[] { "B", "A" }) && s.Tabs.Single().Aliases.SequenceEqual(new[] { "X", "Y" })),
            "Carl");
        var response = Assert.IsType<NavigationOrderResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("Carl", response.UpdatedBy);
    }

    [Fact]
    public void Missing_tabs_mean_no_saved_tab_order() => Assert.Empty(new NavigationOrderRequest(null, null).ToSettings().Tabs);

    [Fact]
    public void Only_admins_can_save()
    {
        var save = typeof(NavigationOrderController).GetMethod(nameof(NavigationOrderController.Save))!;
        Assert.Equal(AuthorizationPolicies.RequireAdminAccess, save.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal(AuthorizationPolicies.BackOfficeAccess, typeof(NavigationOrderController).GetCustomAttribute<AuthorizeAttribute>(inherit: false)?.Policy);
    }
}
