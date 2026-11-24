using Esatto.Umbraco.Backoffice.SeoReport.Jobs;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Umbraco.Cms.Core.Sync;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The scheduled job: it wakes every fifteen minutes and starts a backfill only when the package is set up, automatic fetching is on,
/// something is missing, and the last failure is more than an hour old (or a success came after it). It never throws, because a
/// job that does would be a noise in the scheduler's log every quarter of an hour. The backfill itself is in
/// <see cref="FetchCoordinatorTests"/>; here it runs over a fake report service on a clock the test moves.
/// </summary>
public class SeoReportRefreshJobTests
{
    private static readonly TimeSpan Bounded = TimeSpan.FromSeconds(10);

    private static readonly string[] ThirteenMonths =
        [.. Enumerable.Range(0, 13).Select(i => Period.Parse("2026-07").AddMonths(-i).ToString())];

    private sealed class Rig
    {
        private Rig(SettingsHarness settings, Func<InMemoryStore, ISeoReportStore>? wrapJobStore)
        {
            Settings = settings;
            Coordinator = new FetchCoordinator(Reports, settings.Settings, settings.Store, settings.Time, new CapturingLogger<FetchCoordinator>());
            Job = new SeoReportRefreshJob(settings.Settings, Reports, wrapJobStore?.Invoke(settings.Store) ?? settings.Store, Coordinator, settings.Time, Log);
        }

        public SettingsHarness Settings { get; }

        public InMemoryStore Store => Settings.Store;

        public FakeTimeProvider Time => Settings.Time;

        public FakeReportService Reports { get; } = new();

        public CapturingLogger<SeoReportRefreshJob> Log { get; } = new();

        public FetchCoordinator Coordinator { get; }

        public SeoReportRefreshJob Job { get; }

        /// <summary>A package that is set up, on 2026-08-20 (thirteen months wanted: 2026-07 down to 2025-07).</summary>
        public static async Task<Rig> ReadyAsync(Func<InMemoryStore, ISeoReportStore>? wrapJobStore = null)
            => new(await ReadySettingsHarness.ReadyAsync(), wrapJobStore);

        /// <summary>A package with nothing set up.</summary>
        public static Rig NotConfigured()
        {
            var settings = new SettingsHarness();
            settings.Time.SetUtcNow(ReadySettingsHarness.Now);
            return new Rig(settings, null);
        }

        /// <summary>Whether a wake-up started a run, then waits for it to end (bounded).</summary>
        public async Task<bool> WakeAndWaitAsync()
        {
            await Job.RunJobAsync().WaitAsync(Bounded);
            if (Coordinator.CurrentRun is not { } run) return false;

            await run.WaitAsync(Bounded);
            return true;
        }

