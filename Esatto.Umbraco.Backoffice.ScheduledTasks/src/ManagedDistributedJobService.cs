using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Infrastructure.Models;
using Umbraco.Cms.Infrastructure.Services;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// Decorates Umbraco's <see cref="IDistributedJobService"/>: every job it hands out is wrapped in a
/// <see cref="TrackedDistributedJob"/> (pause + run log); editor interval overrides are written back
/// after Umbraco re-syncs the job table at startup; and after every run the row gets the period the
/// stored schedule calls for (for a calendar, the time until its next occurrence).
/// </summary>
/// <remarks>
/// <para>
/// No job is handed out until the settings have been read from the database, so a paused job
/// doesn't run in the window between Umbraco reaching runtime level Run and this package's
/// migration (the job just stays due for the next poll).
/// </para>
/// <para>
/// On a calendar the occurrence a scheduled run serves is recorded when the job is taken
/// (<see cref="ScheduledTaskSetting.LastScheduledUtc"/>), and the next one is counted from there,
/// not from when a run finished: a Run now that is still running at an occurrence leaves that
/// occurrence due, and a run that ends inside a repeated autumn hour doesn't run again at its
/// second instance. A job Umbraco hands out before its pending occurrence (e.g. while a restarting
/// server has just reset the row to the job's own period) is skipped. While a calendar job runs its
/// row's period is at least the job's own, or the time to the following occurrence, so Umbraco
/// doesn't take the run for a stale one (and start it again) sooner than it would on an interval.
/// </para>
/// </remarks>
internal sealed class ManagedDistributedJobService : IDistributedJobService
{
    // The period of a calendar row that never runs again.
    private static readonly TimeSpan Never = TimeSpan.FromDays(3650);

    private readonly IDistributedJobService _inner;
    private readonly IDistributedJobTable _table;
    private readonly IScheduledTaskSettingsStore _settings;
    private readonly IScheduledTaskRunLog _runLog;
    private readonly IEnumerable<IDistributedBackgroundJob> _jobs;
    private readonly ScheduledTaskCalendarEvaluator _calendarEvaluator;
    private readonly ILogger<ManagedDistributedJobService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly ScheduledTaskRunControl _runControl;
    private readonly IOptionsMonitor<ScheduledTasksOptions>? _options;

    // Occurrences handled on this server, by job name: still known if recording one in the database failed.
    private readonly ConcurrentDictionary<string, DateTime> _handledOccurrences = new(StringComparer.Ordinal);

    // Aligned jobs whose stored calendar has been reported as ignored.
    private readonly ConcurrentDictionary<string, byte> _alignedCalendarWarned = new(StringComparer.Ordinal);

    // Set when Umbraco reset the rows' periods before the settings could be read.
    private volatile bool _schedulesPending;
    private int _heldBackLogged;

    public ManagedDistributedJobService(
        IDistributedJobService inner,
        IDistributedJobTable table,
        IScheduledTaskSettingsStore settings,
        IScheduledTaskRunLog runLog,
        IEnumerable<IDistributedBackgroundJob> jobs,
        ScheduledTaskCalendarEvaluator calendarEvaluator,
        ILogger<ManagedDistributedJobService> logger,
        TimeProvider? timeProvider = null,
        ScheduledTaskRunControl? runControl = null,
        IOptionsMonitor<ScheduledTasksOptions>? options = null)
    {
        _inner = inner;
        _table = table;
        _settings = settings;
        _runLog = runLog;
        _jobs = jobs;
        _calendarEvaluator = calendarEvaluator;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _runControl = runControl ?? new ScheduledTaskRunControl(runLog, _timeProvider);
        _options = options;
    }

