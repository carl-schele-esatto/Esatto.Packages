using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Jobs;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The registrations, checked by a container that behaves like the host's strictest one: <c>ValidateScopes</c> and
/// <c>ValidateOnBuild</c> on, so a singleton that depends on a scoped service (a captive dependency, which works on a developer's
/// machine and misbehaves under load) fails to build. Only the host's own services are faked: the database scope provider, Data
/// Protection, the application lifetime and the services of the signed-in user.
/// </summary>
public class DependencyInjectionTests
{
    private static readonly ServiceProviderOptions Strict = new() { ValidateScopes = true, ValidateOnBuild = true };

    /// <summary>What Umbraco and ASP.NET Core provide, as the package finds them. The users' services are scoped, as the host's are.</summary>
    private static ServiceCollection Host(IHostApplicationLifetime? lifetime = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddSingleton(Substitute.For<IScopeProvider>());
        services.AddSingleton(lifetime ?? Substitute.For<IHostApplicationLifetime>());
        services.AddScoped(_ => Substitute.For<IBackOfficeSecurityAccessor>());
        services.AddScoped(_ => Substitute.For<IUserGroupService>());
        return services;
    }

    private static ServiceProvider Build(Action<ServiceCollection>? before = null, Action<ServiceCollection>? after = null)
    {
        var services = Host();
        before?.Invoke(services);
        services.AddSeoReport();
        services.AddRecurringBackgroundJob<SeoReportRefreshJob>();
        after?.Invoke(services);
        return services.BuildServiceProvider(Strict);
    }

    public static IEnumerable<object[]> Services =>
    [
        [typeof(ISeoReportStore)],
        [typeof(SecretProtector)],
        [typeof(SettingsService)],
        [typeof(GoogleCredentialSource)],
        [typeof(IGoogleCredentialSource)],
        [typeof(IGoogleClientFactory)],
        [typeof(IGoogleClients)],
        [typeof(ISeoReportService)],
        [typeof(INoteService)],
        [typeof(ITargetQueryService)],
        [typeof(IDailyTrafficService)],
        [typeof(ConnectionTester)],
        [typeof(FetchCoordinator)],
        [typeof(IFetchCoordinator)],
        [typeof(TimeProvider)],
    ];

