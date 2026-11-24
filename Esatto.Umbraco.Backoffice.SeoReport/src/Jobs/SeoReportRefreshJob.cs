using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace Esatto.Umbraco.Backoffice.SeoReport.Jobs;

/// <summary>
/// The safety net behind "Fetch now" and the backfill that saving settings starts: wakes every 15 minutes (five minutes after startup)
/// and asks "which months are missing?", never "is the data old?". Only when some wanted month needs a fetch does it start a backfill
/// (<see cref="FetchCoordinator"/>, which is what keeps it to one run at a time, here and on other instances). Runs on the scheduling
/// server only, so a load-balanced site has one job.
///
/// IT STAYS QUIET. Not set up, or automatic fetching switched off (in the settings or in configuration): it does nothing, and says so
/// once per process at Information, not every quarter of an hour. A failure that is less than an hour old, with no success after it,
/// holds it back (see <see cref="FetchState.IsBackingOff"/>): a failing month is not retried more often than hourly. A person pressing
/// Fetch now is not held back. It never throws: an exception in a job is noise in the scheduler's log every wake-up, so it is a Warning
/// (with the exception: the job reads only the settings, the archive and the record of the last run, and the settings layer never lets
/// a key text into an exception) and the next wake-up tries again.
/// </summary>
public sealed class SeoReportRefreshJob(
    SettingsService settings,
    ISeoReportService reports,
    ISeoReportStore store,
    FetchCoordinator coordinator,
    TimeProvider time,
    ILogger<SeoReportRefreshJob> logger) : IRecurringBackgroundJob
{
    private int _idleSaid;

    public TimeSpan Period => TimeSpan.FromMinutes(15);

    public TimeSpan Delay => TimeSpan.FromMinutes(5);

    public ServerRole[] ServerRoles => [ServerRole.Single, ServerRole.SchedulingPublisher];

    // Required by the interface; the period never changes.
    public event EventHandler PeriodChanged { add { } remove { } }

    public async Task RunJobAsync()
    {
        try
        {
            await WakeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The scheduled SEO Report refresh failed with {ExceptionType}; it tries again at the next wake-up.", ex.GetType().Name);
        }
    }

    private async Task WakeAsync()
    {
        if (await settings.GetSetupStateAsync().ConfigureAwait(false) != SetupState.Ready
            || !(await settings.GetAsync().ConfigureAwait(false)).Settings.AutoFetch)
        {
            SayIdleOnce();
            return;
        }

        if ((await FetchCoordinator.DueAsync(reports, time).ConfigureAwait(false)).Count == 0) return;

        var state = FetchState.Parse(await store.GetDocumentAsync(DocumentKeys.FetchState).ConfigureAwait(false));
        if (state.IsBackingOff(time.GetUtcNow()))
        {
            logger.LogDebug("Months are missing, but the last fetch failed less than an hour ago ({Code}): waiting.", state.LastErrorCode);
            return;
        }

        // The coordinator decides whether it can run (it may be busy, here or on another instance); either way there is nothing more to do.
        await coordinator.TryStartAsync(FetchState.Scheduled).ConfigureAwait(false);
    }

    private void SayIdleOnce()
    {
        if (Interlocked.Exchange(ref _idleSaid, 1) != 0) return;

        logger.LogInformation("The SEO Report is not set up, or automatic fetching is switched off: the scheduled refresh does nothing. Fetch now still works once it is set up.");
    }
}
