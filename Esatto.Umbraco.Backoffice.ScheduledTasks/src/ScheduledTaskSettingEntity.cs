using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// One row per task an editor changed: paused, interval override and/or property values.
/// Tasks running on their defaults have no row.
/// </summary>
[TableName(TableName)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class ScheduledTaskSettingEntity
{
    public const string TableName = "ScheduledTaskSettings";
    public const string TaskIdIndexName = "IX_ScheduledTaskSettings_taskId";
    public const string PropertiesColumn = "propertiesJson";
    public const string ScheduleColumn = "scheduleJson";
    public const string LastScheduledColumn = "lastScheduledUtc";
    public const string RunPolicyColumn = "runPolicyJson";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true, IdentitySeed = 1)]
    public int Id { get; set; }

    [Column("taskId")]
    [Length(450)]
    [Index(IndexTypes.UniqueNonClustered, Name = TaskIdIndexName, ForColumns = "taskId")]
    public string TaskId { get; set; } = string.Empty;

    [Column("isPaused")]
    public bool IsPaused { get; set; }

    /// <summary>Interval override in ticks; null = use the job's own period.</summary>
    [Column("periodTicks")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public long? PeriodTicks { get; set; }

    /// <summary>A calendar schedule (<see cref="ScheduledTaskCalendar"/>) as JSON; null = runs on an interval.</summary>
    [Column(ScheduleColumn)]
    [Length(2000)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? ScheduleJson { get; set; }

    /// <summary>The last calendar occurrence a recurring task handled (ran, or skipped while paused).</summary>
    [Column(LastScheduledColumn)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? LastScheduledUtc { get; set; }

    /// <summary>Editor-set property values as a JSON object (alias → value); null = all defaults.</summary>
    [Column(PropertiesColumn)]
    [SpecialDbType(SpecialDbTypes.NVARCHARMAX)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? PropertiesJson { get; set; }

    /// <summary>Retries and time limit (<see cref="ScheduledTaskRunPolicy"/>) as JSON; null = the job's defaults.</summary>
    [Column(RunPolicyColumn)]
    [Length(1000)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? RunPolicyJson { get; set; }

    [Column("updatedUtc")]
    public DateTime UpdatedUtc { get; set; }

    [Column("updatedBy")]
    [Length(255)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? UpdatedBy { get; set; }
}
