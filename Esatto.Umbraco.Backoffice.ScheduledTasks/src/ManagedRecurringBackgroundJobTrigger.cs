using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Infrastructure.HostedServices;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// Replaces Umbraco's <see cref="IRecurringBackgroundJobTrigger{TJob}"/> so triggers from site code
/// go through <see cref="ManagedRecurringBackgroundJobHostedService{TJob}"/>: they're recorded in
/// the run history, respect the dashboard's pause, and still run on a long interval's heartbeat
/// (where a plain loop trigger would look like an early tick and be skipped).
/// </summary>
/// <remarks>Same contract as Umbraco's: returns false when no hosted service runs the job.</remarks>
internal sealed class ManagedRecurringBackgroundJobTrigger<TJob> : IRecurringBackgroundJobTrigger<TJob>
    where TJob : class, ITriggerableRecurringBackgroundJob
{
    private static readonly string TaskId = ScheduledTaskIdentity.ForRecurring(typeof(TJob));

    private readonly RecurringJobHostRegistry _registry;

    public ManagedRecurringBackgroundJobTrigger(RecurringJobHostRegistry registry) => _registry = registry;

    public bool TriggerExecution() => TriggerExecution(NextExecutionStrategy.None);

    public bool TriggerExecution(NextExecutionStrategy strategy)
        => _registry.TryGet(TaskId, out var host) && host.Trigger(strategy);

    public bool TriggerExecution(TimeSpan nextDelay)
        => _registry.TryGet(TaskId, out var host) && host.Trigger(nextDelay);
}
