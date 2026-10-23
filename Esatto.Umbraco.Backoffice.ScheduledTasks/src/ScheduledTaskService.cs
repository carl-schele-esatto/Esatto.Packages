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

    /// <summary>Retries and time limit the task runs with (an editor's values over the job's defaults).</summary>
    public EffectiveRunPolicy RunPolicy { get; init; } = EffectiveRunPolicy.None;

    /// <summary>The job's own retries and time limit (<see cref="ScheduledTaskRunPolicyAttribute"/>).</summary>
    public EffectiveRunPolicy DefaultRunPolicy { get; init; } = EffectiveRunPolicy.None;

    /// <summary>Who asked the task's run in progress on this server to stop, if anyone has.</summary>
    public string? StopRequestedBy { get; init; }

    /// <summary>What the job does (<see cref="ScheduledTaskDescriptionAttribute"/>, else Umbraco's canned list); null when unknown.</summary>
    public ScheduledTaskJobDescription? Description { get; init; }
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

    /// <summary>As <see cref="ClearRuns(string)"/>, recording who did it in the change log.</summary>
    ScheduledTaskCommandResult ClearRuns(string id, string? clearedBy) => ClearRuns(id);

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

    /// <summary>As <see cref="ResetRun(string, DateTime?)"/>, recording who did it in the change log.</summary>
    ScheduledTaskCommandResult ResetRun(string id, DateTime? runningSinceUtc, string? resetBy) => ResetRun(id, runningSinceUtc);

    /// <summary>
    /// The task's properties form (see <see cref="IScheduledTaskWithProperties{TProperties}"/>);
    /// no definitions when it has no properties, null when the task doesn't exist.
    /// </summary>
    ScheduledTaskPropertiesForm? GetProperties(string id);

    /// <summary>Saves editor values for the task's properties; null restores all defaults.</summary>
    ScheduledTaskCommandResult SetProperties(string id, IReadOnlyDictionary<string, JsonNode?>? values, string? updatedBy);

    /// <summary>
    /// Asks the task's runs in progress (on this server and others) to stop, or cancels a pending
    /// retry. A job only stops if it observes its cancellation token.
    /// </summary>
    ScheduledTaskCommandResult Stop(string id, string? requestedBy) => throw new NotSupportedException();

    /// <summary>Sets the task's retries and time limit; null restores the job's defaults.</summary>
    ScheduledTaskCommandResult SetRunPolicy(string id, ScheduledTaskRunPolicy? policy, string? updatedBy) => throw new NotSupportedException();

    /// <summary>The task's change log, newest first; null when the task doesn't exist.</summary>
    IReadOnlyList<ScheduledTaskAuditEntry>? GetChanges(string id) => null;

    /// <summary>Every task's editor settings (pause, schedule, properties, run policy), for another environment.</summary>
    ScheduledTasksExport Export() => throw new NotSupportedException();

    /// <summary>
    /// Compares an export with the current settings and, when <paramref name="apply"/>, applies it:
    /// all of it, or nothing when any task's settings can't be used.
    /// </summary>
    /// <param name="resetMissing">Also put the tasks the file leaves out back on their defaults.</param>
    ScheduledTasksImportResult Import(ScheduledTasksExport document, bool apply, bool resetMissing, string? updatedBy)
        => throw new NotSupportedException();
}

