using NPoco;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>The <c>ScheduledTaskRuns</c> table: every server's saved runs.</summary>
internal interface IScheduledTaskRunStore
{
    /// <summary>False until the package's migration has created the table.</summary>
    bool IsAvailable { get; }

    /// <summary>Inserts new runs and updates known ones (by run id). Never clears a stop request made from another server.</summary>
    void Save(IReadOnlyCollection<ScheduledTaskRun> runs);

    /// <summary>The task's newest runs, newest first.</summary>
    IReadOnlyList<ScheduledTaskRun> GetRuns(string taskId, int limit);

    /// <summary>The task's newest finished run that ran (succeeded, failed or was canceled), or null.</summary>
    ScheduledTaskRun? GetLatest(string taskId);

    /// <summary><see cref="GetLatest"/> for every task.</summary>
    IReadOnlyDictionary<string, ScheduledTaskRun> GetLatestPerTask();

    /// <summary>The task's runs saved as in progress, on any server.</summary>
    IReadOnlyList<ScheduledTaskRun> GetRunning(string taskId);

    /// <summary>The task's runs in progress or waiting for a retry, on any server.</summary>
    IReadOnlyList<ScheduledTaskRun> GetActive(string taskId) => GetRunning(taskId);

    /// <summary>
    /// Asks the server running <paramref name="runId"/> (or waiting to retry it) to stop. False when
    /// it isn't running or waiting any more.
    /// </summary>
    bool RequestStop(Guid runId, string requestedBy);

    /// <summary>This server's runs in progress or waiting to retry that someone asked (from any server) to stop: run id → who.</summary>
    IReadOnlyDictionary<Guid, string> GetStopRequests(string server);

    /// <summary>Marks this server's runs in progress as still alive (see <see cref="CloseAbandoned"/>).</summary>
    void Heartbeat(string server, DateTime utcNow) { }

    /// <summary>
    /// Closes runs left in progress (or waiting to retry) by a server that stopped saving them before
    /// <paramref name="staleBeforeUtc"/>: e.g. a container replaced by a deploy, which never starts again
    /// under its name to close them itself.
    /// </summary>
    int CloseAbandoned(DateTime staleBeforeUtc) => 0;

    /// <summary>Clears a failed run's pending retry.</summary>
    bool ClearRetry(Guid runId);

    void Delete(string taskId);

    /// <summary>Keeps the task's newest <paramref name="keep"/> runs.</summary>
    void Prune(string taskId, int keep);

    /// <summary>Deletes finished runs that started before <paramref name="cutoffUtc"/>.</summary>
    void PruneOlderThan(DateTime cutoffUtc);

    /// <summary>
    /// Marks this server's runs that were still in progress when it stopped as canceled (the site
    /// restarted mid-run), so they don't show as running forever.
    /// </summary>
    int MarkInterrupted(string server, DateTime startedBeforeUtc);
}

internal sealed class ScheduledTaskRunStore : IScheduledTaskRunStore
{
    private const int ChunkSize = 500;
    private const string Running = nameof(ScheduledTaskRunOutcome.Running);
    private const string Skipped = nameof(ScheduledTaskRunOutcome.Skipped);

    // Every column a save writes; stopRequestedBy only when the run has one (see Save).
    private static readonly string[] SavedColumns =
    [
        "runTrigger", "triggeredBy", "server", "startedUtc", "finishedUtc", "outcome", "message", "details",
        "occurrences", "lastOccurrenceUtc", "progress", "progressMessage", "attempt", "maxAttempts", "retryAtUtc", "log",
    ];

    private readonly IScopeProvider _scopeProvider;
    private readonly ScheduledTasksSchemaState _schema;

    public ScheduledTaskRunStore(IScopeProvider scopeProvider, ScheduledTasksSchemaState schema)
    {
        _scopeProvider = scopeProvider;
        _schema = schema;
    }

    public bool IsAvailable => _schema.IsReady;

    public void Save(IReadOnlyCollection<ScheduledTaskRun> runs)
    {
        if (runs.Count == 0) return;

        using var scope = _scopeProvider.CreateScope();
        var ids = new Dictionary<Guid, int>();
        foreach (var chunk in runs.Select(r => r.Id).Chunk(ChunkSize))
        {
            var sql = scope.SqlContext.Sql()
                .Select<ScheduledTaskRunEntity>(x => x.Id, x => x.RunId)
                .From<ScheduledTaskRunEntity>()
                .WhereIn<ScheduledTaskRunEntity>(x => x.RunId, chunk);
            foreach (var row in scope.Database.Fetch<ScheduledTaskRunEntity>(sql)) ids[row.RunId] = row.Id;
        }

        foreach (var run in runs)
        {
            var entity = ToEntity(run);
            if (ids.TryGetValue(run.Id, out var id))
            {
                entity.Id = id;
                scope.Database.Update(entity, run.StopRequestedBy is null ? SavedColumns : [.. SavedColumns, ScheduledTaskRunEntity.StopRequestedByColumn]);
            }
            else
            {
                scope.Database.Insert(entity);
            }
        }

        scope.Complete();
    }

