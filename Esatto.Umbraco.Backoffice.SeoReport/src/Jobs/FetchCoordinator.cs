using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Esatto.Umbraco.Backoffice.SeoReport.Jobs;

/// <summary>What <see cref="FetchCoordinator.TryStart"/> did.</summary>
public enum FetchStartResult
{
    /// <summary>A backfill is running now (it may find that nothing is missing and end at once).</summary>
    Started,

    /// <summary>A backfill is already running, in this process or on another instance. Nothing was changed.</summary>
    AlreadyRunning,

    /// <summary>The package is not ready to fetch: no usable key (a key that is saved but cannot be read counts as well), or a property is missing.</summary>
    NotConfigured,
}

/// <summary>
/// How far the latest backfill of this process is. Only counts and codes: <paramref name="ErrorCode"/> is one of <c>ErrorCodes.All</c>,
/// never a message (the client has the words), and the key is nowhere in it.
/// </summary>
/// <param name="Running">True from the start of the run to its end.</param>
/// <param name="Done">Months finished: archived, or skipped because they can no longer be fetched. A month that failed is not done.</param>
/// <param name="Total">The months that needed a fetch when the run started.</param>
/// <param name="CurrentPeriod">The month being fetched (<c>YYYY-MM</c>), or null.</param>
/// <param name="ErrorCode">Why the run ended badly: the code that stopped it, or the last failure when every month it tried failed. Null while running and after a run that went well.</param>
public sealed record FetchProgress(bool Running, int Done, int Total, string? CurrentPeriod, string? ErrorCode);

