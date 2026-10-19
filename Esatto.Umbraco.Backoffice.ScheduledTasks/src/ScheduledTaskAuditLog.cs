using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>What was done to a task, as recorded in its change log.</summary>
public static class ScheduledTaskAuditActions
{
    public const string Paused = "paused";
    public const string Resumed = "resumed";
    public const string IntervalChanged = "intervalChanged";
    public const string ScheduleChanged = "scheduleChanged";
    public const string ScheduleReset = "scheduleReset";
    public const string PropertiesChanged = "propertiesChanged";
    public const string PropertiesReset = "propertiesReset";
    public const string RunPolicyChanged = "runPolicyChanged";
    public const string RunStarted = "runStarted";
    public const string StopRequested = "stopRequested";
    public const string RetryCanceled = "retryCanceled";
    public const string HistoryCleared = "historyCleared";
    public const string RunReset = "runReset";
    public const string Imported = "imported";
}

/// <summary>One entry in a task's change log.</summary>
/// <param name="Action">One of <see cref="ScheduledTaskAuditActions"/>.</param>
/// <param name="Details">What changed, e.g. "Every 2 hours → Every Monday at 07:00 (Europe/Stockholm)".</param>
public sealed record ScheduledTaskAuditEntry(
    string TaskId,
    DateTime Utc,
    string? PerformedBy,
    string Action,
    string? Details,
    string? Server);

/// <summary>
/// Who changed what on each task, from the dashboard (or an import): pauses, schedules, properties,
/// run policies, runs started and stopped. Kept in the <c>ScheduledTaskAudit</c> table for
/// <see cref="ScheduledTasksOptions.AuditRetention"/>, and written to the log too.
/// </summary>
public interface IScheduledTaskAuditLog
{
    void Write(string taskId, string action, string? performedBy, string? details = null);

    /// <summary>The task's newest entries, newest first.</summary>
    IReadOnlyList<ScheduledTaskAuditEntry> Get(string taskId, int take = 100);
}

internal sealed class ScheduledTaskAuditLog : IScheduledTaskAuditLog
{
    private readonly IScopeProvider _scopeProvider;
    private readonly ScheduledTasksSchemaState _schema;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ScheduledTaskAuditLog> _logger;
    private readonly string _server = Environment.MachineName;

    public ScheduledTaskAuditLog(
        IScopeProvider scopeProvider,
        ScheduledTasksSchemaState schema,
        TimeProvider timeProvider,
        ILogger<ScheduledTaskAuditLog> logger)
    {
        _scopeProvider = scopeProvider;
        _schema = schema;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public void Write(string taskId, string action, string? performedBy, string? details = null)
    {
        _logger.LogInformation(
            "Scheduled task {TaskId}: {Action} by {PerformedBy}{Details}",
            taskId, action, performedBy ?? "(unknown)", string.IsNullOrEmpty(details) ? string.Empty : $" ({details})");

        if (!_schema.IsReady) return;
        try
        {
            using var scope = _scopeProvider.CreateScope();
            scope.Database.Insert(new ScheduledTaskAuditEntity
            {
                TaskId = taskId,
                Utc = _timeProvider.GetUtcNow().UtcDateTime,
                PerformedBy = Truncate(performedBy, 255),
                Action = Truncate(action, 50)!,
                Details = Truncate(details, 4000),
                Server = Truncate(_server, 255),
            });
            scope.Complete();
        }
        catch (Exception ex)
        {
            // The change itself is done; losing its log entry mustn't undo or fail it.
            _logger.LogWarning(ex, "Could not write the change log entry for scheduled task {TaskId}", taskId);
        }
    }

    public IReadOnlyList<ScheduledTaskAuditEntry> Get(string taskId, int take = 100)
    {
        if (!_schema.IsReady) return [];

        using var scope = _scopeProvider.CreateScope(autoComplete: true);
        var sql = scope.SqlContext.Sql()
            .Select<ScheduledTaskAuditEntity>()
            .From<ScheduledTaskAuditEntity>()
            .Where<ScheduledTaskAuditEntity>(x => x.TaskId == taskId)
            .OrderByDescending<ScheduledTaskAuditEntity>(x => x.Utc, x => x.Id)
            .SelectTop(Math.Clamp(take, 1, 1000));
        return scope.Database.Fetch<ScheduledTaskAuditEntity>(sql)
            .Select(e => new ScheduledTaskAuditEntry(
                e.TaskId, DateTime.SpecifyKind(e.Utc, DateTimeKind.Utc), e.PerformedBy, e.Action, e.Details, e.Server))
            .ToList();
    }

    /// <summary>Deletes entries older than <paramref name="cutoffUtc"/>.</summary>
    internal void PruneOlderThan(DateTime cutoffUtc)
    {
        if (!_schema.IsReady) return;
        using var scope = _scopeProvider.CreateScope();
        scope.Database.Execute(scope.SqlContext.Sql().Delete<ScheduledTaskAuditEntity>(x => x.Utc < cutoffUtc));
        scope.Complete();
    }

    private static string? Truncate(string? value, int maxLength)
        => value is null || value.Length <= maxLength ? value : value[..maxLength];
}