    public IReadOnlyList<ScheduledTaskRun> GetRuns(string taskId, int limit)
    {
        using var scope = _scopeProvider.CreateScope(autoComplete: true);
        var sql = scope.SqlContext.Sql()
            .Select<ScheduledTaskRunEntity>()
            .From<ScheduledTaskRunEntity>()
            .Where<ScheduledTaskRunEntity>(x => x.TaskId == taskId)
            .OrderByDescending<ScheduledTaskRunEntity>(x => x.StartedUtc)
            .SelectTop(Math.Max(1, limit));
        return scope.Database.Fetch<ScheduledTaskRunEntity>(sql).Select(ToRun).ToList();
    }

    public ScheduledTaskRun? GetLatest(string taskId)
    {
        using var scope = _scopeProvider.CreateScope(autoComplete: true);
        var sql = LightSelect(scope.SqlContext)
            .From<ScheduledTaskRunEntity>()
            .Where<ScheduledTaskRunEntity>(x => x.TaskId == taskId && x.Outcome != Running && x.Outcome != Skipped)
            .OrderByDescending<ScheduledTaskRunEntity>(x => x.StartedUtc)
            .SelectTop(1);
        return scope.Database.Fetch<ScheduledTaskRunEntity>(sql).Select(ToRun).FirstOrDefault();
    }

    public IReadOnlyDictionary<string, ScheduledTaskRun> GetLatestPerTask()
    {
        using var scope = _scopeProvider.CreateScope(autoComplete: true);
        var syntax = scope.SqlContext.SqlSyntax;
        var table = syntax.GetQuotedTableName(ScheduledTaskRunEntity.TableName);
        string Column(string name) => syntax.GetQuotedColumnName(name);
        var finished = $"{{0}}.{Column("outcome")} <> @0 AND {{0}}.{Column("outcome")} <> @1";

        // Each task's newest finished run: served by the (taskId, startedUtc) index.
        var sql = new Sql<ISqlContext>(
            scope.SqlContext,
            $"SELECT {string.Join(", ", LightColumnNames.Select(c => "r." + Column(c)))} FROM {table} r WHERE {string.Format(finished, "r")} AND r.{Column("startedUtc")} = " +
            $"(SELECT MAX(r2.{Column("startedUtc")}) FROM {table} r2 WHERE r2.{Column("taskId")} = r.{Column("taskId")} AND {string.Format(finished, "r2")})",
            Running,
            Skipped);

        return scope.Database.Fetch<ScheduledTaskRunEntity>(sql)
            .GroupBy(r => r.TaskId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => ToRun(g.OrderByDescending(r => r.Id).First()), StringComparer.Ordinal);
    }

    public IReadOnlyList<ScheduledTaskRun> GetActive(string taskId)
    {
        using var scope = _scopeProvider.CreateScope(autoComplete: true);
        var sql = LightSelect(scope.SqlContext)
            .From<ScheduledTaskRunEntity>()
            .Where<ScheduledTaskRunEntity>(x => x.TaskId == taskId && (x.Outcome == Running || x.RetryAtUtc != null));
        return scope.Database.Fetch<ScheduledTaskRunEntity>(sql).Select(ToRun).ToList();
    }

    public IReadOnlyList<ScheduledTaskRun> GetRunning(string taskId)
    {
        using var scope = _scopeProvider.CreateScope(autoComplete: true);
        var sql = scope.SqlContext.Sql()
            .Select<ScheduledTaskRunEntity>()
            .From<ScheduledTaskRunEntity>()
            .Where<ScheduledTaskRunEntity>(x => x.TaskId == taskId && x.Outcome == Running);
        return scope.Database.Fetch<ScheduledTaskRunEntity>(sql).Select(ToRun).ToList();
    }

    public bool RequestStop(Guid runId, string requestedBy)
    {
        using var scope = _scopeProvider.CreateScope();
        var by = Truncate(requestedBy, 255);
        var sql = scope.SqlContext.Sql()
            .Update<ScheduledTaskRunEntity>(u => u.Set(x => x.StopRequestedBy, by))
            .Where<ScheduledTaskRunEntity>(x => x.RunId == runId && (x.Outcome == Running || x.RetryAtUtc != null));
        var updated = scope.Database.Execute(sql) > 0;
        scope.Complete();
        return updated;
    }