internal sealed partial class ScheduledTaskService : IScheduledTaskService
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
    private readonly IScheduledTaskAuditLog? _auditLog;
    private readonly ScheduledTaskRunControl _runControl;
    private readonly IScheduledTaskRunStore? _runStore;

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
        ILogger<ScheduledTaskService> logger,
        IScheduledTaskAuditLog? auditLog = null,
        ScheduledTaskRunControl? runControl = null,
        IScheduledTaskRunStore? runStore = null)
    {
        _auditLog = auditLog;
        _runControl = runControl ?? new ScheduledTaskRunControl(runLog, TimeProvider.System);
        _runStore = runStore;
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

            if (!host.RequestRun(triggeredBy)) return ScheduledTaskCommandResult.Conflict("The task is already running.");
            Audit(id, ScheduledTaskAuditActions.RunStarted, triggeredBy);
            return ScheduledTaskCommandResult.Ok("Run started.");
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
            Audit(id, ScheduledTaskAuditActions.RunStarted, triggeredBy);
            return ScheduledTaskCommandResult.Ok("Run started.");
        }

        return ScheduledTaskCommandResult.NotFound;
    }

    public ScheduledTaskCommandResult SetPaused(string id, bool isPaused, string? updatedBy)
    {
        if (!Exists(id)) return ScheduledTaskCommandResult.NotFound;

        var wasPaused = _settings.Get(id)?.IsPaused == true;
        _settings.Update(id, state => state with { IsPaused = isPaused }, updatedBy);
        if (wasPaused != isPaused) Audit(id, isPaused ? ScheduledTaskAuditActions.Paused : ScheduledTaskAuditActions.Resumed, updatedBy);
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
        var before = DescribeSchedule(_settings.Get(id), defaultPeriod);
        _settings.Update(id, state => state with { PeriodOverride = periodOverride, Calendar = null }, updatedBy);
        ApplySchedule(id, periodOverride ?? defaultPeriod);
        Audit(
            id,
            periodOverride is null ? ScheduledTaskAuditActions.ScheduleReset : ScheduledTaskAuditActions.IntervalChanged,
            updatedBy,
            ScheduledTaskDescriptions.Change(before, ScheduledTaskDescriptions.Schedule(null, periodOverride ?? defaultPeriod)));

        return ScheduledTaskCommandResult.Ok(periodOverride is null ? "Back on the task's own schedule." : "Interval updated.");
    }

    public ScheduledTaskCommandResult SetSchedule(string id, ScheduledTaskCalendar? calendar, string? updatedBy)
    {
        TimeSpan defaultPeriod;
        if (FindRecurring(id) is { } recurring) defaultPeriod = recurring.Period;
        else if (FindDistributed(id) is { } distributed) defaultPeriod = distributed.Period;
        else return ScheduledTaskCommandResult.NotFound;

        var current = _settings.Get(id);
        var before = DescribeSchedule(current, defaultPeriod);
        if (calendar is null)
        {
            _settings.Update(id, state => state with { Calendar = null }, updatedBy);
            ApplySchedule(id, current?.PeriodOverride ?? defaultPeriod);
            Audit(id, ScheduledTaskAuditActions.ScheduleChanged, updatedBy,
                ScheduledTaskDescriptions.Change(before, ScheduledTaskDescriptions.Schedule(null, current?.PeriodOverride ?? defaultPeriod)));
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
        Audit(id, ScheduledTaskAuditActions.ScheduleChanged, updatedBy,
            ScheduledTaskDescriptions.Change(before, ScheduledTaskDescriptions.Calendar(saved)));
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

    public ScheduledTaskCommandResult ClearRuns(string id) => ClearRuns(id, null);

    public ScheduledTaskCommandResult ClearRuns(string id, string? clearedBy)
    {
        if (!Exists(id)) return ScheduledTaskCommandResult.NotFound;
        _runLog.Clear(id);
        Audit(id, ScheduledTaskAuditActions.HistoryCleared, clearedBy);
        return ScheduledTaskCommandResult.Ok("History cleared.");
    }

    public ScheduledTaskCommandResult ResetRun(string id) => ResetRun(id, null);

    public ScheduledTaskCommandResult ResetRun(string id, DateTime? runningSinceUtc) => ResetRun(id, runningSinceUtc, null);

    public ScheduledTaskCommandResult ResetRun(string id, DateTime? runningSinceUtc, string? resetBy)
    {
        var result = ResetRunCore(id, runningSinceUtc);
        if (result.Status == ScheduledTaskCommandStatus.Ok) Audit(id, ScheduledTaskAuditActions.RunReset, resetBy);
        return result;
    }

    private ScheduledTaskCommandResult ResetRunCore(string id, DateTime? runningSinceUtc)
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

        var before = _settings.Get(id)?.PropertiesJson;
        _settings.Update(id, state => state with { PropertiesJson = json }, updatedBy);
        Audit(
            id,
            values is null ? ScheduledTaskAuditActions.PropertiesReset : ScheduledTaskAuditActions.PropertiesChanged,
            updatedBy,
            values is null ? null : ScheduledTaskDescriptions.ChangedProperties(before, json));
        return ScheduledTaskCommandResult.Ok(json is null ? "Properties reset to their defaults." : "Properties saved.");
    }

    public ScheduledTaskCommandResult SetRunPolicy(string id, ScheduledTaskRunPolicy? policy, string? updatedBy)
    {
        if (FindJob(id) is not { } job) return ScheduledTaskCommandResult.NotFound;
        if (policy is not null && ScheduledTaskRunPolicies.Validate(policy) is { } error) return ScheduledTaskCommandResult.Invalid(error);

        // Values equal to the job's own defaults aren't overrides: they keep following the code.
        var defaults = ScheduledTaskRunPolicies.Defaults(job.GetType());
        var overrides = policy is null ? null : WithoutDefaults(policy, defaults);
        var before = ScheduledTaskRunPolicies.Resolve(job.GetType(), _settings.Get(id)?.RunPolicy);

        _settings.Update(id, state => state with { RunPolicy = overrides }, updatedBy);
        var after = ScheduledTaskRunPolicies.Resolve(job.GetType(), overrides);
        if (before != after)
        {
            Audit(id, ScheduledTaskAuditActions.RunPolicyChanged, updatedBy,
                ScheduledTaskDescriptions.Change(ScheduledTaskRunPolicies.Describe(before), ScheduledTaskRunPolicies.Describe(after)));
        }
        return ScheduledTaskCommandResult.Ok(overrides is null ? "Retries and time limit back to the task's defaults." : "Retries and time limit saved.");
    }

    private static ScheduledTaskRunPolicy? WithoutDefaults(ScheduledTaskRunPolicy policy, EffectiveRunPolicy defaults)
    {
        var timeoutDefault = defaults.Timeout ?? TimeSpan.Zero;
        var trimmed = new ScheduledTaskRunPolicy(
            policy.Retries == defaults.Retries ? null : policy.Retries,
            policy.RetryDelay == defaults.RetryDelay ? null : policy.RetryDelay,
            policy.Timeout == timeoutDefault ? null : policy.Timeout);
        return trimmed.IsEmpty ? null : trimmed;
    }

    public ScheduledTaskCommandResult Stop(string id, string? requestedBy)
    {
        if (!Exists(id)) return ScheduledTaskCommandResult.NotFound;

        // Here, and on every other server that saved a run of the task as in progress.
        var stoppedHere = _runControl.RequestStop(id, requestedBy);
        var otherServers = new List<string>();
        if (_runStore is { IsAvailable: true } store)
        {
            try
            {
                // Running there, or waiting in-process for a retry there (a recurring task's wait).
                foreach (var run in store.GetActive(id).Where(r => !string.Equals(r.Server, Environment.MachineName, StringComparison.OrdinalIgnoreCase)))
                {
                    if (store.RequestStop(run.Id, requestedBy ?? "someone")) otherServers.Add(run.Server);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not ask other servers to stop scheduled task {TaskId}", id);
            }
        }

        if (stoppedHere || otherServers.Count > 0)
        {
            Audit(id, ScheduledTaskAuditActions.StopRequested, requestedBy,
                otherServers.Count == 0 ? null : $"On {string.Join(", ", otherServers.Distinct(StringComparer.OrdinalIgnoreCase))}");
            return ScheduledTaskCommandResult.Ok(stoppedHere
                ? "Asked the task to stop. It stops as soon as it checks its cancellation token."
                : "Asked the server running the task to stop it; it does within a few seconds, if the task checks its cancellation token.");
        }

        // A distributed task waiting for its retry isn't running anywhere: cancel the retry.
        if (_runLog.GetPendingRetry(id, fresh: true) is { } failed)
        {
            _runLog.SetRetry(id, failed.Id, null);
            if (ScheduledTaskIdentity.IsDistributed(id) && FindDistributed(id) is { } distributed)
                ApplySchedule(id, _settings.Get(id)?.PeriodOverride ?? distributed.Period);
            Audit(id, ScheduledTaskAuditActions.RetryCanceled, requestedBy);
            return ScheduledTaskCommandResult.Ok("Retry canceled.");
        }

        return ScheduledTaskCommandResult.Conflict("The task isn't running.");
    }

    public IReadOnlyList<ScheduledTaskAuditEntry>? GetChanges(string id)
    {
        if (!Exists(id)) return null;
        if (_auditLog is null) return [];
        try
        {
            return _auditLog.Get(id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the change log of scheduled task {TaskId}", id);
            return [];
        }
    }

    /// <summary>Who asked the task's run on another server to stop, per the saved run; null when nobody has.</summary>
    private string? RemoteStopRequestedBy(string id)
    {
        if (_runStore is not { IsAvailable: true } store) return null;
        try
        {
            return store.GetRunning(id).FirstOrDefault(r => r.StopRequestedBy is not null)?.StopRequestedBy;
        }
        catch
        {
            return null; // the dashboard still works without it
        }
    }

    private void Audit(string id, string action, string? performedBy, string? details = null)
        => _auditLog?.Write(id, action, performedBy, details);

    private static string DescribeSchedule(ScheduledTaskSetting? setting, TimeSpan defaultPeriod)
        => ScheduledTaskDescriptions.Schedule(setting?.Calendar, setting?.PeriodOverride ?? defaultPeriod);

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
            RunPolicy = ScheduledTaskRunPolicies.Resolve(type, setting?.RunPolicy),
            DefaultRunPolicy = ScheduledTaskRunPolicies.Defaults(type),
            StopRequestedBy = _runControl.StopRequestedBy(id),
            Description = ScheduledTaskJobDescriptions.For(type),
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
            // While a retry is pending the row's period is the wait for it, not the task's interval.
            (stats.PendingRetry is null ? row?.Period : null) ?? setting?.PeriodOverride ?? job.Period,
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
            RunPolicy = ScheduledTaskRunPolicies.Resolve(type, setting?.RunPolicy),
            DefaultRunPolicy = ScheduledTaskRunPolicies.Defaults(type),
            StopRequestedBy = _runControl.StopRequestedBy(id)
                ?? stats.Current?.StopRequestedBy
                ?? (isRunning && !runningHere ? RemoteStopRequestedBy(id) : null),
            Description = ScheduledTaskJobDescriptions.For(type),
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
