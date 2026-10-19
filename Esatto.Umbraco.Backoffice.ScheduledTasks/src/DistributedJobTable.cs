using System.Reflection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Infrastructure.Models;
using Umbraco.Cms.Infrastructure.Persistence.Repositories;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

internal enum DistributedTakeResult
{
    Taken,
    NotRegistered,
    AlreadyRunning,
}

internal enum DistributedResetResult
{
    Reset,

    /// <summary>Not flagged as running (or not registered).</summary>
    NotRunning,

    /// <summary>Flagged as running, but no longer the stuck run the caller saw: it was picked up again.</summary>
    NotStuck,
}

/// <summary>Umbraco's <c>umbracoDistributedJob</c> rows, as far as the dashboard needs them.</summary>
internal interface IDistributedJobTable
{
    IReadOnlyDictionary<string, DistributedBackgroundJobModel> GetAll();

    /// <param name="runningPeriod">When given, the row's period while it runs is at least this (see <see cref="RaiseRunningPeriod"/>).</param>
    DistributedTakeResult TryTake(string jobName, TimeSpan? runningPeriod = null);

    void SetPeriods(IReadOnlyDictionary<string, TimeSpan> periodsByJobName);

    /// <summary>
    /// Raises a running row's period to at least <paramref name="atLeast"/>, so Umbraco doesn't take
    /// the run for a stale one (<c>Period + MaximumExecutionTime</c> after it started) too soon.
    /// Rows not running, or already on a longer period, are left alone.
    /// </summary>
    void RaiseRunningPeriod(string jobName, TimeSpan atLeast);

    /// <summary>How often Umbraco's poller looks for a due job (<c>DistributedJobSettings.Period</c>).</summary>
    TimeSpan PollPeriod { get; }

    /// <summary>
    /// Clears the running flag of a job whose run was interrupted, but only while it still looks
    /// stuck and, when <paramref name="expectedRunningSinceUtc"/> is given, is still the run that
    /// started then (not one picked up since).
    /// </summary>
    DistributedResetResult ResetRunning(string jobName, DateTime? expectedRunningSinceUtc = null);

    bool IsRunning(DistributedBackgroundJobModel job, DateTime utcNow);

    /// <summary>Flagged as running for longer than Umbraco's <c>MaximumExecutionTime</c>.</summary>
    bool IsPossiblyStuck(DistributedBackgroundJobModel job, DateTime utcNow);
}

/// <summary>
/// Reads and nudges Umbraco's <c>umbracoDistributedJob</c> rows: the shared schedule every server
/// in a load-balanced setup polls. Writes take the same <c>DistributedJobs</c> lock Umbraco's
/// <c>DistributedJobService</c> uses.
/// </summary>
internal sealed class DistributedJobTable : IDistributedJobTable
{
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly IDistributedJobRepository _repository;
    private readonly IOptionsMonitor<DistributedJobSettings> _distributedJobSettings;

    public DistributedJobTable(
        ICoreScopeProvider scopeProvider,
        IDistributedJobRepository repository,
        IOptionsMonitor<DistributedJobSettings> distributedJobSettings)
    {
        _scopeProvider = scopeProvider;
        _repository = repository;
        _distributedJobSettings = distributedJobSettings;
    }

    public IReadOnlyDictionary<string, DistributedBackgroundJobModel> GetAll()
    {
        using var scope = _scopeProvider.CreateCoreScope(autoComplete: true);
        return _repository.GetAll().ToDictionary(x => x.Name, StringComparer.Ordinal);
    }

    /// <summary>
    /// Marks one job as running so no server's poller picks it up, exactly as Umbraco does when it
    /// takes a due job. The caller runs it and then calls <c>IDistributedJobService.FinishAsync</c>.
    /// </summary>
    public DistributedTakeResult TryTake(string jobName, TimeSpan? runningPeriod = null)
    {
        using var scope = _scopeProvider.CreateCoreScope();
        scope.EagerWriteLock(Constants.Locks.DistributedJobs);

        var job = _repository.GetByName(jobName);
        if (job is null)
        {
            scope.Complete();
            return DistributedTakeResult.NotRegistered;
        }

        var utcNow = DateTime.UtcNow;
        if (IsRunning(job, utcNow))
        {
            scope.Complete();
            return DistributedTakeResult.AlreadyRunning;
        }

        job.LastAttemptedRun = utcNow;
        job.IsRunning = true;
        if (runningPeriod is { } atLeast && job.Period < atLeast) job.Period = atLeast;
        _repository.Update(job);
        scope.Complete();
        return DistributedTakeResult.Taken;
    }

    /// <summary>Sets the scheduling period on each named row that differs. Unknown names are ignored.</summary>
    public void SetPeriods(IReadOnlyDictionary<string, TimeSpan> periodsByJobName)
    {
        if (periodsByJobName.Count == 0) return;

        using var scope = _scopeProvider.CreateCoreScope();
        scope.EagerWriteLock(Constants.Locks.DistributedJobs);

        foreach (var job in _repository.GetAll())
        {
            if (!periodsByJobName.TryGetValue(job.Name, out var period) || job.Period == period) continue;
            job.Period = period;
            _repository.Update(job);
        }

        scope.Complete();
    }

