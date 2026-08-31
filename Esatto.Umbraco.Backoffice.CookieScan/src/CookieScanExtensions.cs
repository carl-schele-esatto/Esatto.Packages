using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Esatto.Umbraco.Backoffice.CookieScan;

/// <summary>
/// The two things a host may want to say about the cookie scanner. Everything else is wired by
/// <see cref="CookieScanComposer"/> without being asked.
/// </summary>
public static class CookieScanExtensions
{
    /// <summary>
    /// Binds <see cref="CookieScanApiUserOptions"/> from a section other than the package default.
    /// </summary>
    /// <remarks>
    /// Only needed by a site whose scanner settings already live somewhere else - typically one that
    /// had this endpoint in its own source before the package existed, and whose deployed secret and
    /// production environment variable are named after the old section. Renaming those is a
    /// deployment change; this is a line of code. Call it after <c>AddUmbraco(...).Build()</c>, so it
    /// lands after the composer's own binding and therefore wins.
    /// </remarks>
    public static IServiceCollection ConfigureCookieScanApiUser(
        this IServiceCollection services,
        IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(section);

        return services.Configure<CookieScanApiUserOptions>(section);
    }

    /// <summary>
    /// Creates the cookie scanner's API user and registers its client credentials, if the site is
    /// configured for it. Idempotent, and never throws.
    /// </summary>
    /// <remarks>
    /// Call it after <c>BootUmbracoAsync()</c>: it needs the user service and OpenIddict's
    /// application store, neither of which exists before that. Awaited rather than
    /// fire-and-forget so its log lines land in boot order instead of interleaved with the first
    /// request.
    /// <para>
    /// An async scope, not a sync one, and that distinction is load-bearing: if anything resolved
    /// into the scope is <c>IAsyncDisposable</c>-only, a synchronous <c>Dispose()</c> throws
    /// <em>after</em> the seeder's own catch has already done its job - taking down boot, which is
    /// the one outcome the seeder's never-fatal posture exists to prevent.
    /// </para>
    /// </remarks>
    public static async Task SeedCookieScanApiUserAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using AsyncServiceScope scope = services.CreateAsyncScope();

        await scope.ServiceProvider
            .GetRequiredService<CookieScanApiUserSeeder>()
            .SeedAsync(cancellationToken);
    }
}
