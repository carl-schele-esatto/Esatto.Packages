using Esatto.Umbraco.Backoffice.SeoReport.Jobs;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>One run at a time: in this process, and across instances through the database lock <c>fetch</c> (30 minutes, renewed for every month).</summary>
public partial class FetchCoordinatorTests
{
    // ---------------------------------------------------------------------------------------------------------------
    // Simultaneous starts
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Simultaneous_starts_give_exactly_one_Started_and_the_others_AlreadyRunning()
    {
        var rig = await Rig.ReadyAsync();
        var release = NewGate();
        rig.Reports.OnRefresh = (_, _) => release.Task;   // the run stays busy for as long as the test likes
        using var together = new Barrier(4);

        var starts = Enumerable.Range(0, 4).Select(_ => Task.Factory.StartNew(
            () =>
            {
                together.SignalAndWait(Bounded);
                return rig.Coordinator.TryStart("Test User");
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default)).ToList();
        var results = await Task.WhenAll(starts).WaitAsync(Bounded);

        Assert.Equal(1, results.Count(r => r == FetchStartResult.Started));
        Assert.Equal(3, results.Count(r => r == FetchStartResult.AlreadyRunning));

        release.SetResult();
        await rig.FinishedAsync();
        Assert.Equal(ThirteenMonths, rig.Reports.Refreshed.Select(p => p.ToString()));   // one run: every month once
    }

    [Fact]
    public async Task Simultaneous_asynchronous_starts_give_exactly_one_Started_too()
    {
        var rig = await Rig.ReadyAsync();
        var release = NewGate();
        rig.Reports.OnRefresh = (_, _) => release.Task;

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => rig.Coordinator.TryStartAsync("Test User"))).WaitAsync(Bounded);

        Assert.Equal(1, results.Count(r => r == FetchStartResult.Started));
        Assert.Equal(3, results.Count(r => r == FetchStartResult.AlreadyRunning));

