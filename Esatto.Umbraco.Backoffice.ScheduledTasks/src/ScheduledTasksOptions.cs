namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// Optional settings, bound from the <c>Esatto:ScheduledTasks</c> configuration section.
/// </summary>
public sealed class ScheduledTasksOptions
{
    public const string SectionName = "Esatto:ScheduledTasks";

    /// <summary>How many runs to keep in memory per task (newest first). Default 25.</summary>
    public int RunHistoryLimit { get; set; } = 25;

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

    /// <summary>Shortest interval an editor may set. Default 1 second.</summary>
    public TimeSpan MinimumPeriod { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Longest interval an editor may set. Default 10 years (3650 days). Recurring tasks with
    /// intervals beyond what a timer can wait are handled by
    /// <see cref="ManagedRecurringBackgroundJobHostedService{TJob}"/>.
    /// </summary>
    public TimeSpan MaximumPeriod { get; set; } = TimeSpan.FromDays(3650);
}
