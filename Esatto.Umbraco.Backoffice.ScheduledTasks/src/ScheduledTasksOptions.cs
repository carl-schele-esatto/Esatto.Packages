using Microsoft.Extensions.Logging;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// Optional settings, bound from the <c>Esatto:ScheduledTasks</c> configuration section.
/// </summary>
public sealed class ScheduledTasksOptions
{
    public const string SectionName = "Esatto:ScheduledTasks";

    /// <summary>How many runs to keep per task (newest first), in memory and in the database. Default 50.</summary>
    public int RunHistoryLimit { get; set; } = 50;

    /// <summary>
    /// Whether runs are saved in the database (table <c>ScheduledTaskRuns</c>), so the history
    /// survives restarts and shows every server's runs. Default true; false keeps it in memory on
    /// each server only.
    /// </summary>
    public bool PersistRunHistory { get; set; } = true;

    /// <summary>
    /// Runs of tasks that run more often than this are kept in memory only, unless they fail, are
    /// stopped or are retried: Umbraco's own housekeeping (e.g. cache instructions every 5 seconds)
    /// would otherwise write to the database all the time. Default 1 minute.
    /// </summary>
    public TimeSpan PersistAllRunsFromPeriod { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Saved runs older than this are deleted. Default 30 days.</summary>
    public TimeSpan RunHistoryRetention { get; set; } = TimeSpan.FromDays(30);

    /// <summary>How often new and changed runs are written to the database. Default 5 seconds.</summary>
    public TimeSpan RunHistoryFlushInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The lowest level of the log messages captured with each run (everything the job and the
    /// code it calls log while it runs). Default <see cref="LogLevel.Information"/>;
    /// <see cref="LogLevel.None"/> turns capturing off. Serilog's own minimum level still applies.
    /// </summary>
    public LogLevel RunLogMinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>The most log lines kept per run; later lines are counted but dropped. Default 500.</summary>
    public int RunLogMaxLines { get; set; } = 500;

    /// <summary>Entries in the change log (who paused, rescheduled, …) older than this are deleted. Default 365 days.</summary>
    public TimeSpan AuditRetention { get; set; } = TimeSpan.FromDays(365);

    /// <summary>
    /// How long the pause / interval settings are cached before they're re-read from the database.
    /// Changes made on this server apply immediately; in a load-balanced setup the other servers
    /// pick them up within this window. Default 30 seconds; values outside 1 second to 24 days are
    /// treated as the nearest of those (a .NET timer can't wait much longer).
    /// </summary>
    public TimeSpan SettingsRefreshInterval { get; set; } = TimeSpan.FromSeconds(30);

    internal static readonly TimeSpan MinSettingsRefreshInterval = TimeSpan.FromSeconds(1);

    internal static readonly TimeSpan MaxSettingsRefreshInterval = TimeSpan.FromDays(24);

    /// <summary><see cref="SettingsRefreshInterval"/> within what the timers waiting on it can wait.</summary>
    internal TimeSpan EffectiveSettingsRefreshInterval
        => SettingsRefreshInterval < MinSettingsRefreshInterval ? MinSettingsRefreshInterval
            : SettingsRefreshInterval > MaxSettingsRefreshInterval ? MaxSettingsRefreshInterval
            : SettingsRefreshInterval;

    /// <summary><see cref="RunHistoryFlushInterval"/> between 1 second and 1 hour.</summary>
    internal TimeSpan EffectiveRunHistoryFlushInterval
        => RunHistoryFlushInterval < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1)
            : RunHistoryFlushInterval > TimeSpan.FromHours(1) ? TimeSpan.FromHours(1)
            : RunHistoryFlushInterval;

    /// <summary>Shortest interval an editor may set. Default 1 second.</summary>
    public TimeSpan MinimumPeriod { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Longest interval an editor may set. Default 10 years (3650 days). Recurring tasks with
    /// intervals beyond what a timer can wait are handled by
    /// <see cref="ManagedRecurringBackgroundJobHostedService{TJob}"/>.
    /// </summary>
    public TimeSpan MaximumPeriod { get; set; } = TimeSpan.FromDays(3650);
}
