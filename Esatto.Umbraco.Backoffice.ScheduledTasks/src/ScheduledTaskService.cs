using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Infrastructure.Models;
using Umbraco.Cms.Infrastructure.Services;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

public enum ScheduledTaskKind
{
    /// <summary>An <see cref="IRecurringBackgroundJob"/>: runs in-process on every server whose role matches.</summary>
    Recurring,

    /// <summary>An <see cref="IDistributedBackgroundJob"/>: runs on one server at a time, scheduled through the database.</summary>
    Distributed,
}

/// <summary>Everything the dashboard shows about one task.</summary>
public sealed record ScheduledTaskInfo(
    string Id,
    string Name,
    string TypeName,
    string? AssemblyName,
    ScheduledTaskKind Kind,
    bool IsUmbraco,
    TimeSpan Period,
    TimeSpan DefaultPeriod,
    TimeSpan? Delay,
    IReadOnlyList<string> ServerRoles,
    bool RunsOnThisServer,
    bool IsManaged,
    bool IsPaused,
    bool IsRunning,
    DateTime? RunningSinceUtc,
    bool IsPossiblyStuck,
    DateTime? NextRunUtc,
    DateTime? LastRunUtc,
    ScheduledTaskRunStats Stats,
    ScheduledTaskSetting? Setting,
    int PropertyCount)
{
    /// <summary>The calendar schedule the task runs on instead of an interval, if any.</summary>
    public ScheduledTaskCalendar? Calendar => Setting?.Calendar;

    /// <summary>Why the task can't use a calendar schedule (e.g. it aligns its runs to the clock); null when it can.</summary>
    public string? CalendarUnavailableReason { get; init; }

    /// <summary>The shortest interval an editor can set (the task's own period is always accepted).</summary>
    public TimeSpan? MinimumPeriod { get; init; }
}

public enum ScheduledTaskCommandStatus
{
    Ok,
    NotFound,
    Invalid,
    Conflict,
}

/// <param name="Errors">For <see cref="ScheduledTaskCommandStatus.Invalid"/> property values: the problems by property alias.</param>
public sealed record ScheduledTaskCommandResult(
    ScheduledTaskCommandStatus Status,
    string? Message = null,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static readonly ScheduledTaskCommandResult NotFound = new(ScheduledTaskCommandStatus.NotFound, "Scheduled task not found.");

    public static ScheduledTaskCommandResult Ok(string? message = null) => new(ScheduledTaskCommandStatus.Ok, message);

    public static ScheduledTaskCommandResult Invalid(string message, IReadOnlyDictionary<string, string[]>? errors = null)
        => new(ScheduledTaskCommandStatus.Invalid, message, errors);

    public static ScheduledTaskCommandResult Conflict(string message) => new(ScheduledTaskCommandStatus.Conflict, message);
}

public interface IScheduledTaskService
{
    /// <summary>This server's role (SchedulingPublisher, Subscriber, Single, …).</summary>
    ServerRole CurrentServerRole { get; }

    /// <summary>All registered tasks: custom ones first, then Umbraco's, each group by name.</summary>
    IReadOnlyList<ScheduledTaskInfo> GetAll();

    ScheduledTaskInfo? Get(string id);

    /// <summary>The task's runs on this server, newest first; null when the task doesn't exist.</summary>
    IReadOnlyList<ScheduledTaskRun>? GetRuns(string id);

    ScheduledTaskCommandResult RunNow(string id, string? triggeredBy);

    ScheduledTaskCommandResult SetPaused(string id, bool isPaused, string? updatedBy);

    /// <summary>
    /// Overrides the task's interval (and drops any calendar schedule); null (or the task's own
    /// period) restores the task's own schedule.
    /// </summary>
    ScheduledTaskCommandResult SetPeriod(string id, TimeSpan? period, string? updatedBy);

