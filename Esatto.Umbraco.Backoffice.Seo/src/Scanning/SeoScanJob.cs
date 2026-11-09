using Esatto.Umbraco.Backoffice.Seo.Pages;
using Esatto.Umbraco.Backoffice.Seo.Persistence;
using Esatto.Umbraco.Backoffice.Seo.Settings;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace Esatto.Umbraco.Backoffice.Seo.Scanning;

/// <summary>
/// Starts the nightly scan of every site. Wakes every 15 minutes; a site is scanned once per night, in the
/// configured hour (server time). Runs only on the scheduling server, so load-balanced sites scan once.
/// </summary>
public sealed class SeoScanJob(IServiceScopeFactory scopes, SettingsService settings, ISeoStore store, ScanCoordinator coordinator, TimeProvider time)
    : IRecurringBackgroundJob
{
    /// <summary>Two scheduled starts this close together are the same night.</summary>
    private static readonly TimeSpan SameNight = TimeSpan.FromHours(20);

    public TimeSpan Period => TimeSpan.FromMinutes(15);

    public TimeSpan Delay => TimeSpan.FromMinutes(2);

    public ServerRole[] ServerRoles => [ServerRole.Single, ServerRole.SchedulingPublisher];

    // Required by the interface; the period never changes.
    public event EventHandler PeriodChanged { add { } remove { } }

    public async Task RunJobAsync()
    {
        var s = (await settings.GetAsync()).Settings;
        if (!s.Schedule.Enabled) return;

        IReadOnlyList<SiteInfo> sites;
        await using (var scope = scopes.CreateAsyncScope())
            sites = await scope.ServiceProvider.GetRequiredService<IPageSource>().GetSitesAsync();

        var now = time.GetLocalNow();
        foreach (var site in sites)
        {
            var last = (await store.GetScansAsync(site.Key, 20)).FirstOrDefault(x => x.Trigger == ScanTrigger.Scheduled)?.StartedUtc;
            if (ScheduleDue(s, now, last)) coordinator.TryStart(site.Key, ScanTrigger.Scheduled, "Scheduled", requestBase: null);
        }
    }

    public static bool ScheduleDue(SeoSettings s, DateTimeOffset nowLocal, DateTime? lastScheduledStartUtc)
        => s.Schedule.Enabled
            && nowLocal.Hour == s.Schedule.Hour
            && (lastScheduledStartUtc is null || nowLocal.UtcDateTime - lastScheduledStartUtc.Value >= SameNight);
}