    [Fact]
    public async Task The_package_caches_in_its_own_memory_cache_so_a_host_cache_with_a_size_limit_breaks_nothing()
    {
        // A host may give its IMemoryCache a SizeLimit; every Set without a size then throws. The package never writes to that cache.
        using var hostCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1 });
        using var provider = Build(before: services =>
        {
            services.AddSingleton<IMemoryCache>(hostCache);
            services.AddSingleton<ISeoReportStore>(new InMemoryStore());
        });
        var reports = provider.GetRequiredService<ISeoReportService>();

        Assert.Null(await reports.GetReportAsync());
        Assert.Empty(await reports.GetAvailablePeriodsAsync());
        Assert.Equal(0, hostCache.Count);
        Assert.Same(hostCache, provider.GetRequiredService<IMemoryCache>());
    }

    [Fact]
    public void The_package_does_not_register_a_memory_cache_for_the_host()
    {
        using var provider = Build();

        Assert.Null(provider.GetService<IMemoryCache>());
    }

    [Theory]
    [MemberData(nameof(Services))]
    public void Each_service_resolves_from_a_scope_and_from_the_root_as_a_singleton(Type service)
    {
        using var provider = Build();
        using var scope = provider.CreateScope();

        // From the root with ValidateScopes on: a scoped registration would throw here, and a transient would be a new one each time.
        var fromRoot = provider.GetRequiredService(service);

        Assert.Same(fromRoot, provider.GetRequiredService(service));
        Assert.Same(fromRoot, scope.ServiceProvider.GetRequiredService(service));
    }

    [Fact]
    public void The_controller_and_the_job_resolve_with_everything_they_need()
    {
        using var provider = Build();
        using var scope = provider.CreateScope();

        var controller = ActivatorUtilities.CreateInstance<SeoReportController>(scope.ServiceProvider);
        var jobs = provider.GetServices<IRecurringBackgroundJob>().ToList();

        Assert.NotNull(controller);
        Assert.IsType<SeoReportRefreshJob>(Assert.Single(jobs));
    }

    [Fact]
    public async Task The_credential_source_shares_the_one_settings_service_so_a_saved_key_works_at_once()
    {
        var store = new InMemoryStore();
        using var provider = Build(before: services => services.AddSingleton<ISeoReportStore>(store));
        var settings = provider.GetRequiredService<SettingsService>();
        var credentials = provider.GetRequiredService<IGoogleCredentialSource>();

        Assert.Same(provider.GetRequiredService<GoogleCredentialSource>(), credentials); // the interface is the very same instance

        // The credential source caches for a minute and learns of a save through the settings service's generation. With a
        // settings service of its own it would go on answering "not configured" until the minute was up.
        Assert.Equal(ErrorCodes.NotConfigured, (await credentials.GetAsync()).ErrorCode);
        await settings.SaveAsync(new SeoReportSettings(), FakeServiceAccount.Json(), removeKey: false);

        var after = await credentials.GetAsync();
        Assert.Null(after.ErrorCode);
        Assert.Equal(FakeServiceAccount.DefaultClientEmail, after.ClientEmail);
    }

    [Fact]
    public void There_is_one_of_each_service_that_holds_state_in_memory()
    {
        using var provider = Build();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        // The report gate and caches, the backfill flag and progress, Test connection's single flight and replay window, the settings cache.
        foreach (var type in new[] { typeof(ISeoReportService), typeof(FetchCoordinator), typeof(ConnectionTester), typeof(SettingsService), typeof(IDailyTrafficService) })
        {
            Assert.Same(first.ServiceProvider.GetRequiredService(type), second.ServiceProvider.GetRequiredService(type));
        }

        // The interface the controller asks for is the coordinator the job asks for.
        Assert.Same(provider.GetRequiredService<FetchCoordinator>(), provider.GetRequiredService<IFetchCoordinator>());
    }

    [Fact]
    public void Registering_twice_changes_nothing()
    {
        var services = Host();
        services.AddSeoReport();
        var once = services.Select(d => (d.ServiceType, d.ImplementationType, d.Lifetime)).ToList();

        services.AddSeoReport();

        Assert.Equal(once, services.Select(d => (d.ServiceType, d.ImplementationType, d.Lifetime)).ToList());
        services.BuildServiceProvider(Strict).Dispose();
    }

    [Fact]
    public void A_clock_the_host_registered_is_the_one_used()
    {
        var clock = new FakeTimeProvider();
        using var provider = Build(before: services => services.AddSingleton<TimeProvider>(clock));

        Assert.Same(clock, provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public async Task The_hosts_lifetime_reaches_the_coordinator_so_a_stopping_application_ends_the_run()
    {
        // The application is already stopping. A coordinator that was not given the lifetime would carry on and fetch every month from
        // the (fake, offline) Google; one that was ends its run before the first month.
        using var stopping = new CancellationTokenSource();
        await stopping.CancelAsync();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(stopping.Token);

        var store = new InMemoryStore();
        var offline = Substitute.For<IGoogleClientFactory>();
        offline.Create(Arg.Any<GoogleCredential>()).Returns(new SplitGoogle(new FakeAnalyticsData(new FakeGoogleTransport()), new FakeSearchConsole(new FakeGoogleTransport())));

        var services = Host(lifetime);
        services.AddSingleton<ISeoReportStore>(store);
        services.AddSingleton(offline);
        services.AddSeoReport();
        await using var provider = services.BuildServiceProvider(Strict);

        await provider.GetRequiredService<SettingsService>().SaveAsync(
            new SeoReportSettings { Ga4PropertyId = ReadySettingsHarness.Ga4Property, SearchConsoleSite = ReadySettingsHarness.Site },
            FakeServiceAccount.Json(),
            removeKey: false);
        var coordinator = provider.GetRequiredService<FetchCoordinator>();

        Assert.Equal(FetchStartResult.Started, await coordinator.TryStartAsync(FetchState.Manual));
        await coordinator.CurrentRun!.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.False(coordinator.Progress!.Running);
        Assert.Equal(0, coordinator.Progress.Done);
        Assert.Empty(await store.ListPeriodsAsync());
    }

    [Fact]
    public void A_singleton_that_depends_on_a_scoped_service_fails_to_build_which_is_what_this_harness_is_for()
    {
        // Control: the store registered as SCOPED first (TryAdd keeps it) makes every singleton that needs it a captive dependency.
        var services = Host();
        services.AddScoped<ISeoReportStore>(_ => new InMemoryStore());
        services.AddSeoReport();

        var failure = Assert.ThrowsAny<Exception>(() => services.BuildServiceProvider(Strict));

        Assert.Contains("scoped", failure.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_service_that_is_missing_from_the_host_fails_to_build_too()
    {
        // Control: without Data Protection nothing here may build, so a passing test above means the fakes really are what is needed.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(Substitute.For<IScopeProvider>());
        services.AddSeoReport();

        Assert.ThrowsAny<Exception>(() => services.BuildServiceProvider(Strict));
    }
}