        release.SetResult();
        await rig.FinishedAsync();
    }

    [Fact]
    public async Task A_second_start_while_a_run_is_going_is_AlreadyRunning_and_changes_nothing()
    {
        var rig = await Rig.ReadyAsync();
        var entered = NewGate();
        var release = NewGate();
        rig.Reports.OnRefresh = async (_, _) =>
        {
            entered.TrySetResult();
            await release.Task;
        };
        rig.Coordinator.TryStart("Test User");
        await entered.Task.WaitAsync(Bounded);
        var before = rig.Coordinator.Progress;

        Assert.Equal(FetchStartResult.AlreadyRunning, rig.Coordinator.TryStart("Another User"));

        Assert.Equal(before, rig.Coordinator.Progress);
        release.SetResult();
        await rig.FinishedAsync();
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Other instances
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_lock_held_by_another_instance_means_AlreadyRunning_and_is_left_alone()
    {
        var rig = await Rig.ReadyAsync();
        Assert.True(await rig.Store.TryAcquireLockAsync("fetch", "another-instance", TimeSpan.FromMinutes(30)));

        Assert.Equal(FetchStartResult.AlreadyRunning, rig.Coordinator.TryStart("Test User"));

        Assert.Empty(rig.Reports.Refreshed);
        Assert.Null(rig.Coordinator.Progress);
        Assert.False(await rig.LockIsFreeAsync());   // still theirs
        Assert.True(await rig.Store.TryAcquireLockAsync("fetch", "another-instance", TimeSpan.FromMinutes(30)));

        // The refusal left no claim behind in this process: once they let go, a start works.
        await rig.Store.ReleaseLockAsync("fetch", "another-instance");
        Assert.Equal(FetchStartResult.Started, await rig.RunAsync());
    }

    [Fact]
    public async Task A_lock_that_has_expired_is_taken_over()
    {
        var rig = await Rig.ReadyAsync();
        await rig.Store.TryAcquireLockAsync("fetch", "crashed-instance", TimeSpan.FromMinutes(30));
        rig.Time.Advance(TimeSpan.FromMinutes(30));   // an expiry of exactly now counts as expired

        Assert.Equal(FetchStartResult.Started, await rig.RunAsync());
        Assert.Equal(13, rig.Reports.Refreshed.Count);
    }

    [Fact]
    public async Task Two_instances_over_the_same_database_never_run_together()
    {
        var first = await Rig.ReadyAsync();
        var second = new FetchCoordinator(new FakeReportService(), first.Settings.Settings, first.Store, first.Time, new CapturingLogger<FetchCoordinator>());
        var release = NewGate();
        first.Reports.OnRefresh = (_, _) => release.Task;

        Assert.Equal(FetchStartResult.Started, first.Coordinator.TryStart("Test User"));
        Assert.Equal(FetchStartResult.AlreadyRunning, second.TryStart("Test User"));
        Assert.Null(second.Progress);

        release.SetResult();
        await first.FinishedAsync();
        Assert.Equal(FetchStartResult.Started, second.TryStart("Test User"));
        await second.CurrentRun!.WaitAsync(Bounded);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The lock's life
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_lock_is_held_for_the_whole_run_and_free_again_when_it_ends()
    {
        var rig = await Rig.ReadyAsync();
        var heldDuring = new List<bool>();
        rig.Reports.OnRefresh = async (_, _) => heldDuring.Add(!await rig.LockIsFreeAsync());

        await rig.RunAsync();

        Assert.Equal(Enumerable.Repeat(true, 13), heldDuring);
        Assert.True(await rig.LockIsFreeAsync());
    }

    [Fact]
    public async Task The_lock_is_renewed_before_every_month_so_a_long_backfill_keeps_it()
    {
        var rig = await Rig.ReadyAsync();
        var heldDuring = new List<bool>();
        rig.Reports.OnRefresh = async (_, _) =>
        {
            // Each month takes 29 minutes. Held for 30 and never renewed, the lock would be gone by the second month.
            rig.Time.Advance(TimeSpan.FromMinutes(29));
            heldDuring.Add(!await rig.LockIsFreeAsync());
        };

        await rig.RunAsync();

        Assert.Equal(Enumerable.Repeat(true, 13), heldDuring);
        Assert.Equal(13, rig.Reports.Refreshed.Count);
    }

    [Fact]
    public async Task A_run_that_lost_its_lock_stops_with_unknown_and_never_releases_the_new_owners_lock()
    {
        var rig = await Rig.ReadyAsync();
        var tookOver = false;
        rig.Reports.OnRefresh = async (_, _) =>
        {
            rig.Time.Advance(TimeSpan.FromMinutes(31));   // longer than the lock lives: it lapses, and somebody else takes it
            tookOver = await rig.Store.TryAcquireLockAsync("fetch", "another-instance", TimeSpan.FromMinutes(30));
        };

        await rig.RunAsync();

        Assert.True(tookOver);
        Assert.Equal(["2026-07"], rig.Reports.Refreshed.Select(p => p.ToString()));
        Assert.Equal(new FetchProgress(false, 1, 13, null, "unknown"), rig.Coordinator.Progress);
        Assert.False(await rig.LockIsFreeAsync());
        Assert.True(await rig.Store.TryAcquireLockAsync("fetch", "another-instance", TimeSpan.FromMinutes(30)));   // still theirs
        Assert.Equal("unknown", (await rig.StateAsync()).LastErrorCode);
        Assert.Contains(rig.Log.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task A_store_that_fails_when_the_lock_is_asked_for_fails_the_start_and_leaves_no_claim_behind()
    {
        FlakyStore? flaky = null;
        var rig = await Rig.ReadyAsync(inner => flaky = new FlakyStore(inner));
        flaky!.AcquireLockFails = new InvalidOperationException("the database does not answer");

        Assert.Throws<InvalidOperationException>(() => rig.Coordinator.TryStart("Test User"));
        Assert.Null(rig.Coordinator.Progress);

        flaky.AcquireLockFails = null;
        Assert.Equal(FetchStartResult.Started, await rig.RunAsync());
    }
}