    public async Task<IDistributedBackgroundJob?> TryTakeRunnableAsync()
    {
        _settings.GetAll(); // loads them when due
        if (_settings.IsWaitingForDatabase)
        {
            // Every distributed job (Umbraco's own too) waits, so say so once rather than stay silent.
            if (Interlocked.Exchange(ref _heldBackLogged, 1) == 0)
                _logger.LogWarning("Distributed background jobs are held back until the Scheduled Tasks settings can be read (its migration has run and the database is reachable)");
            return null;
        }

        if (_schedulesPending)
        {
            try
            {
                ApplyAllSchedules();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not apply Scheduled Tasks schedules to the distributed background jobs");
            }
        }

        var job = await _inner.TryTakeRunnableAsync();
        if (job is null) return null;

        // A failed run's retry that has come due: run it as that retry (it doesn't serve a calendar occurrence).
        if (DueRetry(job) is { } retryOf)
        {
            // The row was taken with the short retry-delay period. Running longer than that plus
            // MaximumExecutionTime would look stale, and another server would start the job on top
            // of the retry; give the running row the task's real period, as a normal run has.
            try
            {
                var setting = _settings.Get(ScheduledTaskIdentity.ForDistributed(job.Name));
                var period = setting?.PeriodOverride ?? job.Period;
                if (RunningPeriod(job, setting, UtcNow()) is { } calendarPeriod && calendarPeriod > period) period = calendarPeriod;
                if (period > TimeSpan.Zero) _table.RaiseRunningPeriod(job.Name, period);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not keep the retry of distributed job {JobName} from looking stale early", job.Name);
            }

            return Track(job, ScheduledTaskTrigger.Retry, triggeredBy: null, skipReason: null, retryOf);
        }

        string? skipReason = null;
        try
        {
            skipReason = TakeScheduledOccurrence(job);
        }
        catch (Exception ex)
        {
            // The job was taken; let it run as Umbraco scheduled it.
            _logger.LogError(ex, "Could not check the calendar schedule of distributed job {JobName}", job.Name);
        }

        return Track(job, ScheduledTaskTrigger.Schedule, triggeredBy: null, skipReason);
    }

