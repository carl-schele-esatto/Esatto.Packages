using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>One run of a task on one server (<see cref="ScheduledTaskRun"/>), kept for the history.</summary>
[TableName(TableName)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class ScheduledTaskRunEntity
{
    public const string TableName = "ScheduledTaskRuns";
    public const string RunIdIndexName = "IX_ScheduledTaskRuns_runId";
    public const string TaskStartedIndexName = "IX_ScheduledTaskRuns_taskId_startedUtc";
    public const string StopRequestedByColumn = "stopRequestedBy";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true, IdentitySeed = 1)]
    public int Id { get; set; }

    [Column("runId")]
    [Index(IndexTypes.UniqueNonClustered, Name = RunIdIndexName, ForColumns = "runId")]
    public Guid RunId { get; set; }

    [Column("taskId")]
    [Length(450)]
    [Index(IndexTypes.NonClustered, Name = TaskStartedIndexName, ForColumns = "taskId,startedUtc")]
    public string TaskId { get; set; } = string.Empty;

    [Column("runTrigger")]
    [Length(20)]
    public string Trigger { get; set; } = string.Empty;

    [Column("triggeredBy")]
    [Length(255)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? TriggeredBy { get; set; }

    [Column("server")]
    [Length(255)]
    public string Server { get; set; } = string.Empty;

    [Column("startedUtc")]
    public DateTime StartedUtc { get; set; }

    [Column("finishedUtc")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? FinishedUtc { get; set; }

    [Column("outcome")]
    [Length(20)]
    public string Outcome { get; set; } = string.Empty;

    [Column("message")]
    [Length(2000)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Message { get; set; }

    [Column("details")]
    [SpecialDbType(SpecialDbTypes.NVARCHARMAX)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Details { get; set; }

    [Column("occurrences")]
    public int Occurrences { get; set; }

    [Column("lastOccurrenceUtc")]
    public DateTime LastOccurrenceUtc { get; set; }

    [Column("progress")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public double? Progress { get; set; }

    [Column("progressMessage")]
    [Length(500)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? ProgressMessage { get; set; }

    [Column("attempt")]
    public int Attempt { get; set; }

    [Column("maxAttempts")]
    public int MaxAttempts { get; set; }

    /// <summary>When a failed run is tried again; null = no retry pending.</summary>
    [Column("retryAtUtc")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? RetryAtUtc { get; set; }

    /// <summary>Who asked a running run to stop; also how a stop requested on another server reaches the server running it.</summary>
    [Column(StopRequestedByColumn)]
    [Length(255)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? StopRequestedBy { get; set; }

    /// <summary>The log lines captured during the run, one per line.</summary>
    [Column("log")]
    [SpecialDbType(SpecialDbTypes.NVARCHARMAX)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Log { get; set; }
}

/// <summary>One change an editor made to a task (paused it, changed its schedule, …).</summary>
[TableName(TableName)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class ScheduledTaskAuditEntity
{
    public const string TableName = "ScheduledTaskAudit";
    public const string TaskUtcIndexName = "IX_ScheduledTaskAudit_taskId_utc";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true, IdentitySeed = 1)]
    public int Id { get; set; }

    [Column("taskId")]
    [Length(450)]
    [Index(IndexTypes.NonClustered, Name = TaskUtcIndexName, ForColumns = "taskId,utc")]
    public string TaskId { get; set; } = string.Empty;

    [Column("utc")]
    public DateTime Utc { get; set; }

    [Column("performedBy")]
    [Length(255)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? PerformedBy { get; set; }

    [Column("auditAction")]
    [Length(50)]
    public string Action { get; set; } = string.Empty;

    [Column("details")]
    [Length(4000)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Details { get; set; }

    [Column("server")]
    [Length(255)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Server { get; set; }
}
