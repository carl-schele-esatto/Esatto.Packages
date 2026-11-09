using Esatto.Umbraco.Backoffice.Seo.Knowledge;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Seo.Tests;

public class ScaffoldTests
{
    [Fact]
    public void Package_assembly_name_matches_package_id()
        => Assert.Equal("Esatto.Umbraco.Backoffice.Seo", typeof(SeoComposer).Assembly.GetName().Name);

    [Theory]
    [InlineData(typeof(Persistence.ISeoStore), ServiceLifetime.Singleton)]
    [InlineData(typeof(Settings.SettingsService), ServiceLifetime.Singleton)]
    [InlineData(typeof(Settings.SeoEnvironment), ServiceLifetime.Singleton)]
    [InlineData(typeof(Scanning.ScanCoordinator), ServiceLifetime.Singleton)]
    [InlineData(typeof(Scanning.PageFetcher), ServiceLifetime.Singleton)]
    [InlineData(typeof(Reporting.ReportCache), ServiceLifetime.Singleton)]
    [InlineData(typeof(Pages.IPageSource), ServiceLifetime.Scoped)]
    [InlineData(typeof(Scanning.ScanRunner), ServiceLifetime.Scoped)]
    [InlineData(typeof(Scanning.RecheckService), ServiceLifetime.Scoped)]
    [InlineData(typeof(Reporting.ReportService), ServiceLifetime.Scoped)]
    [InlineData(typeof(Reporting.IgnoreService), ServiceLifetime.Scoped)]
    public void Services_are_registered(Type service, ServiceLifetime lifetime)
        => Assert.Equal(lifetime, new ServiceCollection().AddSeoHealth().Single(d => d.ServiceType == service).Lifetime);

    [Fact]
    public void Http_client_does_not_follow_redirects_itself()
    {
        var provider = new ServiceCollection().AddSeoHealth().BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.Extensions.Http.HttpClientFactoryOptions>>()
            .Get(Scanning.PageFetcher.ClientName);
        var builder = Substitute.For<Microsoft.Extensions.Http.HttpMessageHandlerBuilder>();
        builder.Services.Returns(provider);
        foreach (var action in options.HttpMessageHandlerBuilderActions) action(builder);
        var handler = Assert.IsType<System.Net.Http.SocketsHttpHandler>(builder.PrimaryHandler);
        Assert.False(handler.AllowAutoRedirect);
    }

    [Fact]
    public void Composer_registers_services_idempotently()
    {
        var services = new ServiceCollection().AddSeoHealth().AddSeoHealth();
        Assert.Single(services, d => d.ServiceType == typeof(KnowledgeBase));
    }
}