    public async Task FinishAsync(string jobName)
    {
        await _inner.FinishAsync(jobName);

        // Always written from the stored setting (not this server's cache), so a period written
        // from stale settings, here or on another server, is corrected after the next run. On a
        // calendar, the next run is the next occurrence after the one last served.
        try
        {
            ApplySchedules([jobName]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not schedule the next run of distributed job {JobName}", jobName);
        }
    }

    public async Task EnsureJobsAsync()
    {
        await _inner.EnsureJobsAsync();

        // Umbraco has just reset every row's period to the job's own; put editor schedules back.
        // Until then a calendar job another server takes is skipped (see TakeScheduledOccurrence).
        ApplyAllSchedules();
    }

    public TrackedDistributedJob Track(IDistributedBackgroundJob job, ScheduledTaskTrigger trigger, string? triggeredBy)
        => Track(job, trigger, triggeredBy, skipReason: null);

    private TrackedDistributedJob Track(
        IDistributedBackgroundJob job,
        ScheduledTaskTrigger trigger,
        string? triggeredBy,
        string? skipReason,
        ScheduledTaskRun? retryOf = null)
        => new(job, trigger, triggeredBy, _settings, _runLog, _logger, skipReason,
            _runControl, _options?.CurrentValue, _timeProvider, retryOf);

    // A retry is taken up to one poll early: the poller only looks every PollPeriod.
    private ScheduledTaskRun? DueRetry(IDistributedBackgroundJob job)
    {
        try
        {
            var pending = _runLog.GetPendingRetry(ScheduledTaskIdentity.ForDistributed(job.Name), fresh: true);
            return pending?.RetryAtUtc is { } retryAt && retryAt <= UtcNow() + _table.PollPeriod ? pending : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check for a pending retry of distributed job {JobName}", job.Name);
            return null;
        }
    }

    /// <summary>
    /// The period a calendar job's row should have while it runs (taken now): at least the job's
    /// own period, or the time until the calendar's following occurrence, whichever is longer.
    /// Null when the job isn't on a calendar. Umbraco reclaims a running row as stale once
    /// <c>Period + MaximumExecutionTime</c> has passed since it was taken, so a period computed to
    /// make the job due soon would otherwise let a second server start it while it still runs.
    /// </summary>
    public TimeSpan? RunningPeriod(string jobName)
    {
        if (_jobs.FirstOrDefault(j => j.Name == jobName) is not { } job) return null;
        return RunningPeriod(job, _settings.Get(ScheduledTaskIdentity.ForDistributed(jobName)), UtcNow());
    }

    private TimeSpan? RunningPeriod(IDistributedBackgroundJob job, ScheduledTaskSetting? setting, DateTime utcNow)
    {
        if (setting?.Calendar is not { } calendar || DistributedJobClock.IsAlignedToClock(job)) return null;

        var own = job.Period > TimeSpan.Zero ? job.Period : TimeSpan.Zero;
        return _calendarEvaluator.NextOccurrenceUtc(calendar, utcNow) is { } following && following - utcNow > own
            ? following - utcNow
            : own;
    }

    /// <summary>
    /// For a job Umbraco's poller just took: on a calendar, records the occurrence this run serves
    /// and keeps the running row from looking stale. Returns why the run is skipped instead, when
    /// the calendar's pending occurrence hasn't come yet.
    /// </summary>
    private string? TakeScheduledOccurrence(IDistributedBackgroundJob job)
    {
        // What this server caches may be up to a refresh interval old (another server may have just
        // saved a calendar, or dropped one); the decision must not be. One read per run, as FinishAsync.
        TryRefreshSettings();

        var taskId = ScheduledTaskIdentity.ForDistributed(job.Name);
        var setting = _settings.Get(taskId);
        if (setting?.Calendar is not { } calendar || DistributedJobClock.IsAlignedToClock(job)) return null;

        // The row wasn't on the calendar's period: e.g. a restarting server had just reset it to the
        // job's own period, or a stale server wrote one. The skipped run's finish puts the period right.
        var utcNow = UtcNow();
        var pending = PendingOccurrence(job.Name, calendar, setting);
        if (pending is null) return "its calendar schedule doesn't run again";
        if (pending > utcNow) return $"its calendar schedule's next occurrence is {pending.Value:u}";

        // Every occurrence up to now is served by this run (or skipped by it while paused).
        _handledOccurrences.AddOrUpdate(job.Name, utcNow, (_, previous) => previous > utcNow ? previous : utcNow);
        try
        {
            _settings.RecordScheduledOccurrence(taskId, utcNow);
        }
        catch (Exception ex)
        {
            // Not fatal: this server still knows; another server might run the occurrence once more.
            _logger.LogWarning(ex, "Could not record the handled calendar occurrence of distributed job {JobName}", job.Name);
        }

        if (RunningPeriod(job, setting, utcNow) is { } running)
            _table.RaiseRunningPeriod(job.Name, running);

        return null;
    }

    private void TryRefreshSettings()
    {
        try
        {
            _settings.Refresh();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not re-read the Scheduled Tasks settings; using the cached ones");
        }
    }

    /// <summary>The calendar's first occurrence after the last one served (or after it was saved).</summary>
    private DateTime? PendingOccurrence(string jobName, ScheduledTaskCalendar calendar, ScheduledTaskSetting setting)
    {
        var baseline = DateTime.SpecifyKind(calendar.SavedUtc, DateTimeKind.Utc);
        if (setting.LastScheduledUtc is { } stored && stored > baseline) baseline = stored;
        if (_handledOccurrences.TryGetValue(jobName, out var handled) && handled > baseline) baseline = handled;
        return _calendarEvaluator.NextOccurrenceUtc(calendar, baseline);
    }

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private void ApplyAllSchedules()
    {
        // Stays pending until every job's schedule has been written: a deferred or failed attempt
        // (e.g. the database can't be read at startup) is retried on the next poll, and applying a
        // single job (after its run) doesn't count.
        _schedulesPending = true;
        var count = ApplySchedules(_jobs.Select(j => j.Name));
        if (_settings.IsWaitingForDatabase) return;
        _schedulesPending = false;

        if (count > 0)
            _logger.LogInformation("Applied Scheduled Tasks schedules to {Count} distributed background job(s)", count);
    }

    /// <summary>
    /// Writes to each named job's row the period its stored setting calls for, read fresh from the
    /// database: the time until its pending calendar occurrence, its interval override, or else the
    /// job's own period. Rows already on that period aren't touched. Before the settings can be
    /// read nothing is written; they're applied once they can.
    /// </summary>
    /// <returns>How many rows were given an editor's schedule.</returns>
    public int ApplySchedules(IEnumerable<string> jobNames)
    {
        _settings.Refresh();
        if (_settings.IsWaitingForDatabase)
        {
            // Writing now would replace editor schedules with the jobs' own periods.
            _schedulesPending = true;
            return 0;
        }

        var settings = _settings.GetAll();
        var rows = _table.GetAll();
        var pollPeriod = _table.PollPeriod;
        var utcNow = UtcNow();
        var periods = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);
        var scheduled = 0;

        foreach (var name in jobNames.Distinct(StringComparer.Ordinal))
        {
            if (_jobs.FirstOrDefault(j => j.Name == name) is not { } job) continue;
            settings.TryGetValue(ScheduledTaskIdentity.ForDistributed(name), out var setting);

            if (setting?.Calendar is { } calendar && DistributedJobClock.IsAlignedToClock(job))
            {
                // Umbraco reads an aligned job's period as clock boundaries, not as a delay. Said once:
                // this runs after every run (e.g. each minute for scheduled publishing).
                if (_alignedCalendarWarned.TryAdd(name, 0))
                    _logger.LogWarning(
                        "Distributed job {JobName} aligns its runs to the clock, so its calendar schedule is ignored; it runs every {Period}",
                        name, job.Period);
                periods[name] = job.Period;
            }
            else if (setting?.IsPaused != true
                     && _runLog.GetPendingRetry(ScheduledTaskIdentity.ForDistributed(name))?.RetryAtUtc is { } retryAt
                     && rows.TryGetValue(name, out var failedRow))
            {
                // A failed run is tried again: due at the retry time, whatever the schedule says.
                var lastRun = DateTime.SpecifyKind(failedRow.LastRun, DateTimeKind.Utc);
                periods[name] = retryAt > lastRun ? retryAt - lastRun : TimeSpan.Zero;
                scheduled++;
            }
            else if (setting?.Calendar is { } onCalendar)
            {
                if (!rows.TryGetValue(name, out var row)) continue;
                periods[name] = CalendarPeriod(job, onCalendar, setting, row, utcNow);
                scheduled++;
            }
            else if (setting?.PeriodOverride is { } period)
            {
                // Saved before shorter intervals were refused: one row due on every poll starves the rest.
                if (period < pollPeriod)
                {
                    _logger.LogWarning(
                        "Distributed job {JobName} has an interval of {Period}, shorter than Umbraco's poll period {PollPeriod}; using {PollPeriod}",
                        name, period, pollPeriod, pollPeriod);
                    period = pollPeriod;
                }

                periods[name] = period;
                scheduled++;
            }
            else
            {
                periods[name] = job.Period;
            }
        }

        _table.SetPeriods(periods);
        return scheduled;
    }

    /// <summary>
    /// The period that makes Umbraco's poller (due once <c>LastRun + Period</c> has passed) take the
    /// job at its pending occurrence: zero when that has already passed (e.g. during a Run now), so
    /// it runs on the next poll. While the row is running it keeps at least its running period.
    /// </summary>
    private TimeSpan CalendarPeriod(
        IDistributedBackgroundJob job,
        ScheduledTaskCalendar calendar,
        ScheduledTaskSetting setting,
        DistributedBackgroundJobModel row,
        DateTime utcNow)
    {
        var lastRun = DateTime.SpecifyKind(row.LastRun, DateTimeKind.Utc);
        var period = PendingOccurrence(job.Name, calendar, setting) is { } pending
            ? pending > lastRun ? pending - lastRun : TimeSpan.Zero
            : Never;

        // E.g. a schedule saved while the job runs: don't let the running row look stale early.
        if (_table.IsRunning(row, utcNow) && RunningPeriod(job, setting, utcNow) is { } running && running > period)
            period = running;

        return period;
    }
}
