using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

public class TryDecorateTests
{
    public interface IGreeter
    {
        string Greet();
    }

    private sealed class Greeter : IGreeter
    {
        public string Greet() => "hej";
    }

    private sealed class LoudGreeter(IGreeter inner) : IGreeter
    {
        public string Greet() => inner.Greet().ToUpperInvariant();
    }

    [Fact]
    public void Wraps_a_type_registration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGreeter, Greeter>();

        Assert.True(services.TryDecorate<IGreeter>((_, inner) => new LoudGreeter(inner)));

        using var provider = services.BuildServiceProvider();
        Assert.Equal("HEJ", provider.GetRequiredService<IGreeter>().Greet());
        Assert.Single(services, d => d.ServiceType == typeof(IGreeter));
    }

    [Fact]
    public void Wraps_a_factory_registration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGreeter>(_ => new Greeter());

        services.TryDecorate<IGreeter>((_, inner) => new LoudGreeter(inner));

        using var provider = services.BuildServiceProvider();
        Assert.Equal("HEJ", provider.GetRequiredService<IGreeter>().Greet());
    }

    [Fact]
    public void Does_nothing_when_the_service_is_not_registered()
    {
        var services = new ServiceCollection();

        Assert.False(services.TryDecorate<IGreeter>((_, inner) => new LoudGreeter(inner)));
        Assert.Empty(services);
    }
}
