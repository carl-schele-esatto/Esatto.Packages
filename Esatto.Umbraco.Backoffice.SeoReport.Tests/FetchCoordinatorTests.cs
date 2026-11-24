using Esatto.Umbraco.Backoffice.SeoReport.Jobs;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The backfill: which months it wants, that one run happens at a time (in this process, and across instances through the database
/// lock), its progress, and what its record in the store says. The failures and the shutdown are in
/// <c>FetchCoordinatorTests.Failures.cs</c>, the lock in <c>FetchCoordinatorTests.Lock.cs</c>. Every run is made deterministic: the
/// fake report service is gated or instant, time is a <see cref="FakeTimeProvider"/>, and every wait is bounded, so a run that never
/// ends or a lock that is never released FAILS a test instead of hanging it.
/// </summary>
public partial class FetchCoordinatorTests
{
    private static readonly TimeSpan Bounded = TimeSpan.FromSeconds(10);

    /// <summary>July 2026 down to July 2025: the thirteen months wanted on <see cref="ReadySettingsHarness.Now"/>.</summary>
    private static readonly string[] ThirteenMonths =
        [.. Enumerable.Range(0, 13).Select(i => Period.Parse("2026-07").AddMonths(-i).ToString())];

    private static TaskCompletionSource NewGate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class FakeLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _stopping = new();

        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => _stopping.Token;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => _stopping.Cancel();
    }

    /// <summary>
    /// The coordinator over an in-memory store, a fake report service and a clock on <see cref="ReadySettingsHarness.Now"/> (2026-08-20,
    /// when July is the latest month that should be archived, so thirteen months are wanted: 2026-07 down to 2025-07).
    /// </summary>
    private sealed class Rig
    {
        private Rig(SettingsHarness settings, Func<InMemoryStore, ISeoReportStore>? wrap = null)
        {
            Settings = settings;
            Coordinator = new FetchCoordinator(Reports, settings.Settings, wrap?.Invoke(settings.Store) ?? settings.Store, settings.Time, Log, Lifetime);
        }

        public SettingsHarness Settings { get; }

        public InMemoryStore Store => Settings.Store;

        public FakeTimeProvider Time => Settings.Time;

        public FakeReportService Reports { get; } = new();

        public CapturingLogger<FetchCoordinator> Log { get; } = new();

        public FakeLifetime Lifetime { get; } = new();

        public FetchCoordinator Coordinator { get; }

        /// <summary>A package that is set up (a key and both properties), so a run can start.</summary>
        public static async Task<Rig> ReadyAsync(Func<InMemoryStore, ISeoReportStore>? wrap = null)
            => new(await ReadySettingsHarness.ReadyAsync(), wrap);

        /// <summary>A package with nothing set up.</summary>
        public static Rig NotConfigured()
        {
            var settings = new SettingsHarness();
            settings.Time.SetUtcNow(ReadySettingsHarness.Now);
            return new Rig(settings);
        }

        /// <summary>A rig over settings the test has prepared itself (its clock is not touched).</summary>
        public static Rig Over(SettingsHarness settings) => new(settings);

        /// <summary>Waits for the run that was started to end. Bounded: a run that never ends fails the test.</summary>
        public Task FinishedAsync()
            => (Coordinator.CurrentRun ?? throw new InvalidOperationException("No run was started.")).WaitAsync(Bounded);

        /// <summary>Starts a run and waits for it to end.</summary>
        public async Task<FetchStartResult> RunAsync(string startedBy = "Test User")
        {
            var result = Coordinator.TryStart(startedBy);
            if (result == FetchStartResult.Started) await FinishedAsync();
            return result;
        }

        public async Task<FetchState> StateAsync() => FetchState.Parse(await Store.GetDocumentAsync(DocumentKeys.FetchState));

        /// <summary>Whether another instance could take the database lock right now (and, when it could, lets it go again).</summary>
        public async Task<bool> LockIsFreeAsync()
        {
            if (!await Store.TryAcquireLockAsync("fetch", "probe", TimeSpan.FromMinutes(1))) return false;

            await Store.ReleaseLockAsync("fetch", "probe");
            return true;
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The months that are wanted
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_wanted_months_are_the_thirteen_ending_at_the_expected_latest_newest_first()
    {
        var wanted = FetchCoordinator.WantedPeriods(Period.Parse("2026-10"), new DateOnly(2026, 11, 19));

        Assert.Equal(13, wanted.Count);
        Assert.Equal(Period.Parse("2026-10"), wanted[0]);
        Assert.Equal(Period.Parse("2025-10"), wanted[^1]);
        Assert.Equal(wanted.OrderByDescending(p => p), wanted);
        Assert.Equal(13, wanted.Distinct().Count());
    }

    // The window of Search Console is sixteen months counted from today's month, so its first month moves up with the date. The
    // thirteenth wanted month (2025-10) is the first one inside it on 2027-01-31 and falls out of it a month later.
    [Theory]
    [InlineData(2026, 11, 19, 13, "2025-10")]
    [InlineData(2027, 1, 31, 13, "2025-10")]
    [InlineData(2027, 2, 1, 12, "2025-11")]
    [InlineData(2027, 3, 1, 11, "2025-12")]
    [InlineData(2028, 1, 1, 1, "2026-10")]
    public void The_wanted_months_stop_at_the_first_month_search_console_still_has(int year, int month, int day, int count, string oldest)
    {
        var wanted = FetchCoordinator.WantedPeriods(Period.Parse("2026-10"), new DateOnly(year, month, day));

        Assert.Equal(count, wanted.Count);
        Assert.Equal(Period.Parse("2026-10"), wanted[0]);
        Assert.Equal(Period.Parse(oldest), wanted[^1]);
    }

    [Fact]
    public void Nothing_is_wanted_when_even_the_expected_latest_is_out_of_the_window()
        => Assert.Empty(FetchCoordinator.WantedPeriods(Period.Parse("2026-10"), new DateOnly(2028, 3, 1)));

    // ---------------------------------------------------------------------------------------------------------------
    // A run
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task There_is_no_progress_before_anything_ran()
    {
        var rig = await Rig.ReadyAsync();

        Assert.Null(rig.Coordinator.Progress);
    }

    [Fact]
    public async Task A_run_fetches_the_missing_months_newest_first_and_reports_how_far_it_is()
    {
        var rig = await Rig.ReadyAsync();
        var seen = new List<FetchProgress?>();
        rig.Reports.OnRefresh = (_, _) =>
        {
            seen.Add(rig.Coordinator.Progress);
            return Task.CompletedTask;
        };

        Assert.Equal(FetchStartResult.Started, await rig.RunAsync());

        Assert.Equal(ThirteenMonths, rig.Reports.Refreshed.Select(p => p.ToString()));
        Assert.Equal(
            ThirteenMonths.Select((month, done) => new FetchProgress(true, done, 13, month, null)),
            seen);
        Assert.Equal(new FetchProgress(false, 13, 13, null, null), rig.Coordinator.Progress);
    }

    [Fact]
    public async Task The_progress_is_there_as_soon_as_the_start_returns_and_names_the_month_being_fetched()
    {
        var rig = await Rig.ReadyAsync();
        var entered = NewGate();
        var release = NewGate();
        rig.Reports.OnRefresh = async (_, _) =>
        {
            entered.TrySetResult();
            await release.Task;
        };

        Assert.Equal(FetchStartResult.Started, rig.Coordinator.TryStart("Test User"));
        Assert.True(rig.Coordinator.Progress!.Running);

        await entered.Task.WaitAsync(Bounded);
        Assert.Equal(new FetchProgress(true, 0, 13, "2026-07", null), rig.Coordinator.Progress);

        release.SetResult();
        await rig.FinishedAsync();
        Assert.False(rig.Coordinator.Progress!.Running);
    }

    [Fact]
    public async Task Months_that_do_not_need_a_fetch_are_left_alone_and_are_not_counted()
    {
        var rig = await Rig.ReadyAsync();
        rig.Reports.Archive("2026-06", "2026-02");

        await rig.RunAsync();

        var expected = ThirteenMonths.Where(m => m is not ("2026-06" or "2026-02")).ToList();
        Assert.Equal(expected, rig.Reports.Refreshed.Select(p => p.ToString()));
        Assert.Equal(new FetchProgress(false, 11, 11, null, null), rig.Coordinator.Progress);
    }

    [Fact]
    public async Task With_nothing_missing_the_run_is_over_at_once_and_records_nothing()
    {
        var rig = await Rig.ReadyAsync();
        rig.Reports.Archive(ThirteenMonths);

        Assert.Equal(FetchStartResult.Started, await rig.RunAsync());

        Assert.Empty(rig.Reports.Refreshed);   // the fourteenth month back (2025-06) is missing too, and is not wanted
        Assert.Equal(new FetchProgress(false, 0, 0, null, null), rig.Coordinator.Progress);
        Assert.False(rig.Store.Documents.ContainsKey(DocumentKeys.FetchState));
        Assert.True(await rig.LockIsFreeAsync());
    }

    [Fact]
    public async Task The_wanted_months_are_worked_out_once_per_run_so_a_clock_that_moves_on_changes_nothing()
    {
        var rig = await Rig.ReadyAsync();
        rig.Reports.OnRefresh = (_, _) =>
        {
            rig.Reports.ExpectedLatestPeriod = Period.Parse("2026-08");
            rig.Time.Advance(TimeSpan.FromDays(30));
            return Task.CompletedTask;
        };

        await rig.RunAsync();

        Assert.Equal(ThirteenMonths, rig.Reports.Refreshed.Select(p => p.ToString()));
    }

    // The run lasts up to half an hour and outlives the request (or job wake-up) that started it. Started inside the caller's
    // execution context it would inherit that context's ambient state (Umbraco's scope, the request's logging scope) and nest its own
    // scopes under a parent that is long disposed.
    private static readonly AsyncLocal<string?> Ambient = new();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_background_run_does_not_inherit_the_ambient_context_of_the_caller(bool asynchronously)
    {
        var rig = await Rig.ReadyAsync();
        var seen = new List<string?>();
        rig.Reports.OnRefresh = (_, _) =>
        {
            seen.Add(Ambient.Value);
            return Task.CompletedTask;
        };
        Ambient.Value = "the request that started the run";
        try
        {
            var result = asynchronously ? await rig.Coordinator.TryStartAsync("Test User") : rig.Coordinator.TryStart("Test User");
            Assert.Equal(FetchStartResult.Started, result);
            await rig.FinishedAsync();

            Assert.Equal("the request that started the run", Ambient.Value);   // the caller keeps its own context
        }
        finally
        {
            Ambient.Value = null;
        }

        Assert.Equal(13, seen.Count);
        Assert.All(seen, value => Assert.Null(value));
    }

    [Fact]
    public async Task A_run_can_be_started_again_once_it_has_ended()
    {
        var rig = await Rig.ReadyAsync();

        Assert.Equal(FetchStartResult.Started, await rig.RunAsync());
        Assert.Equal(FetchStartResult.Started, await rig.RunAsync());

        // The second run found everything archived by the first.
        Assert.Equal(13, rig.Reports.Refreshed.Count);
        Assert.Equal(new FetchProgress(false, 0, 0, null, null), rig.Coordinator.Progress);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Not set up
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Nothing_starts_when_the_package_is_not_set_up()
    {
        var rig = Rig.NotConfigured();

        Assert.Equal(FetchStartResult.NotConfigured, rig.Coordinator.TryStart("Test User"));

        Assert.Null(rig.Coordinator.Progress);
        Assert.Null(rig.Coordinator.CurrentRun);
        Assert.Empty(rig.Reports.Refreshed);
        Assert.True(await rig.LockIsFreeAsync());
    }

    [Fact]
    public async Task A_key_that_cannot_be_used_counts_as_not_set_up()
    {
        var other = new SettingsHarness();   // another key ring: what a restored backup looks like
        await other.SaveKeyAsync(FakeServiceAccount.Json());
        var settings = await ReadySettingsHarness.ReadyAsync();
        await settings.Store.SaveDocumentAsync(DocumentKeys.ProtectedKey, other.Store.Documents[DocumentKeys.ProtectedKey]);
        settings.Time.Advance(TimeSpan.FromMinutes(2));   // the settings are cached for a minute
        var rig = Rig.Over(settings);

        Assert.Equal(SetupState.KeyUnreadable, await settings.Settings.GetSetupStateAsync());
        Assert.Equal(FetchStartResult.NotConfigured, rig.Coordinator.TryStart("Test User"));
        Assert.Null(rig.Coordinator.Progress);
    }

    [Fact]
    public async Task A_missing_property_counts_as_not_set_up()
    {
        var settings = await ReadySettingsHarness.ReadyAsync();
        await settings.Settings.SaveAsync(new SeoReportSettings { Ga4PropertyId = null, SearchConsoleSite = ReadySettingsHarness.Site }, null, false);
        var rig = Rig.Over(settings);

        Assert.Equal(FetchStartResult.NotConfigured, rig.Coordinator.TryStart("Test User"));
    }
}
