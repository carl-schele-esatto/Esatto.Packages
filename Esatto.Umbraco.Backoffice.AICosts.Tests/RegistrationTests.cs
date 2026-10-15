using Esatto.Umbraco.Backoffice.AICosts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Esatto.Umbraco.Backoffice.AICosts.Tests;

public class RegistrationTests
{
    private static ServiceProvider Build(int registrations = 1)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Esatto:AICosts:DefaultCurrency"] = "EUR",
        }).Build());
        services.AddSingleton(Usage.EmptyAnalytics());
        for (var i = 0; i < registrations; i++) services.AddAICosts();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Resolves_the_services_and_binds_the_section()
    {
        using var provider = Build();
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AICostReportService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AICostBreakdownService>());
        Assert.IsType<EcbExchangeRateProvider>(provider.GetRequiredService<IExchangeRateProvider>());
        Assert.Equal("EUR", provider.GetRequiredService<IOptionsMonitor<AICostOptions>>().CurrentValue.DefaultCurrency);
    }

    [Fact]
    public void Registering_twice_registers_once()
    {
        using var provider = Build(registrations: 2);
        Assert.Single(provider.GetServices<IExchangeRateProvider>());
    }
}
