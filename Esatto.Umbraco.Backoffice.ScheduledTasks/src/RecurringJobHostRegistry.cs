using System.Collections.Concurrent;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Infrastructure.HostedServices;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>The control surface of a recurring job's hosted service on this server.</summary>
public interface IManagedRecurringJobHost
{
    string TaskId { get; }

    IRecurringBackgroundJob Job { get; }

    /// <summary>The interval the host is currently scheduling with (override or the job's own).</summary>
    TimeSpan EffectivePeriod { get; }

    /// <summary>Best estimate of the next scheduled tick; null when the job only runs on demand.</summary>
    DateTime? NextRunUtc { get; }

    bool IsRunning { get; }

    /// <summary>Runs the job now on behalf of a dashboard user, even if paused. False if it is already running.</summary>
    bool RequestRun(string? triggeredBy);

    /// <summary>Runs the job now on behalf of code (<c>IRecurringBackgroundJobTrigger</c>); respects the pause.</summary>
    bool Trigger(NextExecutionStrategy strategy);

    /// <summary>
    /// Runs the job now on behalf of code, then waits <paramref name="nextDelay"/> (infinite: only
    /// triggers run it until the interval changes); respects the pause.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nextDelay"/> is negative (other than infinite).</exception>
    bool Trigger(TimeSpan nextDelay);

    /// <summary>Re-reads the pause/interval settings and applies the interval to the schedule.</summary>
    void SyncWithSettings();
}

/// <summary>
/// The managed hosts created on this server, by task id. Filled by the hosts themselves as
/// Umbraco's job runner creates them at startup.
/// </summary>
public sealed class RecurringJobHostRegistry
{
    private readonly ConcurrentDictionary<string, IManagedRecurringJobHost> _hosts = new(StringComparer.Ordinal);

    public void Register(IManagedRecurringJobHost host) => _hosts[host.TaskId] = host;

    public bool TryGet(string taskId, out IManagedRecurringJobHost host)
        => _hosts.TryGetValue(taskId, out host!);

    public IReadOnlyCollection<IManagedRecurringJobHost> All => _hosts.Values.ToArray();
}