    /// <summary>
    /// Runs the task on a calendar schedule instead of an interval (dropping any interval
    /// override); null goes back to the interval.
    /// </summary>
    ScheduledTaskCommandResult SetSchedule(string id, ScheduledTaskCalendar? calendar, string? updatedBy);

    /// <summary>The schedule's next runs from now, or why it can't be used.</summary>
    (IReadOnlyList<DateTime> NextRunsUtc, string? Error) PreviewSchedule(ScheduledTaskCalendar calendar, int count);

    ScheduledTaskCommandResult ClearRuns(string id);

    /// <summary>
    /// Clears the "running" flag of a distributed task whose run was interrupted (e.g. the site
    /// stopped mid-run), so it runs again when due instead of waiting until Umbraco considers it stale.
    /// </summary>
    ScheduledTaskCommandResult ResetRun(string id);

    /// <summary>
    /// As <see cref="ResetRun(string)"/>, but only while the run that started at
    /// <paramref name="runningSinceUtc"/> (as the dashboard showed it) is still the one marked as
    /// running; a run picked up since isn't stuck and is left alone.
    /// </summary>
    ScheduledTaskCommandResult ResetRun(string id, DateTime? runningSinceUtc) => ResetRun(id);

    /// <summary>
    /// The task's properties form (see <see cref="IScheduledTaskWithProperties{TProperties}"/>);
    /// no definitions when it has no properties, null when the task doesn't exist.
    /// </summary>
    ScheduledTaskPropertiesForm? GetProperties(string id);

    /// <summary>Saves editor values for the task's properties; null restores all defaults.</summary>
    ScheduledTaskCommandResult SetProperties(string id, IReadOnlyDictionary<string, JsonNode?>? values, string? updatedBy);
}

internal sealed class ScheduledTaskService : IScheduledTaskService
{
    private readonly IEnumerable<IRecurringBackgroundJob> _recurringJobs;
    private readonly IEnumerable<IDistributedBackgroundJob> _distributedJobs;
    private readonly RecurringJobHostRegistry _registry;
    private readonly IDistributedJobService _distributedJobService;
    private readonly IDistributedJobTable _distributedJobTable;
    private readonly IScheduledTaskSettingsStore _settings;
    private readonly IScheduledTaskRunLog _runLog;
    private readonly IServerRoleAccessor _serverRoleAccessor;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ManualDistributedRunTracker _runTracker;
    private readonly ScheduledTaskPropertyValues _propertyValues;
    private readonly ScheduledTaskCalendarEvaluator _calendarEvaluator;
    private readonly IOptionsMonitor<ScheduledTasksOptions> _options;
    private readonly ILogger<ScheduledTaskService> _logger;

    public ScheduledTaskService(
        IEnumerable<IRecurringBackgroundJob> recurringJobs,
        IEnumerable<IDistributedBackgroundJob> distributedJobs,
        RecurringJobHostRegistry registry,
        IDistributedJobService distributedJobService,
        IDistributedJobTable distributedJobTable,
        IScheduledTaskSettingsStore settings,
        IScheduledTaskRunLog runLog,
        IServerRoleAccessor serverRoleAccessor,
        IHostApplicationLifetime lifetime,
        ManualDistributedRunTracker runTracker,
        ScheduledTaskPropertyValues propertyValues,
        ScheduledTaskCalendarEvaluator calendarEvaluator,
        IOptionsMonitor<ScheduledTasksOptions> options,
        ILogger<ScheduledTaskService> logger)
    {
        _propertyValues = propertyValues;
        _calendarEvaluator = calendarEvaluator;
        _recurringJobs = recurringJobs;
        _distributedJobs = distributedJobs;
        _registry = registry;
        _distributedJobService = distributedJobService;
        _distributedJobTable = distributedJobTable;
        _settings = settings;
        _runLog = runLog;
        _serverRoleAccessor = serverRoleAccessor;
        _lifetime = lifetime;
        _runTracker = runTracker;
        _options = options;
        _logger = logger;
    }

