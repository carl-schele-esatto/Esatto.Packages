using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// The last calendar occurrence a recurring task handled (ran, or skipped while paused), one row per
/// task and server: a recurring job runs in-process on every server whose role matches, so each
/// server keeps its own record, and a restart of that server neither repeats nor loses one.
/// </summary>
[TableName(TableName)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class ScheduledTaskOccurrenceEntity
{
    public const string TableName = "ScheduledTaskOccurrences";
    public const string TaskServerIndexName = "IX_ScheduledTaskOccurrences_taskId_server";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true, IdentitySeed = 1)]
    public int Id { get; set; }

    [Column("taskId")]
    [Length(450)]
    [Index(IndexTypes.UniqueNonClustered, Name = TaskServerIndexName, ForColumns = "taskId,server")]
    public string TaskId { get; set; } = string.Empty;

    [Column("server")]
    [Length(255)]
    public string Server { get; set; } = string.Empty;

    /// <summary>The occurrence (UTC) last handled on this server.</summary>
    [Column("occurrenceUtc")]
    public DateTime OccurrenceUtc { get; set; }

    /// <summary>
    /// <see cref="ScheduledTaskCalendar.SavedUtc"/> (in ticks, so it compares exactly) of the
    /// schedule the occurrence belongs to; a record for another schedule doesn't count.
    /// </summary>
    [Column("scheduleSavedTicks")]
    public long ScheduleSavedTicks { get; set; }
}