    public void RaiseRunningPeriod(string jobName, TimeSpan atLeast)
    {
        using var scope = _scopeProvider.CreateCoreScope();
        scope.EagerWriteLock(Constants.Locks.DistributedJobs);

        if (_repository.GetByName(jobName) is { IsRunning: true } job && job.Period < atLeast)
        {
            job.Period = atLeast;
            _repository.Update(job);
        }

        scope.Complete();
    }

    public TimeSpan PollPeriod => _distributedJobSettings.CurrentValue.Period;

    public DistributedResetResult ResetRunning(string jobName, DateTime? expectedRunningSinceUtc = null)
    {
        using var scope = _scopeProvider.CreateCoreScope();
        scope.EagerWriteLock(Constants.Locks.DistributedJobs);

        var job = _repository.GetByName(jobName);
        if (job is not { IsRunning: true })
        {
            scope.Complete();
            return DistributedResetResult.NotRunning;
        }

        // Checked again under the lock: since the dashboard looked, Umbraco may have reclaimed the
        // stale row and started a fresh run, which must not be cleared.
        if (!IsPossiblyStuck(job, DateTime.UtcNow) || !IsSameRun(job, expectedRunningSinceUtc))
        {
            scope.Complete();
            return DistributedResetResult.NotStuck;
        }

        // LastRun stays as it was, so an overdue job is picked up by the next poll.
        job.IsRunning = false;
        _repository.Update(job);
        scope.Complete();
        return DistributedResetResult.Reset;
    }

    // The dashboard's time went through JSON (and maybe a browser Date), so allow for rounding.
    private static bool IsSameRun(DistributedBackgroundJobModel job, DateTime? expectedRunningSinceUtc)
        => expectedRunningSinceUtc is not { } expected
           || (job.LastAttemptedRun - AsUtc(expected)).Duration() < TimeSpan.FromSeconds(1);

    private static DateTime AsUtc(DateTime value)
        => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    /// <summary>
    /// Running per Umbraco's rules: flagged as running and not yet stale
    /// (stale = no finish within <c>Period + MaximumExecutionTime</c>).
    /// </summary>
    public bool IsRunning(DistributedBackgroundJobModel job, DateTime utcNow)
        => job.IsRunning
           && job.LastAttemptedRun >= utcNow - job.Period - _distributedJobSettings.CurrentValue.MaximumExecutionTime;

    public bool IsPossiblyStuck(DistributedBackgroundJobModel job, DateTime utcNow)
        => job.IsRunning && job.LastAttemptedRun < utcNow - _distributedJobSettings.CurrentValue.MaximumExecutionTime;
}

/// <summary>
/// Umbraco 18.1+ lets a distributed job align its runs to the clock (<c>AlignToClock</c>): it is then
/// due at the first multiple of its period (counted from 0001-01-01 UTC) after its last run, not at
/// <c>LastRun + Period</c>. The package compiles against 18.0, which lacks the member, so it's read
/// by reflection: from the interface on 18.1+, else from a public property the job itself declares
/// (its author asked for alignment, which Umbraco honours once the site is upgraded).
/// </summary>
internal static class DistributedJobClock
{
    private const string AlignToClockName = "AlignToClock";

    private static readonly PropertyInfo? InterfaceProperty =
        typeof(IDistributedBackgroundJob).GetProperty(AlignToClockName, typeof(bool));

    public static bool IsAlignedToClock(IDistributedBackgroundJob job)
    {
        var property = InterfaceProperty
            ?? job.GetType().GetProperty(AlignToClockName, BindingFlags.Public | BindingFlags.Instance, null, typeof(bool), Type.EmptyTypes, null);
        if (property is null) return false;

        try
        {
            return property.GetValue(job) is true;
        }
        catch (TargetInvocationException)
        {
            return false;
        }
    }

    /// <summary>When Umbraco's poller first considers the row due (UTC), per its own rule.</summary>
    public static DateTime NextRunUtc(DistributedBackgroundJobModel row, bool alignedToClock)
    {
        var lastRun = DateTime.SpecifyKind(row.LastRun, DateTimeKind.Utc);
        if (!alignedToClock || row.Period <= TimeSpan.Zero) return lastRun + row.Period;

        // Due once a boundary (a multiple of the period) falls strictly after the last run.
        var periodTicks = row.Period.Ticks;
        var nextBoundaryTicks = lastRun.Ticks - lastRun.Ticks % periodTicks + periodTicks;
        return nextBoundaryTicks > DateTime.MaxValue.Ticks ? DateTime.MaxValue : new DateTime(nextBoundaryTicks, DateTimeKind.Utc);
    }
}