    public IReadOnlyDictionary<Guid, string> GetStopRequests(string server)
    {
        var name = Truncate(server, 255)!;
        using var scope = _scopeProvider.CreateScope(autoComplete: true);
        var sql = scope.SqlContext.Sql()
            .Select<ScheduledTaskRunEntity>(x => x.RunId, x => x.StopRequestedBy)
            .From<ScheduledTaskRunEntity>()
            .Where<ScheduledTaskRunEntity>(x => x.Server == name && (x.Outcome == Running || x.RetryAtUtc != null))
            .WhereNotNull<ScheduledTaskRunEntity>(x => x.StopRequestedBy);
        return scope.Database.Fetch<ScheduledTaskRunEntity>(sql).ToDictionary(r => r.RunId, r => r.StopRequestedBy!);
    }

    public bool ClearRetry(Guid runId)
    {
        using var scope = _scopeProvider.CreateScope();
        var sql = scope.SqlContext.Sql()
            .Update<ScheduledTaskRunEntity>(u => u.Set(x => x.RetryAtUtc, null))
            .Where<ScheduledTaskRunEntity>(x => x.RunId == runId);
        var updated = scope.Database.Execute(sql) > 0;
        scope.Complete();
        return updated;
    }

    public void Delete(string taskId)
    {
        using var scope = _scopeProvider.CreateScope();
        // A run in progress stays: it's still to finish (and the dashboard shows it).
        scope.Database.Execute(scope.SqlContext.Sql().Delete<ScheduledTaskRunEntity>(x => x.TaskId == taskId && x.Outcome != Running));
        scope.Complete();
    }

    public void Prune(string taskId, int keep)
    {
        using var scope = _scopeProvider.CreateScope();
        var sql = scope.SqlContext.Sql()
            .Select<ScheduledTaskRunEntity>(x => x.Id, x => x.Outcome)
            .From<ScheduledTaskRunEntity>()
            .Where<ScheduledTaskRunEntity>(x => x.TaskId == taskId)
            .OrderByDescending<ScheduledTaskRunEntity>(x => x.StartedUtc);
        var surplus = scope.Database.Fetch<ScheduledTaskRunEntity>(sql)
            .Skip(Math.Max(1, keep))
            .Where(r => r.Outcome != Running)
            .Select(r => r.Id)
            .ToList();

        foreach (var chunk in surplus.Chunk(ChunkSize))
            scope.Database.Execute(scope.SqlContext.Sql().Delete<ScheduledTaskRunEntity>().WhereIn<ScheduledTaskRunEntity>(x => x.Id, chunk));
        scope.Complete();
    }

    public void PruneOlderThan(DateTime cutoffUtc)
    {
        using var scope = _scopeProvider.CreateScope();
        scope.Database.Execute(scope.SqlContext.Sql().Delete<ScheduledTaskRunEntity>(x => x.StartedUtc < cutoffUtc && x.Outcome != Running));
        scope.Complete();
    }

    public int MarkInterrupted(string server, DateTime startedBeforeUtc)
    {
        var name = Truncate(server, 255)!;
        using var scope = _scopeProvider.CreateScope();
        var sql = scope.SqlContext.Sql()
            .Select<ScheduledTaskRunEntity>()
            .From<ScheduledTaskRunEntity>()
            .Where<ScheduledTaskRunEntity>(x => x.Server == name && x.Outcome == Running && x.StartedUtc < startedBeforeUtc);
        var interrupted = scope.Database.Fetch<ScheduledTaskRunEntity>(sql);
        foreach (var run in interrupted)
        {
            run.Outcome = nameof(ScheduledTaskRunOutcome.Canceled);
            run.FinishedUtc = run.LastOccurrenceUtc;
            run.Message = "Interrupted: the site stopped while the task was running.";
            scope.Database.Update(run, ["outcome", "finishedUtc", "message"]);
        }

        // A recurring task waits for its retry in-process: the wait ended with the process.
        const string recurring = ScheduledTaskIdentity.RecurringPrefix;
        scope.Database.Execute(scope.SqlContext.Sql()
            .Update<ScheduledTaskRunEntity>(u => u.Set(x => x.RetryAtUtc, null))
            .Where<ScheduledTaskRunEntity>(x => x.Server == name && x.RetryAtUtc != null && x.StartedUtc < startedBeforeUtc && x.TaskId.StartsWith(recurring)));

        scope.Complete();
        return interrupted.Count;
    }

