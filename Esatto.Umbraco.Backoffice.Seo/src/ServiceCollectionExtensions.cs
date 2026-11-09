using System.Net;
using Esatto.Umbraco.Backoffice.Seo.Knowledge;
using Esatto.Umbraco.Backoffice.Seo.Pages;
using Esatto.Umbraco.Backoffice.Seo.Persistence;
using Esatto.Umbraco.Backoffice.Seo.Reporting;
using Esatto.Umbraco.Backoffice.Seo.Scanning;
using Esatto.Umbraco.Backoffice.Seo.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Esatto.Umbraco.Backoffice.Seo;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the SEO Health services. Called by <see cref="SeoComposer"/>; idempotent.</summary>
    public static IServiceCollection AddSeoHealth(this IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(KnowledgeBase))) return services;

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<KnowledgeBase>();
        services.AddSingleton<ISeoStore, NPocoSeoStore>();
        // Singletons: settings cache, report cache, running scans and their progress live across requests.
        services.AddSingleton<SettingsService>();
        services.AddSingleton<SeoEnvironment>();
        services.AddSingleton<ReportCache>();
        services.AddSingleton<ScanRowLock>();
        services.AddSingleton<ScanCoordinator>();
        services.AddSingleton<PageFetcher>();
        services.AddSingleton<LinkChecker>();
        services.AddSingleton<SiteProber>();

        // Scoped: they read Umbraco's content (published cache, navigation) or depend on something that does.
        services.AddScoped<IPageSource, SitePageSource>();
        services.AddScoped<SnapshotLoader>();
        services.AddScoped<ScanRunner>();
        services.AddScoped<RecheckService>();
        services.AddScoped<ReportService>();
        services.AddScoped<IgnoreService>();

        // Redirects are followed by PageFetcher (to record the chain); timeouts are per request there too.
        services.AddHttpClient(PageFetcher.ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            })
            .ConfigureHttpClient(client => client.Timeout = Timeout.InfiniteTimeSpan);

        return services;
    }
}