    public ServerRole CurrentServerRole => _serverRoleAccessor.CurrentServerRole;

    public IReadOnlyList<ScheduledTaskInfo> GetAll()
    {
        var settings = _settings.GetAll();
        var rows = ReadDistributedRows();
        var utcNow = DateTime.UtcNow;

        return DistinctRecurringJobs()
            .Select(job => DescribeRecurring(job, settings))
            .Concat(DistinctDistributedJobs().Select(job => DescribeDistributed(job, settings, rows, utcNow)))
            .OrderBy(t => t.IsUmbraco)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public ScheduledTaskInfo? Get(string id)
    {
        if (FindRecurring(id) is { } recurring) return DescribeRecurring(recurring, _settings.GetAll());
        if (FindDistributed(id) is { } distributed)
            return DescribeDistributed(distributed, _settings.GetAll(), ReadDistributedRows(), DateTime.UtcNow);
        return null;
    }

    public IReadOnlyList<ScheduledTaskRun>? GetRuns(string id)
        => Exists(id) ? _runLog.GetRuns(id) : null;

    public ScheduledTaskCommandResult RunNow(string id, string? triggeredBy)
    {
        if (FindRecurring(id) is { } recurring)
        {
            if (!_registry.TryGet(id, out var host))
                return ScheduledTaskCommandResult.Conflict("This task's host isn't managed by Scheduled Tasks, so it can't be started from here.");

            var role = CurrentServerRole;
            if (!recurring.ServerRoles.Contains(role))
                return ScheduledTaskCommandResult.Conflict(
                    $"This task doesn't run on this server's role ({role}). Run it from a server with role {string.Join(" or ", recurring.ServerRoles)}.");

            return host.RequestRun(triggeredBy)
                ? ScheduledTaskCommandResult.Ok("Run started.")
                : ScheduledTaskCommandResult.Conflict("The task is already running.");
        }

        if (FindDistributed(id) is { } distributed)
        {
            // A run started now would hold up shutdown, or be cut off with its row left marked as running.
            if (_lifetime.ApplicationStopping.IsCancellationRequested)
                return ScheduledTaskCommandResult.Conflict("The site is shutting down; run the task once it's back.");

            // On a calendar the row keeps a period that doesn't let the run look stale before an interval run would.
            var runningPeriod = (_distributedJobService as ManagedDistributedJobService)?.RunningPeriod(distributed.Name);
            switch (_distributedJobTable.TryTake(distributed.Name, runningPeriod))
            {
                case DistributedTakeResult.NotRegistered:
                    return ScheduledTaskCommandResult.Conflict(
                        "Umbraco hasn't registered this task in the database yet; it does so shortly after startup. Try again in a minute.");
                case DistributedTakeResult.AlreadyRunning:
                    return ScheduledTaskCommandResult.Conflict("The task is already running.");
            }

            StartDistributedRun(distributed, triggeredBy);
            return ScheduledTaskCommandResult.Ok("Run started.");
        }

        return ScheduledTaskCommandResult.NotFound;
    }

    public ScheduledTaskCommandResult SetPaused(string id, bool isPaused, string? updatedBy)
    {
        if (!Exists(id)) return ScheduledTaskCommandResult.NotFound;

        _settings.Update(id, state => state with { IsPaused = isPaused }, updatedBy);
        return ScheduledTaskCommandResult.Ok(isPaused ? "Task paused." : "Task resumed.");
    }

    public ScheduledTaskCommandResult SetPeriod(string id, TimeSpan? period, string? updatedBy)
    {
        TimeSpan defaultPeriod;
        if (FindRecurring(id) is { } recurring) defaultPeriod = recurring.Period;
        else if (FindDistributed(id) is { } distributed) defaultPeriod = distributed.Period;
        else return ScheduledTaskCommandResult.NotFound;

        var options = _options.CurrentValue;
        if (period is { } requested && (requested < options.MinimumPeriod || requested > options.MaximumPeriod))
            return ScheduledTaskCommandResult.Invalid(
                $"The interval must be between {DescribePeriod(options.MinimumPeriod)} and {DescribePeriod(options.MaximumPeriod)}.");

        // Umbraco's poller takes one due job per poll, so a row due on every poll starves the rest.
        var pollPeriod = _distributedJobTable.PollPeriod;
        if (period is { } shortest && ScheduledTaskIdentity.IsDistributed(id) && shortest != defaultPeriod && shortest < pollPeriod)
            return ScheduledTaskCommandResult.Invalid(
                $"Distributed tasks can't run more often than Umbraco checks for them (every {DescribePeriod(pollPeriod)}).");

        // Setting the task's own period is the same as resetting it. An interval replaces a calendar.
        var periodOverride = period == defaultPeriod ? null : period;
        _settings.Update(id, state => state with { PeriodOverride = periodOverride, Calendar = null }, updatedBy);
        ApplySchedule(id, periodOverride ?? defaultPeriod);

        return ScheduledTaskCommandResult.Ok(periodOverride is null ? "Back on the task's own schedule." : "Interval updated.");
    }

    public ScheduledTaskCommandResult SetSchedule(string id, ScheduledTaskCalendar? calendar, string? updatedBy)
    {
        TimeSpan defaultPeriod;
        if (FindRecurring(id) is { } recurring) defaultPeriod = recurring.Period;
        else if (FindDistributed(id) is { } distributed) defaultPeriod = distributed.Period;
        else return ScheduledTaskCommandResult.NotFound;

        if (calendar is null)
        {
            var current = _settings.Get(id);
            _settings.Update(id, state => state with { Calendar = null }, updatedBy);
            ApplySchedule(id, current?.PeriodOverride ?? defaultPeriod);
            return ScheduledTaskCommandResult.Ok("Back on an interval.");
        }

        // Umbraco reads an aligned job's period as clock boundaries, which a calendar's period isn't.
        if (FindDistributed(id) is { } aligned && DistributedJobClock.IsAlignedToClock(aligned))
            return ScheduledTaskCommandResult.Invalid($"{AlignedToClockReason} Use an interval instead.");

        var utcNow = DateTime.UtcNow;
        if (_calendarEvaluator.Validate(calendar, utcNow) is { } error) return ScheduledTaskCommandResult.Invalid(error);

        // Occurrences before now don't count: saving never triggers a run for a time that has passed.
        var saved = calendar with { SavedUtc = utcNow };
        _settings.Update(id, state => state with { Calendar = saved, PeriodOverride = null }, updatedBy);
        ApplySchedule(id, defaultPeriod);
        return ScheduledTaskCommandResult.Ok("Schedule saved.");
    }

    public (IReadOnlyList<DateTime> NextRunsUtc, string? Error) PreviewSchedule(ScheduledTaskCalendar calendar, int count)
    {
        var utcNow = DateTime.UtcNow;
        return _calendarEvaluator.Validate(calendar, utcNow) is { } error
            ? ([], error)
            : (_calendarEvaluator.NextOccurrencesUtc(calendar, utcNow, Math.Clamp(count, 1, 20)), null);
    }

    /// <summary>
    /// Makes the task's scheduler pick up its stored schedule now: the recurring host re-reads it;
    /// a distributed job's row gets the period it calls for (<paramref name="intervalPeriod"/> unless
    /// it's on a calendar).
    /// </summary>
    private void ApplySchedule(string id, TimeSpan intervalPeriod)
    {
        if (ScheduledTaskIdentity.IsRecurring(id))
        {
            if (_registry.TryGet(id, out var host)) host.SyncWithSettings();
            return;
        }

        var name = id[ScheduledTaskIdentity.DistributedPrefix.Length..];
        if (_distributedJobService is ManagedDistributedJobService managed)
        {
            // From the stored setting, so it's also right if another server changed it meanwhile.
            managed.ApplySchedules([name]);
            return;
        }

        _distributedJobTable.SetPeriods(new Dictionary<string, TimeSpan> { [name] = intervalPeriod });
    }

    public ScheduledTaskCommandResult ClearRuns(string id)
    {
        if (!Exists(id)) return ScheduledTaskCommandResult.NotFound;
        _runLog.Clear(id);
        return ScheduledTaskCommandResult.Ok("History cleared.");
    }

    public ScheduledTaskCommandResult ResetRun(string id) => ResetRun(id, null);

    public ScheduledTaskCommandResult ResetRun(string id, DateTime? runningSinceUtc)
    {
        if (FindDistributed(id) is not { } job)
        {
            return FindRecurring(id) is null
                ? ScheduledTaskCommandResult.NotFound
                : ScheduledTaskCommandResult.Invalid("Only distributed tasks can be reset; a recurring task's run ends when the site stops.");
        }

        if (_runLog.GetStats(id).Current is not null)
            return ScheduledTaskCommandResult.Conflict("The task is running on this server right now.");

        return _distributedJobTable.ResetRunning(job.Name, runningSinceUtc) switch
        {
            DistributedResetResult.Reset => ScheduledTaskCommandResult.Ok("The task is no longer marked as running; it runs again when it's due."),
            DistributedResetResult.NotStuck => ScheduledTaskCommandResult.Conflict(DescribePickedUpAgain(job.Name)),
            _ => ScheduledTaskCommandResult.Conflict("The task isn't marked as running."),
        };
    }

    private string DescribePickedUpAgain(string jobName)
    {
        var since = ReadDistributedRows().TryGetValue(jobName, out var row) ? DateTime.UtcNow - row.LastAttemptedRun : (TimeSpan?)null;
        return since is { } ago && ago >= TimeSpan.Zero
            ? $"It was picked up again {DescribePeriod(TimeSpan.FromSeconds(Math.Max(1, Math.Round(ago.TotalSeconds))))} ago; it isn't stuck."
            : "It was picked up again; it isn't stuck.";
    }

    public ScheduledTaskPropertiesForm? GetProperties(string id)
    {
        if (FindJob(id) is not { } job) return null;

        return ScheduledTaskPropertyValues.PropertiesTypeOf(job) is { } propertiesType
            ? _propertyValues.Form(propertiesType, _settings.Get(id)?.PropertiesJson)
            : new ScheduledTaskPropertiesForm([], new Dictionary<string, JsonNode?>(), false, []);
    }

    public ScheduledTaskCommandResult SetProperties(
        string id,
        IReadOnlyDictionary<string, JsonNode?>? values,
        string? updatedBy)
    {
        if (FindJob(id) is not { } job) return ScheduledTaskCommandResult.NotFound;
        if (ScheduledTaskPropertyValues.PropertiesTypeOf(job) is not { } propertiesType)
            return ScheduledTaskCommandResult.Invalid("This task has no properties.");

        string? json = null;
        if (values is not null)
        {
            var (prepared, errors) = _propertyValues.Prepare(propertiesType, values);
            if (errors is not null)
                return ScheduledTaskCommandResult.Invalid("Some properties need attention.", errors);
            json = prepared;
        }

        _settings.Update(id, state => state with { PropertiesJson = json }, updatedBy);
        return ScheduledTaskCommandResult.Ok(json is null ? "Properties reset to their defaults." : "Properties saved.");
    }

    // Units for describing periods, largest first. Like the dashboard, a month is 30 days and a year
    // 365 days: Umbraco schedules by fixed periods, not calendar dates.
    private static readonly (TimeSpan Size, string Name)[] PeriodUnits =
    [
        (TimeSpan.FromDays(365), "year"),
        (TimeSpan.FromDays(30), "month"),
        (TimeSpan.FromDays(7), "week"),
        (TimeSpan.FromDays(1), "day"),
        (TimeSpan.FromHours(1), "hour"),
        (TimeSpan.FromMinutes(1), "minute"),
    ];

    /// <summary>
    /// Formats a period for messages in the largest unit that expresses it exactly:
    /// <c>1 second</c>, <c>90 minutes</c>, <c>2 weeks</c>, <c>10 years</c>.
    /// </summary>
    internal static string DescribePeriod(TimeSpan period)
    {
        static string Unit(double value, string unit) => $"{value:0.##} {unit}{(value == 1 ? string.Empty : "s")}";

        foreach (var (size, name) in PeriodUnits)
        {
            if (period >= size && period.Ticks % size.Ticks == 0) return Unit(period.Ticks / size.Ticks, name);
        }

        return Unit(period.TotalSeconds, "second");
    }

    private ScheduledTaskInfo DescribeRecurring(
        IRecurringBackgroundJob job,
        IReadOnlyDictionary<string, ScheduledTaskSetting> settings)
    {
        var type = job.GetType();
        var id = ScheduledTaskIdentity.ForRecurring(type);
        settings.TryGetValue(id, out var setting);
        var hosted = _registry.TryGet(id, out var host);
        var stats = _runLog.GetStats(id);

        return new ScheduledTaskInfo(
            id,
            ScheduledTaskIdentity.Humanize(type.Name),
            type.FullName ?? type.Name,
            type.Assembly.GetName().Name,
            ScheduledTaskKind.Recurring,
            IsUmbracoAssembly(type),
            hosted ? host.EffectivePeriod : setting?.PeriodOverride ?? job.Period,
            job.Period,
            job.Delay,
            job.ServerRoles.Select(r => r.ToString()).ToArray(),
            job.ServerRoles.Contains(CurrentServerRole),
            hosted,
            setting?.IsPaused ?? false,
            hosted ? host.IsRunning : stats.Current is not null,
            stats.Current?.StartedUtc,
            false,
            hosted ? host.NextRunUtc : null,
            stats.Last?.StartedUtc,
            stats,
            setting,
            PropertyCount(job))
        {
            MinimumPeriod = _options.CurrentValue.MinimumPeriod,
        };
    }

    private ScheduledTaskInfo DescribeDistributed(
        IDistributedBackgroundJob job,
        IReadOnlyDictionary<string, ScheduledTaskSetting> settings,
        IReadOnlyDictionary<string, DistributedBackgroundJobModel> rows,
        DateTime utcNow)
    {
        var type = job.GetType();
        var id = ScheduledTaskIdentity.ForDistributed(job.Name);
        settings.TryGetValue(id, out var setting);
        rows.TryGetValue(job.Name, out var row);
        var stats = _runLog.GetStats(id);
        var runningHere = stats.Current is not null;
        var isRunning = runningHere || (row is not null && _distributedJobTable.IsRunning(row, utcNow));
        DateTime? runningSince = stats.Current?.StartedUtc
            ?? (row is { IsRunning: true } ? DateTime.SpecifyKind(row.LastAttemptedRun, DateTimeKind.Utc) : null);

        return new ScheduledTaskInfo(
            id,
            ScheduledTaskIdentity.Humanize(job.Name),
            type.FullName ?? type.Name,
            type.Assembly.GetName().Name,
            ScheduledTaskKind.Distributed,
            IsUmbracoAssembly(type),
            row?.Period ?? setting?.PeriodOverride ?? job.Period,
            job.Period,
            null,
            [],
            true,
            _distributedJobService is ManagedDistributedJobService,
            setting?.IsPaused ?? false,
            isRunning,
            runningSince,
            !runningHere && row is not null && _distributedJobTable.IsPossiblyStuck(row, utcNow),
            row is null || isRunning ? null : DistributedJobClock.NextRunUtc(row, DistributedJobClock.IsAlignedToClock(job)),
            row is null ? stats.Last?.StartedUtc : DateTime.SpecifyKind(row.LastRun, DateTimeKind.Utc),
            stats,
            setting,
            PropertyCount(job))
        {
            CalendarUnavailableReason = DistributedJobClock.IsAlignedToClock(job) ? AlignedToClockReason : null,
            MinimumPeriod = DistributedMinimumPeriod(),
        };
    }

    private const string AlignedToClockReason = "This task aligns its runs to the clock, so it can't use a calendar schedule.";

    // Umbraco's poller takes one due job per poll, so a row due on every poll starves the rest.
    private TimeSpan DistributedMinimumPeriod()
    {
        var minimum = _options.CurrentValue.MinimumPeriod;
        var pollPeriod = _distributedJobTable.PollPeriod;
        return pollPeriod > minimum ? pollPeriod : minimum;
    }

    private static int PropertyCount(object job)
        => ScheduledTaskPropertyValues.PropertiesTypeOf(job) is { } propertiesType
            ? ScheduledTaskPropertySchema.For(propertiesType).Count
            : 0;

    private object? FindJob(string id) => (object?)FindRecurring(id) ?? FindDistributed(id);

    private void StartDistributedRun(IDistributedBackgroundJob job, string? triggeredBy)
    {
        IDistributedBackgroundJob runnable = _distributedJobService is ManagedDistributedJobService managed
            ? managed.Track(job, ScheduledTaskTrigger.Manual, triggeredBy)
            : job;
        var stoppingToken = _lifetime.ApplicationStopping;

        // Don't let the request's ExecutionContext (ambient scope, HttpContext) flow into the run.
        using (ExecutionContext.IsFlowSuppressed() ? null : (IDisposable?)ExecutionContext.SuppressFlow())
        {
            // Tracked so shutdown waits for the run and its FinishAsync, as it does for Umbraco's poller.
            _runTracker.Track(Task.Run(async () =>
            {
                try
                {
                    await runnable.ExecuteAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Shutting down.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An exception occurred while running distributed background job '{JobName}'.", job.Name);
                }
                finally
                {
                    try
                    {
                        await _distributedJobService.FinishAsync(job.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "An exception occurred while finishing distributed background job '{JobName}'.", job.Name);
                    }
                }
            }));
        }
    }

    private IReadOnlyDictionary<string, DistributedBackgroundJobModel> ReadDistributedRows()
    {
        try
        {
            return _distributedJobTable.GetAll();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the distributed job table.");
            return new Dictionary<string, DistributedBackgroundJobModel>();
        }
    }

    private bool Exists(string id) => FindRecurring(id) is not null || FindDistributed(id) is not null;

    private IRecurringBackgroundJob? FindRecurring(string id)
        => ScheduledTaskIdentity.IsRecurring(id)
            ? _recurringJobs.FirstOrDefault(j => ScheduledTaskIdentity.ForRecurring(j.GetType()) == id)
            : null;

    private IDistributedBackgroundJob? FindDistributed(string id)
        => ScheduledTaskIdentity.IsDistributed(id)
            ? _distributedJobs.FirstOrDefault(j => ScheduledTaskIdentity.ForDistributed(j.Name) == id)
            : null;

    // Umbraco hosts one service per job type and one schedule per job name, so duplicates are noise.
    private IEnumerable<IRecurringBackgroundJob> DistinctRecurringJobs() => _recurringJobs.DistinctBy(j => j.GetType());

    private IEnumerable<IDistributedBackgroundJob> DistinctDistributedJobs() => _distributedJobs.DistinctBy(j => j.Name);

    private static bool IsUmbracoAssembly(Type type)
        => type.Assembly.GetName().Name?.StartsWith("Umbraco.", StringComparison.Ordinal) == true;
}
