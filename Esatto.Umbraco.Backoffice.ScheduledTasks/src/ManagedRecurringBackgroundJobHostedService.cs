using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Infrastructure.HostedServices;
using Umbraco.Cms.Infrastructure.Notifications;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// Umbraco's recurring job host with editor controls: pause, interval override, run now, and a
/// run log with error details.
/// </summary>
/// <remarks>
/// <para>
/// Derives from <see cref="RecurringBackgroundJobHostedService{TJob}"/>, so start/stop and the
/// period-changed wiring keep working, and the same <c>RecurringBackgroundJob*Notification</c>s are
/// published with the original job. <see cref="PerformExecuteAsync(CancellationToken)"/> is
/// re-implemented (mirroring Umbraco 18.2's checks and back-off) because the base swallows the
/// job's exception, which the run log needs, and has no hook to skip a paused tick.
/// </para>
/// <para>
/// Runs requested in the dashboard (<see cref="RequestRun"/>) bypass the pause; runs triggered from
/// code through <see cref="IRecurringBackgroundJobTrigger{TJob}"/> (routed here by
/// <see cref="ManagedRecurringBackgroundJobTrigger{TJob}"/>) respect it. Neither bypasses
/// Umbraco's runtime-level, server-role and MainDom checks.
/// </para>
/// <para>
/// Long intervals: Umbraco's loop waits on a .NET timer, which can't wait longer than about 49.7
/// days (it throws, faulting the hosted service). Intervals above <see cref="MaxTimerPeriod"/> are
/// therefore scheduled as a <see cref="Heartbeat"/>: the loop ticks hourly, and ticks that come
/// before the interval has passed since the last scheduled run are skipped silently (no
/// notifications, no run log entry), so from the outside the job simply has a long period. Trigger
/// strategies (<see cref="NextExecutionStrategy"/>, custom next delay) are applied to that schedule.
/// Triggers never hand the loop a wait above <see cref="MaxTimerPeriod"/> either: a next run further
/// out (a custom delay, or <see cref="NextExecutionStrategy.Replace"/> pushing the schedule out) is
/// reached in steps the loop can wait, skipping the ticks in between silently. Should the loop still
/// fault, it is logged and restarted (<see cref="ExecuteAsync(CancellationToken)"/>).
/// </para>
/// <para>
/// Calendar schedules (<see cref="ScheduledTaskCalendar"/>, e.g. every Monday at 07:00): the loop
/// ticks every <see cref="CalendarTick"/> and a tick counts once an occurrence has passed since the
/// last one handled (or since the schedule was saved). Handled occurrences are recorded in the
/// database per server (the job runs on every server whose role matches), so a restart neither
/// repeats one nor loses one that fell while the site was down (a server with no record of a
/// schedule saved before it started, e.g. a new one, counts from its start); there is no extra run <c>Delay</c>
/// after startup, and a <c>Delay</c> longer than a tick (even an infinite one) doesn't hold the
/// calendar up (see <see cref="WakeForCalendarIfWaiting"/>). An occurrence while paused is skipped;
/// one Umbraco blocks on this server (runtime level, role, MainDom) is retried on the next tick.
/// Switching back to an interval counts the interval from the switch.
/// </para>
/// <para>
/// Until the settings have been read from the database
/// (<see cref="IScheduledTaskSettingsStore.IsWaitingForDatabase"/>) only dashboard runs run: it
/// isn't known yet whether the task is paused.
/// </para>
/// <para>
/// Interval changes: Umbraco's loop bases each wait on the period it read before the previous wait,
/// and only notices a change while it is waiting. A change made at any other time (e.g. during a
/// tick) is applied again by the next <see cref="SyncWithSettings"/>, which
/// <see cref="ScheduledTaskSettingsSynchronizer"/> calls every
/// <see cref="ScheduledTasksOptions.SettingsRefreshInterval"/>.
/// </para>
/// </remarks>
public class ManagedRecurringBackgroundJobHostedService<TJob> : RecurringBackgroundJobHostedService<TJob>, IManagedRecurringJobHost
    where TJob : IRecurringBackgroundJob
{
    /// <summary>Longest period handed to Umbraco's timer loop; comfortably below the ~24.8-day Int32 and ~49.7-day UInt32 millisecond timer limits.</summary>
    internal static readonly TimeSpan MaxTimerPeriod = TimeSpan.FromDays(24);

    /// <summary>Tick interval for periods above <see cref="MaxTimerPeriod"/>.</summary>
    internal static readonly TimeSpan Heartbeat = TimeSpan.FromHours(1);

    /// <summary>Tick interval on a calendar schedule: an occurrence runs at most this much late.</summary>
    internal static readonly TimeSpan CalendarTick = TimeSpan.FromSeconds(30);

    // Heartbeat ticks land a hair before the exact due time; don't let that cost a whole extra hour.
    private static readonly TimeSpan DueTolerance = TimeSpan.FromMinutes(1);

    // Shortest wait after a paused tick, so a job with a zero period doesn't spin while paused.
    private static readonly TimeSpan MinPausedBackOff = TimeSpan.FromSeconds(1);

    // Longest wait a .NET timer accepts (4,294,967,294 ms, about 49.7 days); only used where the loop
    // can't be stepped (zero or infinite period).
    private static readonly TimeSpan MaxTimerWait = TimeSpan.FromMilliseconds(uint.MaxValue - 1d);

    // Wait before restarting a faulted timer loop; doubles while it keeps faulting, up to an hour.
    internal static readonly TimeSpan LoopRestartBackOff = TimeSpan.FromMinutes(1);

    private readonly IRuntimeState _runtimeState;
    private readonly ILogger _logger;
    private readonly IMainDom _mainDom;
    private readonly IServerRoleAccessor _serverRoleAccessor;
    private readonly IEventAggregator _eventAggregator;
    private readonly IEventMessagesFactory _eventMessagesFactory;
    private readonly TJob _job;
    private readonly TimeProvider _timeProvider;
    private readonly IScheduledTaskSettingsStore _settings;
    private readonly IScheduledTaskRunLog _runLog;
    private readonly IOptionsMonitor<ScheduledTasksOptions> _options;
    private readonly ScheduledTaskCalendarEvaluator _calendarEvaluator;
    private readonly ScheduledTaskRunControl _runControl;
    private readonly Lock _periodLock = new();

    private CancellationTokenSource _ignoredDelayChangeCts = new();
    private CancellationTokenSource _wakeCts = new();
    private PendingRun? _pendingRun;
    private int _running;
    private int _insideTick;
    private TimeSpan _effectivePeriod;
    private TimeSpan _timerPeriod;
    private TimeSpan? _periodOverride;
    private bool _periodReapplyPending;
    private DateTime? _firstRunUtc;
    private DateTime? _lastScheduledStartUtc;
    private ScheduledTaskCalendar? _calendar;
    private DateTime? _lastHandledOccurrenceUtc;

    // The SavedUtc of the calendar _lastHandledOccurrenceUtc belongs to.
    private DateTime? _handledForSavedUtc;

    // When this host was created (the site started): a calendar this server has no record of counts
    // from here if it was saved earlier (see ApplySettings).
    private readonly DateTime _hostStartedUtc;

    // Whether Umbraco's loop has ticked since it (re)started, i.e. is past the job's initial Delay;
    // and whether it was woken early for a calendar during that Delay.
    private int _loopTicked;
    private bool _wokenForCalendar;

    // Off the heartbeat: scheduled ticks before this are skipped silently, because a trigger moved the
    // next run further out than the loop can wait in one go (see LoopDelayUntil).
    private DateTime? _notBeforeUtc;

    // TriggerExecution(Timeout.InfiniteTimeSpan): Umbraco's manually triggered mode. Only triggers run
    // the job until a Reset or custom-delay trigger, or a real change of the interval or schedule.
    private bool _manualMode;

    public ManagedRecurringBackgroundJobHostedService(
        IRuntimeState runtimeState,
        ILogger<RecurringBackgroundJobHostedService<TJob>> logger,
        IMainDom mainDom,
        IServerRoleAccessor serverRoleAccessor,
        IEventAggregator eventAggregator,
        IEventMessagesFactory eventMessagesFactory,
        TJob job,
        TimeProvider timeProvider,
        IScheduledTaskSettingsStore settings,
        IScheduledTaskRunLog runLog,
        IOptionsMonitor<ScheduledTasksOptions> options,
        ScheduledTaskCalendarEvaluator calendarEvaluator,
        RecurringJobHostRegistry registry,
        ScheduledTaskRunControl? runControl = null)
        : base(runtimeState, logger, mainDom, serverRoleAccessor, eventAggregator, eventMessagesFactory, job, timeProvider)
    {
        _calendarEvaluator = calendarEvaluator;
        _runControl = runControl ?? new ScheduledTaskRunControl(runLog, timeProvider);
        _runtimeState = runtimeState;
        _logger = logger;
        _mainDom = mainDom;
        _serverRoleAccessor = serverRoleAccessor;
        _eventAggregator = eventAggregator;
        _eventMessagesFactory = eventMessagesFactory;
        _job = job;
        _timeProvider = timeProvider;
        _settings = settings;
        _runLog = runLog;
        _options = options;
        _effectivePeriod = job.Period;
        _timerPeriod = job.Period;
        _hostStartedUtc = UtcNow();

        TaskId =ScheduledTaskIdentity.ForRecurring(job.GetType());

        // The base handed the job's own period to the timer loop; cap it before the loop starts.
        SetTimerPeriod(TimerPeriodFor(job.Period));

        // Subscribed after the base's handler, so an override wins over the job's own new period.
        _job.PeriodChanged += OnJobPeriodChanged;
        _job.IgnoredDelayChanged += OnIgnoredDelayChanged;

        registry.Register(this);
    }

    public string TaskId { get; }

    public IRecurringBackgroundJob Job => _job;

    public TimeSpan EffectivePeriod
    {
        get { lock (_periodLock) return _effectivePeriod; }
    }

    /// <summary>The period Umbraco's timer loop is actually waiting with (see <see cref="MaxTimerPeriod"/>).</summary>
    internal TimeSpan TimerPeriod
    {
        get { lock (_periodLock) return _timerPeriod; }
    }

    public bool IsRunning => Volatile.Read(ref _running) == 1;

    public DateTime? NextRunUtc
    {
        get
        {
            if (CurrentCalendar() is { } calendar) return NextCalendarOccurrence(calendar);

            DateTime? notBefore;
            lock (_periodLock)
            {
                if (_manualMode) return null;
                notBefore = _notBeforeUtc;
            }

            var lastScheduledStart = _lastScheduledStartUtc;
            if (lastScheduledStart is null) return notBefore ?? _firstRunUtc;

            var period = EffectivePeriod;
            if (period == Timeout.InfiniteTimeSpan) return null;

            var next = AddClamped(lastScheduledStart.Value, period);
            return notBefore > next ? notBefore : next;
        }
    }

    public bool RequestRun(string? triggeredBy)
    {
        if (IsRunning) return false;

        // Strategy None: run now, then carry on with the original schedule.
        return Signal(
            new PendingRun(ScheduledTaskTrigger.Manual, triggeredBy, NextExecutionStrategy.None, null),
            () => TriggerExecution(NextExecutionStrategy.None));
    }

    public bool Trigger(NextExecutionStrategy strategy)
        => Signal(
            new PendingRun(ScheduledTaskTrigger.Code, null, strategy, null),
            () => TriggerLoop(strategy));

    public bool Trigger(TimeSpan nextDelay)
    {
        // As Umbraco's own TriggerExecution(TimeSpan) does, before anything is recorded.
        if (nextDelay != Timeout.InfiniteTimeSpan) ArgumentOutOfRangeException.ThrowIfLessThan(nextDelay, TimeSpan.Zero);

        return Signal(
            new PendingRun(ScheduledTaskTrigger.Code, null, NextExecutionStrategy.None, nextDelay),
            () => TriggerLoop(nextDelay));
    }

    public void SyncWithSettings()
    {
        // Before the settings could be read they look like defaults; don't apply those.
        var setting = _settings.Get(TaskId);
        if (!_settings.IsWaitingForDatabase) ApplySettings(setting);

        lock (_periodLock)
        {
            if (!_periodReapplyPending || Volatile.Read(ref _insideTick) == 1) return;

            // The loop is (almost certainly) waiting now: make it recompute its wait with the current period.
            _periodReapplyPending = false;
            ChangePeriod(_timerPeriod);
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _firstRunUtc = FirstRunFromNow();
        await base.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Runs Umbraco's timer loop, restarting it should it ever fault. A faulted loop would otherwise
    /// stop the job silently until the site restarts: Umbraco's job runner starts this host itself, so
    /// nothing observes the fault.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var backOff = LoopRestartBackOff;
        while (!stoppingToken.IsCancellationRequested)
        {
            var loopStart = _timeProvider.GetTimestamp();
            try
            {
                // Named, so it doesn't bind to the obsolete ExecuteAsync(object? state).
                await base.ExecuteAsync(stoppingToken: stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // A loop that ran for a while before faulting starts the back-off afresh.
                if (_timeProvider.GetElapsedTime(loopStart) > Heartbeat) backOff = LoopRestartBackOff;
                _logger.LogError(ex, "The timer loop of scheduled task {TaskId} stopped unexpectedly; restarting it in {BackOff}.", TaskId, backOff);
            }

            try
            {
                await Task.Delay(backOff, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            backOff = backOff * 2 > Heartbeat ? Heartbeat : backOff * 2;
            PrepareLoopRestart();
        }
    }

    public override async Task PerformExecuteAsync(CancellationToken stoppingToken)
    {
        Volatile.Write(ref _loopTicked, 1);
        Volatile.Write(ref _insideTick, 1);
        try
        {
            await ExecuteTickAsync(stoppingToken);
        }
        finally
        {
            Volatile.Write(ref _insideTick, 0);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _job.PeriodChanged -= OnJobPeriodChanged;
            _job.IgnoredDelayChanged -= OnIgnoredDelayChanged;
            _ignoredDelayChangeCts.Dispose();
            _wakeCts.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task ExecuteTickAsync(CancellationToken stoppingToken)
    {
        var pendingRun = Interlocked.Exchange(ref _pendingRun, null);
        var trigger = pendingRun?.Trigger ?? ScheduledTaskTrigger.Schedule;

        var setting = _settings.Get(TaskId);
        // Until the settings could be read it isn't known whether the task is paused (or on which
        // schedule); such ticks don't run it (below), and the defaults they look like aren't applied.
        var waitingForSettings = _settings.IsWaitingForDatabase;
        var calendarRemoved = !waitingForSettings && ApplySettings(setting);

        var now = UtcNow();
        var calendar = CurrentCalendar();
        var onHeartbeat = IsOnHeartbeat();
        if (pendingRun is null)
        {
            if (calendar is not null)
            {
                // A calendar tick only counts once an occurrence has passed (including one missed
                // while the site was down); otherwise it isn't a real tick.
                if (NextCalendarOccurrence(calendar) is not { } occurrence || occurrence > now) return;
            }
            else if (calendarRemoved)
            {
                // A tick of the calendar's timer that finds it replaced by an interval: the interval
                // counts from the switch, so this isn't a run.
                return;
            }
            else
            {
                // A heartbeat tick of a long interval that isn't due yet isn't a real tick, nor is a
                // tick in manual mode or one on the way to a next run further out than a timer can wait.
                if (!IsScheduledTickDue(now, onHeartbeat)) return;
                // On the real period every tick is a slot, used or not (as in Umbraco's loop). On a
                // heartbeat only a run uses the slot (below), so a blocked or paused task catches up
                // within the hour once it can run, instead of waiting another whole interval.
                if (!onHeartbeat) _lastScheduledStartUtc = now;
            }
        }
        else if (calendar is null && !onHeartbeat)
        {
            // Off the heartbeat Umbraco's loop applies the trigger's strategy whether or not the job
            // then runs (paused, or blocked on this server), as with a scheduled slot. Keep the schedule
            // in step with it, or later Replace triggers misjudge how far out the loop already waits.
            AdjustScheduleForTriggeredRun(pendingRun, now, onHeartbeat: false);
        }

        var eventMessages = _eventMessagesFactory.Get();
        var executingNotification = new RecurringBackgroundJobExecutingNotification(_job, eventMessages);
        await _eventAggregator.PublishAsync(executingNotification, stoppingToken);

        try
        {
            if (GetBlockedReason() is { } blockedReason)
            {
                _runLog.Skip(TaskId, blockedReason, trigger, pendingRun?.TriggeredBy, PersistsAllRuns());
                await IgnoreAndWaitAsync(blockedReason, eventMessages, executingNotification, stoppingToken);
                return;
            }

            if (waitingForSettings && trigger != ScheduledTaskTrigger.Manual)
            {
                // Fail closed: a paused task must not run because its pause couldn't be read yet.
                const string waitingReason = "The Scheduled Tasks settings haven't been read from the database yet, so it isn't known whether the task is paused.";
                _runLog.Skip(TaskId, waitingReason, trigger, pendingRun?.TriggeredBy, PersistsAllRuns());
                await IgnoreAndWaitAsync(waitingReason, eventMessages, executingNotification, stoppingToken);
                return;
            }

            if (setting?.IsPaused == true && trigger != ScheduledTaskTrigger.Manual)
            {
                // A calendar occurrence that falls while paused is skipped, not postponed to the resume.
                if (pendingRun is null && calendar is not null) MarkOccurrenceHandled(calendar, now);

                // Paused ticks are cheap and frequent, so they are not logged as runs.
                _logger.LogDebug("Job not running as it is paused in the Scheduled Tasks dashboard");
                await _eventAggregator.PublishAsync(
                    new RecurringBackgroundJobIgnoredNotification(_job, eventMessages).WithStateFrom(executingNotification),
                    stoppingToken);
                await WaitWhilePausedAsync(stoppingToken);
                return;
            }

            if (pendingRun is null)
            {
                if (calendar is not null) MarkOccurrenceHandled(calendar, now);
                else if (onHeartbeat) _lastScheduledStartUtc = now;
            }
            else if (calendar is null && onHeartbeat)
            {
                // On a calendar, extra runs don't move the schedule; off the heartbeat it moved above.
                AdjustScheduleForTriggeredRun(pendingRun, now, onHeartbeat);
            }

            Volatile.Write(ref _running, 1);
            var result = await ExecuteWithPolicyAsync(setting, trigger, pendingRun?.TriggeredBy, stoppingToken);

            switch (result.Outcome)
            {
                case AttemptOutcome.Succeeded:
                    await _eventAggregator.PublishAsync(
                        new RecurringBackgroundJobExecutedNotification(_job, eventMessages).WithStateFrom(executingNotification),
                        stoppingToken);
                    break;

                case AttemptOutcome.Failed:
                    _logger.LogError(result.Exception!.SourceException, "Unhandled exception in recurring background job.");
                    await _eventAggregator.PublishAsync(
                        new RecurringBackgroundJobFailedNotification(_job, eventMessages).WithStateFrom(executingNotification),
                        stoppingToken);
                    break;

                default:
                    _logger.LogDebug(result.Outcome == AttemptOutcome.Stopped ? "Job stopped from the Scheduled Tasks dashboard." : "Job canceled during shutdown.");
                    await _eventAggregator.PublishAsync(
                        new RecurringBackgroundJobCanceledNotification(_job, eventMessages).WithStateFrom(executingNotification),
                        CancellationToken.None);
                    break;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Job canceled during shutdown.");
        }
        catch (Exception ex)
        {
            // A notification handler threw; the run itself is already recorded.
            _logger.LogError(ex, "Unhandled exception in recurring background job.");
            await _eventAggregator.PublishAsync(
                new RecurringBackgroundJobFailedNotification(_job, eventMessages).WithStateFrom(executingNotification),
                stoppingToken);
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }

    /// <summary>
    /// Runs the job with its run policy: within its time limit, and again after a failure as often
    /// as it allows, waiting in between (the task shows as running meanwhile). A stop from the
    /// dashboard ends the attempt (if the job observes its token) and any retries; a pause ends a
    /// scheduled run's retries.
    /// </summary>
    private async Task<AttemptResult> ExecuteWithPolicyAsync(
        ScheduledTaskSetting? setting,
        ScheduledTaskTrigger trigger,
        string? triggeredBy,
        CancellationToken stoppingToken)
    {
        var policy = ScheduledTaskRunPolicies.Resolve(_job.GetType(), setting?.RunPolicy);
        var persistAll = PersistsAllRuns();
        using var execution = _runControl.Begin(TaskId, stoppingToken);

        for (var attempt = 1; ; attempt++)
        {
            var runId = attempt == 1
                ? _runLog.Start(TaskId, trigger, triggeredBy, attempt, policy.MaxAttempts, persistAll)
                : _runLog.Start(TaskId, ScheduledTaskTrigger.Retry, null, attempt, policy.MaxAttempts, persistAll);
            var result = await ScheduledTaskAttempts.RunAsync(
                _runLog, execution, TaskId, runId, policy.Timeout, _options.CurrentValue.RunLogMinimumLevel, _job.RunJobAsync);
            if (result.Outcome != AttemptOutcome.Failed || attempt >= policy.MaxAttempts || IsPausedFor(trigger)) return result;

            _runLog.SetRetry(TaskId, runId, AddClamped(UtcNow(), policy.RetryDelay));
            try
            {
                await Task.Delay(policy.RetryDelay, _timeProvider, execution.Token);
            }
            catch (OperationCanceledException)
            {
                _runLog.SetRetry(TaskId, runId, null);
                return result with { Outcome = execution.IsShuttingDown ? AttemptOutcome.ShutDown : AttemptOutcome.Stopped };
            }

            if (IsPausedFor(trigger))
            {
                _runLog.SetRetry(TaskId, runId, null);
                return result;
            }
        }
    }

    // A pause stops the retries of a scheduled or triggered run; one started from the dashboard keeps going.
    private bool IsPausedFor(ScheduledTaskTrigger trigger)
        => trigger != ScheduledTaskTrigger.Manual && _settings.Get(TaskId)?.IsPaused == true;

    private bool PersistsAllRuns()
        => ScheduledTaskAttempts.PersistsAllRuns(EffectivePeriod, CurrentCalendar() is not null, _options.CurrentValue);

    /// <summary>Umbraco's reasons not to run on this server right now, or null when clear to run.</summary>
    private string? GetBlockedReason()
    {
        if (_runtimeState.Level != RuntimeLevel.Run)
            return $"Umbraco is not ready yet (runtime level {_runtimeState.Level}).";

        var role = _serverRoleAccessor.CurrentServerRole;
        if (!_job.ServerRoles.Contains(role))
            return $"Not configured to run on this server's role ({role}); runs on {string.Join(", ", _job.ServerRoles)}.";

        if (!_mainDom.IsMainDom)
            return "This instance is not the main domain (MainDom).";

        return null;
    }

    /// <summary>
    /// Records a run request and signals the loop. A dashboard request isn't downgraded by a code
    /// trigger that arrives before the tick picks it up.
    /// </summary>
    private bool Signal(PendingRun run, Action triggerLoop)
    {
        var previous = Volatile.Read(ref _pendingRun);
        // The run keeps the dashboard's attribution but takes this trigger's strategy, which is the one
        // Umbraco's loop will apply (the latest trigger wins there), so the schedule stays in step.
        var recorded = run.Trigger == ScheduledTaskTrigger.Code && previous?.Trigger == ScheduledTaskTrigger.Manual
            ? previous with { Strategy = run.Strategy, NextDelay = run.NextDelay }
            : run;
        Volatile.Write(ref _pendingRun, recorded);

        try
        {
            triggerLoop();
        }
        catch (ObjectDisposedException)
        {
            Interlocked.CompareExchange(ref _pendingRun, null, recorded);
            return false;
        }
        catch
        {
            Interlocked.CompareExchange(ref _pendingRun, previous, recorded);
            throw;
        }

        // Cut short a paused tick's back-off so the run starts now.
        RotateAndCancel(ref _wakeCts);
        return true;
    }

    /// <summary>
    /// Moves the schedule the way Umbraco's loop would for the trigger's strategy, so the next-run
    /// estimate is right and, on a heartbeat, the next due tick is too.
    /// </summary>
    private void AdjustScheduleForTriggeredRun(PendingRun run, DateTime now, bool onHeartbeat)
    {
        var period = EffectivePeriod;
        var last = _lastScheduledStartUtc;
        var finite = period != Timeout.InfiniteTimeSpan;

        _lastScheduledStartUtc = run switch
        {
            // Next run after the custom delay.
            { NextDelay: { } delay } when finite && delay != Timeout.InfiniteTimeSpan => AddClamped(now, delay - period),
            // Next run a whole period after this one.
            { Strategy: NextExecutionStrategy.Reset } => now,
            // This run takes the place of the next scheduled one (or of an overdue one, as Umbraco's
            // loop does when its wait has already run out).
            { Strategy: NextExecutionStrategy.Replace } when finite && last is { } previous
                => AddClamped(previous, period) is var replaced && replaced > now ? replaced : now,
            // On a heartbeat, a run when the slot is due or overdue (e.g. Run now on a task paused past
            // its due date) takes that slot, as a tick of Umbraco's loop would have just come round.
            _ when onHeartbeat && finite && last is { } previous && now - previous >= period - DueTolerance => now,
            // Keep the schedule; before the first scheduled run, it starts from this run (as Umbraco does).
            _ => last ?? now,
        };
    }

    /// <summary>
    /// Hands a code trigger's strategy to Umbraco's loop. <see cref="NextExecutionStrategy.Replace"/>
    /// makes the loop wait out what's left of its current wait plus a whole period, so repeated
    /// triggers push it out further each time, eventually past what a timer can wait (which faults the
    /// loop). On a calendar or heartbeat the emulated schedule already decides when the job is due, so
    /// the loop just keeps its ticks; otherwise a next run beyond <see cref="MaxTimerPeriod"/> is
    /// reached in steps (see <see cref="LoopDelayUntil"/>).
    /// </summary>
    private void TriggerLoop(NextExecutionStrategy strategy)
    {
        lock (_periodLock)
        {
            if (strategy == NextExecutionStrategy.Reset) ClearTriggeredSchedule();

            if (strategy != NextExecutionStrategy.Replace)
            {
                TriggerExecution(strategy);
                return;
            }

            if (_calendar is not null || IsOnHeartbeat())
            {
                TriggerExecution(NextExecutionStrategy.None);
                return;
            }

            var period = _effectivePeriod;
            var now = UtcNow();
            if (!_manualMode && IsSteppable(period) && _lastScheduledStartUtc is { } last)
            {
                // Where AdjustScheduleForTriggeredRun will put the run after the triggered one.
                var next = _notBeforeUtc ?? AddClamped(last, period);
                var target = AddClamped(next > now ? next : now, period);
                if (_notBeforeUtc is not null || target - now > MaxTimerPeriod)
                {
                    _notBeforeUtc = target;
                    TriggerExecution(LoopDelayUntil(target - now, period));
                    return;
                }
            }

            TriggerExecution(strategy);
        }
    }

    /// <summary>
    /// Hands a code trigger's custom next delay to Umbraco's loop: as is when a timer can wait it,
    /// otherwise in steps. On a heartbeat the loop gets the real delay too (then ticks hourly again),
    /// so a short delay isn't rounded up to the next heartbeat. <see cref="Timeout.InfiniteTimeSpan"/>
    /// is Umbraco's manually triggered mode.
    /// </summary>
    private void TriggerLoop(TimeSpan nextDelay)
    {
        lock (_periodLock)
        {
            ClearTriggeredSchedule();

            if (_calendar is not null)
            {
                // The calendar decides what's next.
                TriggerExecution(CalendarTick);
                return;
            }

            var onHeartbeat = IsOnHeartbeat();
            if (nextDelay == Timeout.InfiniteTimeSpan)
            {
                // On a heartbeat the loop keeps ticking, so a later change of the interval is noticed;
                // the ticks are skipped while in manual mode.
                _manualMode = true;
                TriggerExecution(onHeartbeat ? Heartbeat : Timeout.InfiniteTimeSpan);
                return;
            }

            if (nextDelay <= MaxTimerPeriod)
            {
                TriggerExecution(nextDelay);
                return;
            }

            // _timerPeriod is the heartbeat or the real period: the loop's ticks after the first wait.
            if (!IsSteppable(_timerPeriod))
            {
                // A zero or infinite period can't be stepped; Umbraco's loop itself waits up to ~49.7 days.
                TriggerExecution(nextDelay > MaxTimerWait ? MaxTimerWait : nextDelay);
                return;
            }

            // On a heartbeat IsDue already skips the ticks before the next run.
            if (!onHeartbeat) _notBeforeUtc = AddClamped(UtcNow(), nextDelay);
            TriggerExecution(LoopDelayUntil(nextDelay, _timerPeriod));
        }
    }

    /// <summary>
    /// The first wait to hand the loop so that its later ticks, one <paramref name="timerPeriod"/>
    /// apart, land on a run <paramref name="untilTarget"/> from now: the whole wait if a timer can
    /// wait it, otherwise that less as few periods as bring it within <see cref="MaxTimerPeriod"/>.
    /// </summary>
    internal static TimeSpan LoopDelayUntil(TimeSpan untilTarget, TimeSpan timerPeriod)
    {
        if (untilTarget <= MaxTimerPeriod) return untilTarget;

        var periods = (untilTarget.Ticks - MaxTimerPeriod.Ticks + timerPeriod.Ticks - 1) / timerPeriod.Ticks;
        return TimeSpan.FromTicks(untilTarget.Ticks - periods * timerPeriod.Ticks);
    }

    // Ticks on a zero period would spin, and an infinite one never comes, so neither can step towards a run.
    private static bool IsSteppable(TimeSpan period) => period != Timeout.InfiniteTimeSpan && period > TimeSpan.Zero;

    /// <summary>
    /// Whether a scheduled (not triggered) tick off a calendar counts: not in manual mode, on a
    /// heartbeat once the interval has passed, otherwise once the next run a trigger set (if any) is due.
    /// </summary>
    private bool IsScheduledTickDue(DateTime utcNow, bool onHeartbeat)
    {
        lock (_periodLock)
        {
            if (_manualMode) return false;
            if (onHeartbeat) return IsDue(utcNow);

            if (_notBeforeUtc is { } notBefore)
            {
                if (utcNow < notBefore - DueTolerance) return false;
                _notBeforeUtc = null;
            }

            return true;
        }
    }

    /// <summary>
    /// Forgets a next run set by an earlier trigger and leaves manual mode. Call with
    /// <see cref="_periodLock"/> held.
    /// </summary>
    /// <param name="wakeLoop">
    /// Whether to make the loop recompute its wait (it may be waiting indefinitely in manual mode).
    /// Not for triggers: they wake the loop themselves, and waking it twice could run the job twice.
    /// </param>
    private void ClearTriggeredSchedule(bool wakeLoop = false)
    {
        _notBeforeUtc = null;
        if (!_manualMode) return;

        _manualMode = false;
        if (!wakeLoop) return;

        ChangePeriod(_timerPeriod);
        _periodReapplyPending = true;
    }

    /// <summary>Before Umbraco's loop is started again after a fault.</summary>
    private void PrepareLoopRestart()
    {
        // The restarted loop runs a tick after the job's Delay; don't let that be an extra run.
        lock (_periodLock)
        {
            if (_lastScheduledStartUtc is { } last && _calendar is null && !IsOnHeartbeat() && IsSteppable(_effectivePeriod))
            {
                var next = AddClamped(last, _effectivePeriod);
                if (_notBeforeUtc is null || next > _notBeforeUtc) _notBeforeUtc = next;
            }
        }

        if (_lastScheduledStartUtc is null) _firstRunUtc = FirstRunFromNow();

        // The restarted loop waits out the job's Delay again; a calendar mustn't wait for it.
        Volatile.Write(ref _loopTicked, 0);
        lock (_periodLock)
        {
            _wokenForCalendar = false;
            WakeForCalendarIfWaiting();
        }
    }

    /// <summary>
    /// On a calendar, ends Umbraco's initial wait of the job's <c>Delay</c> (which a period change
    /// can't cut short) if it is longer than <see cref="CalendarTick"/>, e.g. infinite for a job that
    /// otherwise only runs when triggered. The tick this causes is an ordinary calendar check, not a
    /// run, and the loop then ticks every <see cref="CalendarTick"/>. Call with
    /// <see cref="_periodLock"/> held.
    /// </summary>
    private void WakeForCalendarIfWaiting()
    {
        if (_calendar is null || _wokenForCalendar || Volatile.Read(ref _loopTicked) == 1) return;
        if (_job.Delay != Timeout.InfiniteTimeSpan && _job.Delay <= CalendarTick) return;

        _wokenForCalendar = true;
        try
        {
            TriggerExecution(CalendarTick);
        }
        catch (ObjectDisposedException)
        {
            // Shutting down.
        }
    }

    private DateTime? FirstRunFromNow() => _job.Delay == Timeout.InfiniteTimeSpan ? null : AddClamped(UtcNow(), _job.Delay);

    private static DateTime AddClamped(DateTime value, TimeSpan offset)
        => offset > TimeSpan.Zero && offset >= DateTime.MaxValue - value ? DateTime.MaxValue
            : offset < TimeSpan.Zero && -offset >= value - DateTime.MinValue ? DateTime.MinValue
            : value + offset;

    private async Task WaitWhilePausedAsync(CancellationToken stoppingToken)
    {
        var refresh = _options.CurrentValue.EffectiveSettingsRefreshInterval;
        var ignoredDelay = _job.IgnoredDelay;
        var backOff = ignoredDelay == Timeout.InfiniteTimeSpan || ignoredDelay > refresh ? refresh : ignoredDelay;
        if (backOff < MinPausedBackOff) backOff = MinPausedBackOff;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _wakeCts.Token);
        try
        {
            await Task.Delay(backOff, _timeProvider, linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Shutting down, or a run was requested.
        }
    }

    /// <summary>Applies the stored interval override or calendar to the timer loop.</summary>
    /// <returns>Whether this replaced a calendar by an interval.</returns>
    private bool ApplySettings(ScheduledTaskSetting? setting)
    {
        // This server's own record (read outside the lock: it may reload from the database). Another
        // server's runs of a recurring job don't count here: each server runs it in-process.
        var recorded = setting?.Calendar is { } stored ? _settings.GetServerOccurrence(TaskId, stored.SavedUtc) : null;
        DateTime? startMarker = null;
        bool calendarRemoved;

        lock (_periodLock)
        {
            var hadCalendar = _calendar is not null;
            var previousPeriod = _effectivePeriod;

            _calendar = setting?.Calendar;
            if (_calendar is null)
            {
                _lastHandledOccurrenceUtc = null;
                _handledForSavedUtc = null;
            }
            else
            {
                // The bookkeeping belongs to the calendar it was recorded under.
                if (_handledForSavedUtc != _calendar.SavedUtc)
                {
                    _lastHandledOccurrenceUtc = null;
                    _handledForSavedUtc = _calendar.SavedUtc;

                    if (recorded is null)
                    {
                        // No record of this schedule on this server: it either started after the
                        // schedule was saved and never saw it (a new server, e.g. a new container or
                        // scale-out instance with a new machine name), whose occurrences before it
                        // started aren't missed runs; or it was already running, and counts from the
                        // save. Recorded now, so a restart of this server still catches up an
                        // occurrence missed while it was down.
                        if (_hostStartedUtc > _calendar.SavedUtc) _lastHandledOccurrenceUtc = _hostStartedUtc;
                        startMarker = _lastHandledOccurrenceUtc ?? _calendar.SavedUtc;
                    }
                }

                // Recorded on this server before a restart: don't repeat it.
                if (recorded is { } handled && (_lastHandledOccurrenceUtc is null || handled > _lastHandledOccurrenceUtc))
                    _lastHandledOccurrenceUtc = handled;
            }

            calendarRemoved = hadCalendar && _calendar is null;
            if (calendarRemoved)
            {
                // Calendar ticks don't keep the interval schedule; count the new interval from the
                // switch, so a long one (on the heartbeat) doesn't run at the next heartbeat tick.
                _lastScheduledStartUtc = UtcNow();
            }

            // A calendar replaces the interval; the job's own period still describes it when there's none.
            _periodOverride = _calendar is null ? setting?.PeriodOverride : null;
            _effectivePeriod = _periodOverride ?? _job.Period;
            SetTimerPeriod(TimerFor(_effectivePeriod));

            // A real change of the interval or schedule replaces what earlier triggers set.
            if (_effectivePeriod != previousPeriod || (_calendar is not null) != hadCalendar) ClearTriggeredSchedule(wakeLoop: true);

            WakeForCalendarIfWaiting();
        }

        if (startMarker is { } marker && setting?.Calendar is { } calendar)
        {
            try
            {
                _settings.RecordServerOccurrence(TaskId, marker, calendar.SavedUtc);
            }
            catch (Exception ex)
            {
                // Not fatal: after a restart an occurrence missed while down might not be caught up.
                _logger.LogWarning(ex, "Could not record when {TaskId} started following its calendar on this server", TaskId);
            }
        }

        return calendarRemoved;
    }

    private void OnJobPeriodChanged(object? sender, EventArgs e)
    {
        lock (_periodLock)
        {
            // The base handler has just handed the job's own new period to the timer loop; put back
            // the override or calendar tick (if any) and the cap for periods a timer can't wait.
            _effectivePeriod = _periodOverride ?? _job.Period;
            _timerPeriod = _job.Period;
            _periodReapplyPending = true;
            SetTimerPeriod(TimerFor(_effectivePeriod));

            // The base handler has also taken Umbraco's loop out of manual mode.
            ClearTriggeredSchedule();
        }
    }

    // What the loop should wait with: the calendar tick, a heartbeat for long intervals, or the interval.
    private TimeSpan TimerFor(TimeSpan effectivePeriod)
        => _calendar is not null ? CalendarTick : TimerPeriodFor(effectivePeriod);

    private ScheduledTaskCalendar? CurrentCalendar()
    {
        lock (_periodLock) return _calendar;
    }

    /// <summary>
    /// The calendar's next occurrence after the last one handled, or after the schedule was saved if
    /// none has been (so saving a schedule never triggers a run for a time that has already passed).
    /// </summary>
    private DateTime? NextCalendarOccurrence(ScheduledTaskCalendar calendar)
        => _calendarEvaluator.NextOccurrenceUtc(calendar, CalendarBaseline(calendar));

    private DateTime CalendarBaseline(ScheduledTaskCalendar calendar)
    {
        lock (_periodLock)
        {
            return _lastHandledOccurrenceUtc is { } handled && handled > calendar.SavedUtc ? handled : calendar.SavedUtc;
        }
    }

    /// <summary>
    /// Records that the calendar's occurrences up to now were dealt with, here and in the database
    /// (for this server). What is recorded is the latest occurrence served, not the time of the tick:
    /// counting from a tick inside a repeated autumn hour would make the same wall-clock time due again.
    /// </summary>
    private void MarkOccurrenceHandled(ScheduledTaskCalendar calendar, DateTime utcNow)
    {
        var served = _calendarEvaluator.LatestOccurrenceUtc(calendar, CalendarBaseline(calendar), utcNow) ?? utcNow;
        lock (_periodLock)
        {
            if (_lastHandledOccurrenceUtc is null || served > _lastHandledOccurrenceUtc) _lastHandledOccurrenceUtc = served;
            _handledForSavedUtc = calendar.SavedUtc;
        }

        try
        {
            _settings.RecordServerOccurrence(TaskId, served, calendar.SavedUtc);
        }
        catch (Exception ex)
        {
            // Not fatal: this server still knows; after a restart the occurrence might run once more.
            _logger.LogWarning(ex, "Could not record the handled calendar occurrence of {TaskId}", TaskId);
        }
    }

    private void SetTimerPeriod(TimeSpan timerPeriod)
    {
        lock (_periodLock)
        {
            if (timerPeriod == _timerPeriod) return;

            _timerPeriod = timerPeriod;
            ChangePeriod(timerPeriod);
            // The loop only notices this if it's waiting right now; SyncWithSettings re-applies it otherwise.
            _periodReapplyPending = true;
        }
    }

    private static TimeSpan TimerPeriodFor(TimeSpan period)
        => period != Timeout.InfiniteTimeSpan && period > MaxTimerPeriod ? Heartbeat : period;

    /// <summary>Whether the timer loop ticks on <see cref="Heartbeat"/> for a long interval (not on a calendar).</summary>
    private bool IsOnHeartbeat()
    {
        lock (_periodLock) return _calendar is null && _timerPeriod != _effectivePeriod;
    }

    /// <summary>
    /// Whether a heartbeat tick should count: once the interval has passed since the last
    /// scheduled run. The first tick after startup counts, as it does for Umbraco's own loop.
    /// </summary>
    private bool IsDue(DateTime utcNow)
        => _lastScheduledStartUtc is not { } lastStart || utcNow - lastStart >= EffectivePeriod - DueTolerance;

    private void OnIgnoredDelayChanged(object? sender, EventArgs e) => RotateAndCancel(ref _ignoredDelayChangeCts);

    private static void RotateAndCancel(ref CancellationTokenSource field)
    {
        var next = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref field, next);
        try
        {
            previous.Cancel();
        }
        catch (ObjectDisposedException)
        {
            next.Dispose();
        }
    }

    // Mirrors Umbraco's private back-off after an ignored tick, so a job that can't run here
    // doesn't spin when its period is shorter than IgnoredDelay.
    private async Task IgnoreAndWaitAsync(
        string message,
        EventMessages eventMessages,
        RecurringBackgroundJobExecutingNotification executingNotification,
        CancellationToken stoppingToken)
    {
        _logger.LogDebug(message);
        await _eventAggregator.PublishAsync(
            new RecurringBackgroundJobIgnoredNotification(_job, eventMessages).WithStateFrom(executingNotification),
            stoppingToken);

        var waitStart = _timeProvider.GetTimestamp();

        while (true)
        {
            var ignoredDelay = _job.IgnoredDelay;
            if (ignoredDelay != Timeout.InfiniteTimeSpan && ignoredDelay <= TimeSpan.Zero) return;

            var remaining = Remaining(ignoredDelay, _timeProvider.GetElapsedTime(waitStart));
            if (remaining == TimeSpan.Zero) return;

            var ignoredDelayChangeToken = _ignoredDelayChangeCts.Token;
            var wakeToken = _wakeCts.Token;
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, ignoredDelayChangeToken, wakeToken);

            try
            {
                await Task.Delay(remaining, _timeProvider, linkedCts.Token);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested || wakeToken.IsCancellationRequested)
            {
                // Shutting down, or a run was requested (it shouldn't wait out the back-off).
                return;
            }
            catch (OperationCanceledException) when (ignoredDelayChangeToken.IsCancellationRequested)
            {
                // IgnoredDelay changed: re-read it and recompute the remaining wait.
            }
        }
    }

    private static TimeSpan Remaining(TimeSpan period, TimeSpan elapsed)
    {
        if (period == Timeout.InfiniteTimeSpan) return Timeout.InfiniteTimeSpan;
        var remaining = period - elapsed;
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private sealed record PendingRun(
        ScheduledTaskTrigger Trigger,
        string? TriggeredBy,
        NextExecutionStrategy Strategy,
        TimeSpan? NextDelay);
}
