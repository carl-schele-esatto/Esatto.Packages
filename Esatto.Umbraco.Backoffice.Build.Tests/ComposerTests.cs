using Esatto.Umbraco.Backoffice.Build.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core.Configuration;
using Umbraco.Cms.Core.DependencyInjection;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Build.Tests;

public sealed class ComposerTests
{
    private static ServiceCollection Compose(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        var builder = Substitute.For<IUmbracoBuilder>();
        builder.Services.Returns(services);
        builder.Config.Returns(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

        new BuildComposer().Compose(builder);

        return services;
    }

    [Fact]
    public void Options_are_bound_from_the_esatto_build_section()
    {
        var services = Compose(new()
        {
            ["Esatto:Build:FilePath"] = "x.json",
            ["Esatto:Build:Packages:0"] = "Example.Tools",
            ["Esatto:Build:Packages:1"] = "Contoso.*",
        });
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<BuildOptions>>().Value;

        Assert.Equal("x.json", options.FilePath);
        Assert.Equal(["Example.Tools", "Contoso.*"], options.Packages);
    }

    [Fact]
    public void Defaults_apply_when_the_section_is_absent()
    {
        using var provider = Compose([]).BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<BuildOptions>>().Value;

        Assert.Equal("build-info.json", options.FilePath);
        Assert.Empty(options.Packages);
    }

    [Fact]
    public void The_service_is_a_singleton_that_the_container_can_build()
    {
        var services = Compose([]);
        services.AddLogging();
        services.AddSingleton(ServiceRig.Host(AppContext.BaseDirectory));
        services.AddSingleton(Substitute.For<IUmbracoVersion>());

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(BuildInfoService));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);

        // Only the public constructor is visible to the container; the internal one is the test seam.
        using var provider = services.BuildServiceProvider();
        Assert.Same(provider.GetRequiredService<BuildInfoService>(), provider.GetRequiredService<BuildInfoService>());
    }
}