        /// <summary>Writes the record of an earlier run, the ages given in seconds before now (null: never).</summary>
        public Task SeedStateAsync(int? failedSecondsAgo, int? succeededSecondsAgo = null)
        {
            var now = Time.GetUtcNow();
            var state = new FetchState(
                LastAttemptUtc: now,
                LastSuccessUtc: succeededSecondsAgo is { } s ? now.AddSeconds(-s) : null,
                LastFailureUtc: failedSecondsAgo is { } f ? now.AddSeconds(-f) : null,
                LastErrorCode: failedSecondsAgo is null ? null : "quota",
                StartedBy: FetchState.Scheduled);
            return Store.SaveDocumentAsync(DocumentKeys.FetchState, state.ToJson());
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The schedule
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_job_wakes_every_fifteen_minutes_five_minutes_after_startup_on_the_scheduling_server_only()
    {
        var job = (await Rig.ReadyAsync()).Job;

        Assert.Equal(TimeSpan.FromMinutes(15), job.Period);
        Assert.Equal(TimeSpan.FromMinutes(5), job.Delay);
        Assert.Equal([ServerRole.Single, ServerRole.SchedulingPublisher], job.ServerRoles);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // When it does nothing
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_package_that_is_not_set_up_is_left_alone_and_said_so_once_per_process()
    {
        var rig = Rig.NotConfigured();

        for (var wake = 0; wake < 3; wake++) Assert.False(await rig.WakeAndWaitAsync());

        Assert.Empty(rig.Reports.Refreshed);
        Assert.Null(rig.Coordinator.Progress);
        var said = Assert.Single(rig.Log.Entries);
        Assert.Equal(LogLevel.Information, said.Level);
    }

    [Fact]
    public async Task Switching_automatic_fetching_off_leaves_the_package_alone_and_says_so_once_per_process()
    {
        var rig = await Rig.ReadyAsync();
        await rig.Settings.Settings.SaveAsync(
            new SeoReportSettings { Ga4PropertyId = ReadySettingsHarness.Ga4Property, SearchConsoleSite = ReadySettingsHarness.Site, AutoFetch = false },
            null,
            false);

        for (var wake = 0; wake < 3; wake++) Assert.False(await rig.WakeAndWaitAsync());

        Assert.Empty(rig.Reports.Refreshed);
        Assert.Equal(LogLevel.Information, Assert.Single(rig.Log.Entries).Level);
    }

    [Fact]
    public async Task Automatic_fetching_switched_off_in_configuration_is_honoured()
    {
        var rig = await Rig.ReadyAsync();
        rig.Settings.Configure("AutoFetch", "false");
        rig.Time.Advance(TimeSpan.FromMinutes(2));   // the settings are cached for a minute

        Assert.False(await rig.WakeAndWaitAsync());

        Assert.Empty(rig.Reports.Refreshed);
    }

    [Fact]
    public async Task A_complete_archive_is_left_alone_without_a_word()
    {
        var rig = await Rig.ReadyAsync();
        rig.Reports.Archive(ThirteenMonths);

        Assert.False(await rig.WakeAndWaitAsync());

        Assert.Empty(rig.Reports.Refreshed);
        Assert.Empty(rig.Log.Entries);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // When it starts a backfill
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Missing_months_start_one_scheduled_backfill_newest_first()
    {
        var rig = await Rig.ReadyAsync();
        rig.Reports.Archive("2026-03");

        Assert.True(await rig.WakeAndWaitAsync());

        Assert.Equal(ThirteenMonths.Where(m => m != "2026-03"), rig.Reports.Refreshed.Select(p => p.ToString()));
        Assert.Equal(FetchState.Scheduled, FetchState.Parse(rig.Store.Documents[DocumentKeys.FetchState]).StartedBy);
        Assert.Empty(rig.Log.Entries);
    }

    [Fact]
    public async Task A_wake_while_a_backfill_is_still_going_does_not_start_another()
    {
        var rig = await Rig.ReadyAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Reports.OnRefresh = async (_, _) =>
        {
            entered.TrySetResult();
            await release.Task;
        };
        await rig.Job.RunJobAsync().WaitAsync(Bounded);
        await entered.Task.WaitAsync(Bounded);

        await rig.Job.RunJobAsync().WaitAsync(Bounded);   // the next wake-up, while the first run is busy
        release.SetResult();
        await rig.Coordinator.CurrentRun!.WaitAsync(Bounded);

        Assert.Equal(ThirteenMonths, rig.Reports.Refreshed.Select(p => p.ToString()));   // one run, each month once
        Assert.DoesNotContain(rig.Log.Entries, e => e.Level >= LogLevel.Warning);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The hold after a failure
    // ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, false)]
    [InlineData(300, false)]
    [InlineData(3599, false)]   // not an hour yet
    [InlineData(3600, true)]    // exactly an hour is allowed to run
    [InlineData(3601, true)]
    [InlineData(86400, true)]
    public async Task A_failure_less_than_an_hour_old_holds_the_job_back(int failedSecondsAgo, bool runs)
    {
        var rig = await Rig.ReadyAsync();
        await rig.SeedStateAsync(failedSecondsAgo);

        Assert.Equal(runs, await rig.WakeAndWaitAsync());

        Assert.Equal(runs ? 13 : 0, rig.Reports.Refreshed.Count);
    }

    [Theory]
    [InlineData(300, 60, true)]     // a success after the failure ends the hold
    [InlineData(300, 300, false)]   // at the same moment is not after (a run that failed one month and archived others)
    [InlineData(300, 600, false)]   // a success before the failure does not
    public async Task Only_a_success_after_the_failure_lifts_the_hold(int failedSecondsAgo, int succeededSecondsAgo, bool runs)
    {
        var rig = await Rig.ReadyAsync();
        await rig.SeedStateAsync(failedSecondsAgo, succeededSecondsAgo);

        Assert.Equal(runs, await rig.WakeAndWaitAsync());
    }

    [Fact]
    public async Task A_month_that_keeps_failing_with_an_argument_error_inside_the_range_is_not_fetched_again_at_every_wake_up()
    {
        var rig = await Rig.ReadyAsync();
        rig.Reports.OnRefresh = (period, _) => period == Period.Parse("2026-05")
            ? Task.FromException(new ArgumentOutOfRangeException("period", "a bug, not the service's range check"))
            : Task.CompletedTask;
        await rig.WakeAndWaitAsync();
        Assert.Equal(13, rig.Reports.Refreshed.Count);   // 2026-05 failed, the other twelve are archived

        // The month is still missing, so every wake-up sees work to do. Within the hour it must leave it alone.
        rig.Time.Advance(TimeSpan.FromMinutes(15));
        await rig.WakeAndWaitAsync();
        rig.Time.Advance(TimeSpan.FromMinutes(15));
        await rig.WakeAndWaitAsync();
        Assert.Equal(13, rig.Reports.Refreshed.Count);

        rig.Time.Advance(TimeSpan.FromMinutes(31));   // an hour and a minute after the failure
        await rig.WakeAndWaitAsync();
        Assert.Equal(["2026-05"], rig.Reports.Refreshed.Skip(13).Select(p => p.ToString()));   // only the missing month is tried again
    }

    [Fact]
    public async Task A_failure_stamped_in_the_future_does_not_hold_the_job_back()
    {
        var rig = await Rig.ReadyAsync();
        await rig.SeedStateAsync(failedSecondsAgo: -7200);

        Assert.True(await rig.WakeAndWaitAsync());
    }

    [Fact]
    public async Task A_record_that_cannot_be_read_does_not_hold_the_job_back()
    {
        var rig = await Rig.ReadyAsync();
        await rig.Store.SaveDocumentAsync(DocumentKeys.FetchState, "{ this is not json");

        Assert.True(await rig.WakeAndWaitAsync());
    }

    // ---------------------------------------------------------------------------------------------------------------
    // It never throws
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_store_that_cannot_be_read_is_a_warning_and_not_an_exception()
    {
        FlakyStore? flaky = null;
        var rig = await Rig.ReadyAsync(inner => flaky = new FlakyStore(inner));
        flaky!.GetDocumentFails = new InvalidOperationException("the database does not answer");

        Assert.False(await rig.WakeAndWaitAsync());

        Assert.Null(rig.Coordinator.CurrentRun);
        Assert.Contains(rig.Log.Entries, e => e.Level == LogLevel.Warning && e.Text.Contains("InvalidOperationException"));
    }

    [Fact]
    public async Task A_report_service_that_fails_is_a_warning_and_not_an_exception()
    {
        var rig = await Rig.ReadyAsync();
        rig.Reports.NeedsFetchFails = new InvalidOperationException("the archive cannot be read");

        Assert.False(await rig.WakeAndWaitAsync());

        Assert.Contains(rig.Log.Entries, e => e.Level == LogLevel.Warning);
    }
}
