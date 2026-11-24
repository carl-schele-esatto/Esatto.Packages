using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Jobs;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Esatto.Umbraco.Backoffice.SeoReport;

public static class ServiceCollectionExtensions
{
    /// <summary>The service key of the package's own memory cache (see <see cref="AddSeoReport"/>).</summary>
    public const string CacheKey = "Esatto.Umbraco.Backoffice.SeoReport";

    /// <summary>
    /// Registers the SEO Report services. Called by <see cref="SeoReportComposer"/>; idempotent (every registration is a
    /// <c>TryAdd</c>, so calling it twice, or a host that registered one of these first, changes nothing).
    ///
    /// EVERYTHING IS A SINGLETON, and each for a reason: the settings cache, the report gate and caches, the single flight of Test
    /// connection, the backfill's flag and progress, and the Google clients live across requests and only work if every request shares
    /// them. The scoped services of Umbraco's content (the published cache, navigation) are not used here: the data is Google's and the
    /// package's own tables. The store is a singleton too, because the singletons need it and a singleton cannot depend on a scoped
    /// service: it opens a scope of its own for each statement from Umbraco's scope provider, which is a singleton (as in SEO Health).
    ///
    /// THE CACHE IS THE PACKAGE'S OWN: a <see cref="MemoryCache"/> registered under <see cref="CacheKey"/>, never the host's
    /// <c>IMemoryCache</c>. A host may give its cache a size limit, and then every entry written without a size throws: the dashboard
    /// would fail on every read.
    ///
    /// The host provides what is not here: Data Protection (the key's encryption), the scope provider, <c>IHostApplicationLifetime</c>
    /// (the coordinator uses it to stop a backfill when the application stops) and the services of the signed-in user. Not registered
    /// here: the controller (discovered by MVC) and the refresh job (the composer).
    /// </summary>
    public static IServiceCollection AddSeoReport(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddKeyedSingleton<IMemoryCache>(CacheKey, (_, _) => new MemoryCache(new MemoryCacheOptions()));

        services.TryAddSingleton<ISeoReportStore, NPocoSeoReportStore>();

        // Settings and the key.
        services.TryAddSingleton<SecretProtector>();
        services.TryAddSingleton<SettingsService>();

        // THE CREDENTIAL SOURCE SHARES THE ONE SETTINGS SERVICE, which it resolves from here: it learns that a key was saved or removed
        // from that service's generation, so a source with a settings service of its own would go on handing out the old credential.
        services.TryAddSingleton<GoogleCredentialSource>();
        services.TryAddSingleton<IGoogleCredentialSource>(provider => provider.GetRequiredService<GoogleCredentialSource>());
        services.TryAddSingleton<IGoogleClientFactory, GoogleClientFactory>();
        services.TryAddSingleton<IGoogleClients, GoogleClients>();

        // The archive, the notes, the targets and the day-fresh traffic.
        services.TryAddSingleton<ISeoReportService, SeoReportService>();
        services.TryAddSingleton<INoteService, NoteService>();
        services.TryAddSingleton<ITargetQueryService, TargetQueryService>();
        services.TryAddSingleton<IDailyTrafficService, DailyTrafficService>();

        // Test connection, and the backfill. The interface the controller asks for is the very same coordinator the job asks for.
        services.TryAddSingleton<ConnectionTester>();
        services.TryAddSingleton<FetchCoordinator>();
        services.TryAddSingleton<IFetchCoordinator>(provider => provider.GetRequiredService<FetchCoordinator>());

        return services;
    }
}
