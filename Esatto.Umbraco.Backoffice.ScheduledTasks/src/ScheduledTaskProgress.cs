using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// Lets a running job show how far it has got: the dashboard draws a progress bar with the latest
/// percentage and message under the task's status, and the run history keeps the last message.
/// </summary>
/// <remarks>
/// Inject it into the job and report as the work goes along:
/// <code>
/// _progress.Report(this, 100.0 * done / total, $"Checked {done} of {total} articles");
/// </code>
/// Progress is kept in memory on the server that runs the job, like the run history; reporting is
/// cheap, so it's fine to report on every item. Reports made while no run of the job is in progress
/// (e.g. from a constructor) are ignored.
/// </remarks>
public interface IScheduledTaskProgress
{
    /// <summary>Reports the progress of a recurring job's current run.</summary>
    /// <param name="job">The job reporting, usually <c>this</c>.</param>
    /// <param name="percent">0–100; values outside are clamped. Null keeps the previous percentage and only changes the message.</param>
    /// <param name="message">What the job is doing, e.g. "Checked 400 of 1000 articles". Null keeps the previous message.</param>
    void Report(IRecurringBackgroundJob job, double? percent, string? message = null);

    /// <summary>Reports the progress of a distributed job's current run on this server.</summary>
    /// <param name="job">The job reporting, usually <c>this</c>.</param>
    /// <param name="percent">0–100; values outside are clamped. Null keeps the previous percentage and only changes the message.</param>
    /// <param name="message">What the job is doing. Null keeps the previous message.</param>
    void Report(IDistributedBackgroundJob job, double? percent, string? message = null);
}

internal sealed class ScheduledTaskProgress : IScheduledTaskProgress
{
    private readonly IScheduledTaskRunLog _runLog;

    public ScheduledTaskProgress(IScheduledTaskRunLog runLog) => _runLog = runLog;

    public void Report(IRecurringBackgroundJob job, double? percent, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(job);
        _runLog.ReportProgress(ScheduledTaskIdentity.ForJob(job, "to report progress"), percent, message);
    }

    public void Report(IDistributedBackgroundJob job, double? percent, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(job);
        _runLog.ReportProgress(ScheduledTaskIdentity.ForJob(job, "to report progress"), percent, message);
    }
}
