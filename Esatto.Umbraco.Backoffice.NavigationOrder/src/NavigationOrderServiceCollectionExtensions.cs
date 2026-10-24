using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Esatto.Umbraco.Backoffice.NavigationOrder;

public static class NavigationOrderServiceCollectionExtensions
{
    /// <summary>Registers the navigation order store. Idempotent.</summary>
    public static IServiceCollection AddNavigationOrder(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<INavigationOrderStore, NavigationOrderStore>();
        return services;
    }
}
