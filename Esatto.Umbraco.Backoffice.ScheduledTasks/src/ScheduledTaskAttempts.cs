using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

internal enum AttemptOutcome
{
    Succeeded,
    Failed,

    /// <summary>Someone stopped it from the dashboard.</summary>
    Stopped,

    /// <summary>The application is shutting down.</summary>
    ShutDown,
}

/// <param name="Exception">For <see cref="AttemptOutcome.Failed"/>: what failed (a <see cref="TimeoutException"/> for a time limit).</param>
internal sealed record AttemptResult(AttemptOutcome Outcome, Guid RunId, ExceptionDispatchInfo? Exception = null);

/// <summary>
/// Runs one attempt of a task the same way for recurring and distributed jobs: with the
/// execution's token (stop, shutdown) plus the time limit, the run's log captured, and the outcome
/// recorded in the run log.
/// </summary>
internal static class ScheduledTaskAttempts
{
    public static async Task<AttemptResult> RunAsync(
        IScheduledTaskRunLog runLog,
        ScheduledTaskExecution execution,
        string taskId,
        Guid runId,
        TimeSpan? timeout,
        LogLevel captureLevel,
        Func<CancellationToken, Task> body)
    {
        using var attempt = execution.BeginAttempt(runId, timeout);
        try
        {
            using (ScheduledTaskLogCapture.Begin(runLog, taskId, runId, captureLevel))
            {
                await body(attempt.Token);
            }
        }
        // Whatever a job throws once it's asked to stop counts as stopping, not as a failure.
        catch (Exception) when (execution.IsShuttingDown)
        {
            runLog.Cancel(taskId, runId);
            return new AttemptResult(AttemptOutcome.ShutDown, runId);
        }
        catch (Exception) when (execution.IsStopRequested)
        {
            runLog.Cancel(taskId, runId, $"Stopped by {execution.StopRequestedBy}.");
            return new AttemptResult(AttemptOutcome.Stopped, runId);
        }
        catch (Exception ex) when (attempt.TimedOut)
        {
            var message = $"Timed out after {ScheduledTaskService.DescribePeriod(timeout!.Value)}.";
            runLog.Fail(taskId, runId, ex, message);
            return new AttemptResult(AttemptOutcome.Failed, runId, ExceptionDispatchInfo.Capture(new TimeoutException(message, ex)));
        }
        catch (Exception ex)
        {
            runLog.Fail(taskId, runId, ex);
            return new AttemptResult(AttemptOutcome.Failed, runId, ExceptionDispatchInfo.Capture(ex));
        }

        // Finished normally, but maybe only after being asked to stop: say so, it's worth knowing.
        if (attempt.TimedOut)
            runLog.Succeed(taskId, runId, $"Finished after its time limit of {ScheduledTaskService.DescribePeriod(timeout!.Value)}: the job doesn't stop when asked to.");
        else if (execution.IsStopRequested)
            runLog.Succeed(taskId, runId, $"Finished although {execution.StopRequestedBy} asked it to stop: the job doesn't stop when asked to.");
        else
            runLog.Succeed(taskId, runId);

        return new AttemptResult(AttemptOutcome.Succeeded, runId);
    }

    /// <summary>
    /// Whether a run of a task on this schedule goes to the database even when it succeeds
    /// (see <see cref="ScheduledTasksOptions.PersistAllRunsFromPeriod"/>).
    /// </summary>
    public static bool PersistsAllRuns(TimeSpan period, bool onCalendar, ScheduledTasksOptions options)
        => onCalendar || period == Timeout.InfiniteTimeSpan || period >= options.PersistAllRunsFromPeriod;
}
