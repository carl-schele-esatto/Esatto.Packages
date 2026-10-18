using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Esatto.Umbraco.Backoffice.AICosts;

public static class AICostsServiceCollectionExtensions
{
    /// <summary>Registers the AI cost services and binds <see cref="AICostOptions"/>. Idempotent.</summary>
    public static IServiceCollection AddAICosts(this IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(AICostQueries))) return services;

        services.AddOptions<AICostOptions>().BindConfiguration(AICostOptions.SectionName);
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient(EcbExchangeRateProvider.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10));
        // Singleton so the rate cache survives across requests; HttpClients come from the factory per fetch.
        services.AddSingleton<IExchangeRateProvider, EcbExchangeRateProvider>();
        services.AddScoped<AICostQueries>();
        services.AddScoped<AICostReportService>();
        services.AddScoped<AICostBreakdownService>();
        return services;
    }
}