    public void Heartbeat(string server, DateTime utcNow)
    {
        var name = Truncate(server, 255)!;
        using var scope = _scopeProvider.CreateScope();
        // lastOccurrenceUtc means nothing else for a run in progress (it's only folded skips' latest).
        scope.Database.Execute(scope.SqlContext.Sql()
            .Update<ScheduledTaskRunEntity>(u => u.Set(x => x.LastOccurrenceUtc, utcNow))
            .Where<ScheduledTaskRunEntity>(x => x.Server == name && x.Outcome == Running));
        scope.Complete();
    }

    public int CloseAbandoned(DateTime staleBeforeUtc)
    {
        using var scope = _scopeProvider.CreateScope();
        var sql = LightSelect(scope.SqlContext)
            .From<ScheduledTaskRunEntity>()
            .Where<ScheduledTaskRunEntity>(x => (x.Outcome == Running && x.LastOccurrenceUtc < staleBeforeUtc)
                || (x.RetryAtUtc != null && x.RetryAtUtc < staleBeforeUtc));
        var abandoned = scope.Database.Fetch<ScheduledTaskRunEntity>(sql);
        foreach (var run in abandoned)
        {
            if (run.Outcome == Running)
            {
                run.Outcome = nameof(ScheduledTaskRunOutcome.Canceled);
                run.FinishedUtc = run.LastOccurrenceUtc;
                run.Message = $"Interrupted: {run.Server} stopped saving the run.";
            }

            run.RetryAtUtc = null;
            scope.Database.Update(run, ["outcome", "finishedUtc", "message", "retryAtUtc"]);
        }

        scope.Complete();
        return abandoned.Count;
    }

    // Everything but the log and the stack trace: for the queries that run on every poll.
    private static readonly string[] LightColumnNames =
    [
        "id", "runId", "taskId", "runTrigger", "triggeredBy", "server", "startedUtc", "finishedUtc", "outcome", "message",
        "occurrences", "lastOccurrenceUtc", "progress", "progressMessage", "attempt", "maxAttempts", "retryAtUtc", ScheduledTaskRunEntity.StopRequestedByColumn,
    ];

    private static Sql<ISqlContext> LightSelect(ISqlContext context)
        => context.Sql().Select(LightColumnNames.Select(c => (object)context.SqlSyntax.GetQuotedColumnName(c)).ToArray());

    private static ScheduledTaskRunEntity ToEntity(ScheduledTaskRun run) => new()
    {
        RunId = run.Id,
        TaskId = run.TaskId,
        Trigger = run.Trigger.ToString(),
        TriggeredBy = Truncate(run.TriggeredBy, 255),
        Server = Truncate(run.Server, 255)!,
        StartedUtc = run.StartedUtc,
        FinishedUtc = run.FinishedUtc,
        Outcome = run.Outcome.ToString(),
        Message = Truncate(run.Message, 2000),
        Details = run.Details,
        Occurrences = run.Occurrences,
        LastOccurrenceUtc = run.LastOccurrenceUtc,
        Progress = run.Progress,
        ProgressMessage = Truncate(run.ProgressMessage, 500),
        Attempt = run.Attempt,
        MaxAttempts = run.MaxAttempts,
        RetryAtUtc = run.RetryAtUtc,
        StopRequestedBy = Truncate(run.StopRequestedBy, 255),
        Log = run.Log,
    };

    internal static ScheduledTaskRun ToRun(ScheduledTaskRunEntity entity) => new(
        entity.RunId,
        entity.TaskId,
        Enum.TryParse<ScheduledTaskTrigger>(entity.Trigger, out var trigger) ? trigger : ScheduledTaskTrigger.Schedule,
        entity.TriggeredBy,
        entity.Server,
        Utc(entity.StartedUtc),
        entity.FinishedUtc is { } finished ? Utc(finished) : null,
        Enum.TryParse<ScheduledTaskRunOutcome>(entity.Outcome, out var outcome) ? outcome : ScheduledTaskRunOutcome.Canceled,
        entity.Message,
        entity.Details,
        Math.Max(1, entity.Occurrences),
        Utc(entity.LastOccurrenceUtc),
        entity.Progress,
        entity.ProgressMessage)
    {
        Attempt = Math.Max(1, entity.Attempt),
        MaxAttempts = Math.Max(1, entity.MaxAttempts),
        RetryAtUtc = entity.RetryAtUtc is { } retryAt ? Utc(retryAt) : null,
        StopRequestedBy = entity.StopRequestedBy,
        Log = entity.Log,
    };

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string? Truncate(string? value, int maxLength)
        => value is null || value.Length <= maxLength ? value : value[..maxLength];
}
