using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Umbraco.Cms.Infrastructure.Scoping;
using Xunit;

namespace Esatto.Umbraco.Backoffice.NavigationOrder.Tests;

public class RegistrationTests
{
    [Fact]
    public void AddNavigationOrder_is_idempotent()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IScopeProvider>());

        services.AddNavigationOrder();
        services.AddNavigationOrder();

        Assert.Single(services, d => d.ServiceType == typeof(INavigationOrderStore));
        using var provider = services.BuildServiceProvider();
        Assert.IsType<NavigationOrderStore>(provider.GetRequiredService<INavigationOrderStore>());
    }
}