/// <summary>
/// Fills the archive in the background: every month of the last thirteen (see <see cref="WantedPeriods"/>) that needs a fetch,
/// newest first, one month at a time. Singleton, like the report service whose gate keeps this process to one fetch at a time: the
/// dashboard polls <see cref="Progress"/> while a run goes on. Opening the dashboard never starts one; saving settings that work,
/// Fetch now (the controller) and the scheduled job (<see cref="SeoReportRefreshJob"/>) do.
///
/// ONE BACKFILL AT A TIME, in two layers. In this process a flag is claimed with one atomic exchange, so two callers at the same
/// moment cannot both start. Across instances the database lock <c>fetch</c> is held for the whole run (30 minutes, renewed before
/// every month, released when the run ends however it ends); a lock held by someone else is <see cref="FetchStartResult.AlreadyRunning"/>,
/// the same answer as a run in this process, so the person is told the same thing.
///
/// THE START IS DECIDED BEFORE IT RETURNS, which is why the lock is taken in <see cref="TryStartAsync"/> and not in the background:
/// a caller must be able to tell a person "already running" or "not set up" instead of "started" and leave the refusal to show up
/// later in the progress. <see cref="TryStartAsync"/> is that start, and does not block. <see cref="TryStart"/> is the same call for
/// a caller that cannot await: it runs <see cref="TryStartAsync"/> on the thread pool and waits for it, so the work never runs on the
/// caller's synchronization context and cannot deadlock with it (ASP.NET Core has none anyway). The wait is for the start only, never
/// for the backfill: at most the setup state (cached for a minute) and one lock write, bounded by the database's own command timeout.
///
/// A RUN CANNOT BE TOLD TO STOP except by the application stopping (the host's <c>ApplicationStopping</c> token), which cancels the
/// month in flight, ends the run without an error code, lets go of the lock and leaves <see cref="Progress"/> not running. Nothing a
/// run does can escape its background task.
/// </summary>
public sealed class FetchCoordinator(
    ISeoReportService reports,
    SettingsService settings,
    ISeoReportStore store,
    TimeProvider time,
    ILogger<FetchCoordinator> logger,
    IHostApplicationLifetime? lifetime = null) : IFetchCoordinator
{
    /// <summary>The database lock that keeps instances from fetching at the same time.</summary>
    public const string LockName = "fetch";

    // The name of RefreshAsync's parameter, which the service's range check puts on its ArgumentOutOfRangeException.
    private const string PeriodParameter = "period";

    // The archive depth: the report month and the twelve before it (GscAgent.WindowMonths), within Search Console's sixteen.
    private const int ArchiveMonths = GscAgent.WindowMonths + 1;

    // Longer than any one month takes by far, and renewed before each month, so a crashed instance's lock lapses within half an hour.
    private static readonly TimeSpan LockLife = TimeSpan.FromMinutes(30);

    // A failure with one of these codes would repeat for every other month (the key, the access, the property, the quota), so the run
    // ends and the code is the answer. Any other failure (an error without a code, a hiccup) costs only its own month.
    private static readonly HashSet<string> StoppingCodes = new(StringComparer.Ordinal)
    {
        ErrorCodes.Quota,
        ErrorCodes.NoAccess,
        ErrorCodes.ApiDisabled,
        ErrorCodes.PropertyNotFound,
        ErrorCodes.KeyUnreadable,
        ErrorCodes.KeyInvalid,
        ErrorCodes.NotConfigured,
    };

    private readonly CancellationToken _stopping = lifetime?.ApplicationStopping ?? CancellationToken.None;

    // Who holds the database lock from this process: one id per process, so two instances never share it.
    private readonly string _owner = Guid.NewGuid().ToString("N");

    private int _claimed;
    private FetchProgress? _progress;
    private Task? _run;

    /// <summary>
    /// The latest run of this process, running or finished; null before the first. An immutable record that is replaced as a whole,
    /// so a poll never sees half an update.
    /// </summary>
    public FetchProgress? Progress => Volatile.Read(ref _progress);

    /// <summary>The task of the latest run, which never faults. For tests, to wait for the end of a run.</summary>
    internal Task? CurrentRun => Volatile.Read(ref _run);

    /// <summary>
    /// The months a backfill wants: <see cref="ArchiveMonths"/> ending at <paramref name="expectedLatest"/>, newest first, without
    /// the ones before the first month Search Console still has on <paramref name="today"/>.
    /// </summary>
    public static IReadOnlyList<Period> WantedPeriods(Period expectedLatest, DateOnly today)
    {
        var earliest = GscAgent.EarliestAvailableMonth(today);
        var wanted = new List<Period>(ArchiveMonths);
        for (var back = 0; back < ArchiveMonths; back++)
        {
            var period = expectedLatest.AddMonths(-back);
            if (period < earliest) break;

            wanted.Add(period);
        }

        return wanted;
    }

    /// <summary>
    /// The wanted months that need a fetch, newest first: what the service says, month by month (<see cref="ISeoReportService.NeedsFetchAsync"/>),
    /// and not a difference of lists. The bounds are read once, here, from the service's expected latest month and the clock.
    /// </summary>
    internal static async Task<List<Period>> DueAsync(ISeoReportService reports, TimeProvider time, CancellationToken ct = default)
    {
        var today = TodayOf(time);
        var due = new List<Period>(ArchiveMonths);
        foreach (var period in WantedPeriods(reports.ExpectedLatestPeriod, today))
        {
            if (await reports.NeedsFetchAsync(period, ct).ConfigureAwait(false)) due.Add(period);
        }

        return due;
    }

    /// <summary>
    /// <see cref="TryStartAsync"/> for a caller that cannot await (see the class remarks for why this cannot deadlock). Returns as
    /// soon as the start is decided, not when the backfill ends.
    /// </summary>
    /// <param name="startedBy"><c>"scheduled"</c> for the job; anything else is a person (their name, which is neither stored nor logged).</param>
    public FetchStartResult TryStart(string startedBy)
    {
        ArgumentNullException.ThrowIfNull(startedBy);

        return Task.Run(() => TryStartAsync(startedBy)).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Starts a backfill in the background unless one is running, and says what it did. <see cref="FetchStartResult.NotConfigured"/>
    /// when the package is not <see cref="SetupState.Ready"/> (<see cref="SetupState.KeyUnreadable"/> too: a key that cannot be read
    /// cannot fetch). <see cref="FetchStartResult.AlreadyRunning"/> when this process is running one or another instance holds the
    /// lock. A started run shows in <see cref="Progress"/> before this returns. A store that fails while the start is decided throws
    /// (nothing is left claimed).
    /// </summary>
    /// <param name="startedBy"><c>"scheduled"</c> for the job; anything else is a person (their name, which is neither stored nor logged).</param>
    public async Task<FetchStartResult> TryStartAsync(string startedBy)
    {
        ArgumentNullException.ThrowIfNull(startedBy);

        if (await settings.GetSetupStateAsync().ConfigureAwait(false) != SetupState.Ready) return FetchStartResult.NotConfigured;

        // The claim is atomic and taken before the next await: of two callers at the same moment exactly one passes.
        if (Interlocked.CompareExchange(ref _claimed, 1, 0) != 0) return FetchStartResult.AlreadyRunning;

        var started = false;
        try
        {
            if (!await store.TryAcquireLockAsync(LockName, _owner, LockLife).ConfigureAwait(false)) return FetchStartResult.AlreadyRunning;

            // Seen by the first poll after this returns, before the background task has done anything.
            Volatile.Write(ref _progress, new FetchProgress(Running: true, Done: 0, Total: 0, CurrentPeriod: null, ErrorCode: null));

            var kind = startedBy == FetchState.Scheduled ? FetchState.Scheduled : FetchState.Manual;
            Volatile.Write(ref _run, StartRun(kind));
            started = true;
            return FetchStartResult.Started;
        }
        finally
        {
            // Refused, or the store failed: nothing is running, so nothing stays claimed. (A started run clears it when it ends.)
            if (!started) Volatile.Write(ref _claimed, 0);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The run
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Starts the run on the thread pool WITHOUT the caller's execution context. A run lasts up to half an hour and outlives the request
    /// (or job wake-up) that started it; <c>Task.Run</c> would copy the caller's <c>AsyncLocal</c> state into it, which includes
    /// Umbraco's ambient scope (the store opens its scopes from it) and the request's logging scope, so the run would nest its own
    /// scopes under a parent that is disposed long before it ends. Nothing is awaited while the flow is suppressed, so the control is
    /// disposed on the thread that created it, and the caller's own context is untouched.
    /// </summary>
    private Task StartRun(string kind)
    {
        if (ExecutionContext.IsFlowSuppressed()) return Task.Run(() => RunAsync(kind));

        using (ExecutionContext.SuppressFlow())
        {
            return Task.Run(() => RunAsync(kind));
        }
    }

    /// <summary>The counts of a run and how it ended; touched only by the run's own task.</summary>
    private sealed class Run
    {
        public int Total { get; set; }

        public int Done { get; set; }

        public int Succeeded { get; set; }

        public int Failed { get; set; }

        public string? LastFailureCode { get; set; }

        /// <summary>The code the run ended on when it did not get to the end of its months.</summary>
        public string? StopCode { get; private set; }

        public bool OutOfRangeLogged { get; set; }

        /// <summary>Whether it ended badly (a failed month or a stop), whatever else it archived.</summary>
        public bool HadFailure => Failed > 0 || StopCode is not null;

        /// <summary>Whether it did anything worth recording: a run with nothing to fetch, or one that was cancelled before it began, says nothing.</summary>
        public bool WorthRecording => Succeeded > 0 || HadFailure;

        /// <summary>The code of a run that ended badly: why it stopped, or the last failure when EVERY month it tried failed.</summary>
        public string? ErrorCode => StopCode ?? (Succeeded == 0 && Failed > 0 ? LastFailureCode : null);

        public void Stop(string code) => StopCode = code;
    }

    private async Task RunAsync(string kind)
    {
        var run = new Run();
        try
        {
            await BackfillAsync(run).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            logger.LogInformation("The report backfill was stopped because the application is shutting down.");
        }
        catch (Exception ex)
        {
            // Nothing escapes a background task, and a surprise is not something the person can fix: a code on the screen, the detail here.
            run.Stop(ErrorCodes.Unknown);
            logger.LogWarning(ex, "The report backfill stopped on an unexpected {ExceptionType}.", ex.GetType().Name);
        }
        finally
        {
            await FinishAsync(kind, run).ConfigureAwait(false);
        }
    }

    private async Task BackfillAsync(Run run)
    {
        var ct = _stopping;

        var due = await DueAsync(reports, time, ct).ConfigureAwait(false);
        run.Total = due.Count;
        Publish(run, current: null);

        foreach (var period in due)
        {
            ct.ThrowIfCancellationRequested();

            if (await CheckBeforeMonthAsync().ConfigureAwait(false) is { } reason)
            {
                run.Stop(reason);
                return;
            }

            Publish(run, period);
            try
            {
                await reports.RefreshAsync(period, ct).ConfigureAwait(false);
                run.Succeeded++;
                run.Done++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (ArgumentOutOfRangeException ex) when (IsRefusedAsOutOfRange(ex, period))
            {
                // The service refuses a month outside what can be archived now. The bounds were worked out once, at the start, and the
                // clock moves on (a month can fall out of Search Console's window while the run goes): the month is no longer
                // fetchable, which is not a failure. Any other ArgumentOutOfRangeException falls through to the failure below.
                run.Done++;
                if (!run.OutOfRangeLogged)
                {
                    run.OutOfRangeLogged = true;
                    logger.LogInformation("The report for {Period} can no longer be fetched (it is outside the months that can be archived now) and is skipped.", period);
                }
            }
            catch (Exception ex)
            {
                var code = CodeOf(period, ex);
                run.Failed++;
                run.LastFailureCode = code;
                if (StoppingCodes.Contains(code))
                {
                    run.Stop(code);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="ex"/> is the service's refusal of a month that is outside the range NOW: it names the parameter
    /// <c>period</c> (what <see cref="ISeoReportService.RefreshAsync"/> checks first) AND the month is really later than the
    /// service's expected latest month or earlier than the first month Search Console still has, both read at this moment. Both are
    /// needed. An <see cref="ArgumentOutOfRangeException"/> from anywhere else in a fetch (an index or a date deep in the code that
    /// builds the report) says nothing about the month's range; forgiven as "not fetchable" it would leave the month missing with no
    /// failure on record, so the scheduled job would start a full fetch of it at every wake-up with nothing to hold it back.
    /// </summary>
    private bool IsRefusedAsOutOfRange(ArgumentOutOfRangeException ex, Period period)
        => ex.ParamName == PeriodParameter
           && (period > reports.ExpectedLatestPeriod || period < GscAgent.EarliestAvailableMonth(TodayOf(time)));

    private static DateOnly TodayOf(TimeProvider time) => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);

    /// <summary>
    /// Before each month: renews the lock (a run that went over its lock's life, and found it taken by another instance, must not carry
    /// on beside it) and checks that the package is still set up (the key can go while the run is in progress). The code to stop with, or null.
    /// </summary>
    private async Task<string?> CheckBeforeMonthAsync()
    {
        if (!await store.TryAcquireLockAsync(LockName, _owner, LockLife).ConfigureAwait(false))
        {
            logger.LogWarning("The fetch lock is now held by another instance (it lapsed during this run): this run stops and leaves it alone.");
            return ErrorCodes.Unknown;
        }

        var code = await settings.GetSetupStateAsync().ConfigureAwait(false) switch
        {
            SetupState.Ready => null,
            SetupState.KeyUnreadable => ErrorCodes.KeyUnreadable,
            _ => ErrorCodes.NotConfigured,
        };
        if (code is not null) logger.LogInformation("The report backfill stopped: the package is no longer ready to fetch ({Code}).", code);

        return code;
    }

    /// <summary>
    /// The code of a failed month. A failure that no code names is logged as a Warning with what is safe to say (its type and HTTP
    /// status, and the exception, which Google's and HTTP's carry no key in); a coded one is expected and only its code is logged.
    /// </summary>
    private string CodeOf(Period period, Exception ex)
    {
        var code = GoogleErrorMapper.Map(ex).Code;
        if (code == ErrorCodes.Unknown)
        {
            logger.LogWarning(ex, "Fetching the report for {Period} failed with {ExceptionType} (HTTP status {HttpStatus}) and no code names it.", period, ex.GetType().Name, GoogleErrorMapper.HttpStatusOf(ex));
        }
        else
        {
            logger.LogInformation("Fetching the report for {Period} failed: {Code}.", period, code);
        }

        return code;
    }

    private void Publish(Run run, Period? current, bool running = true)
        => Volatile.Write(ref _progress, new FetchProgress(running, run.Done, run.Total, current?.ToString(), running ? null : run.ErrorCode));

    /// <summary>
    /// Ends the run on every path: records what it did while it still holds the lock, lets the lock go, shows the final progress, and
    /// only then frees this process for the next start. Each step is allowed to fail on its own (a store that does not answer, a
    /// host that is going away) without stopping the next, and none can throw.
    /// </summary>
    private async Task FinishAsync(string kind, Run run)
    {
        try
        {
            await RecordAsync(kind, run).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "What the report backfill did could not be recorded ({ExceptionType}).", ex.GetType().Name);
        }

        try
        {
            await store.ReleaseLockAsync(LockName, _owner).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The fetch lock could not be released ({ExceptionType}); it lapses by itself.", ex.GetType().Name);
        }

        Publish(run, current: null, running: false);
        Volatile.Write(ref _claimed, 0);
    }

    private async Task RecordAsync(string kind, Run run)
    {
        if (!run.WorthRecording) return;

        var now = time.GetUtcNow();
        var before = FetchState.Parse(await store.GetDocumentAsync(DocumentKeys.FetchState).ConfigureAwait(false));
        var after = before with
        {
            LastAttemptUtc = now,
            LastSuccessUtc = run.Succeeded > 0 ? now : before.LastSuccessUtc,
            LastFailureUtc = run.HadFailure ? now : before.LastFailureUtc,
            LastErrorCode = run.HadFailure ? run.StopCode ?? run.LastFailureCode : before.LastErrorCode,
            StartedBy = kind,
        };

        await store.SaveDocumentAsync(DocumentKeys.FetchState, after.ToJson()).ConfigureAwait(false);
    }
}
