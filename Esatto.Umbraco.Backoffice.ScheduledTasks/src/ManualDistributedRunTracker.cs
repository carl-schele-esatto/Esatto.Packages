using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// Keeps track of distributed task runs started from the dashboard, and makes shutdown wait for
/// them the way it waits for Umbraco's own poller. A run interrupted halfway would otherwise leave
/// its <c>umbracoDistributedJob</c> row marked as running, which stops the task on every server
/// until Umbraco considers it stale (period + <c>MaximumExecutionTime</c>).
/// </summary>
/// <remarks>
/// The runs get <c>IHostApplicationLifetime.ApplicationStopping</c> as their cancellation token, so
/// a job that honours it stops early; each run's <c>finally</c> then marks the row as finished.
/// The wait happens in <see cref="StoppedAsync"/>, after every hosted service's <c>StopAsync</c>:
/// waiting in <c>StopAsync</c> would run before Umbraco's distributed poller is told to stop (hosted
/// services stop in reverse registration order), so it would keep starting jobs during the wait
/// and could be cut off mid-job by the shutdown timeout.
/// </remarks>
internal sealed class ManualDistributedRunTracker : IHostedLifecycleService
{
    private readonly ConcurrentDictionary<long, Task> _runs = new();
    private readonly ILogger<ManualDistributedRunTracker> _logger;
    private long _nextId;

    public ManualDistributedRunTracker(ILogger<ManualDistributedRunTracker> logger) => _logger = logger;

    public void Track(Task run)
    {
        var id = Interlocked.Increment(ref _nextId);
        _runs[id] = run;
        run.ContinueWith(
            _ => _runs.TryRemove(id, out var _),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Called by the host once every hosted service has stopped, with the same shutdown timeout.</summary>
    public async Task StoppedAsync(CancellationToken cancellationToken)
    {
        var runs = _runs.Values.ToArray();
        if (runs.Length == 0) return;

        _logger.LogInformation("Waiting for {Count} scheduled task run(s) started from the dashboard to finish", runs.Length);
        try
        {
            await Task.WhenAll(runs).WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Shutdown timed out before {Count} scheduled task run(s) started from the dashboard finished; " +
                "their tasks stay marked as running until Umbraco considers them stale, or until reset in the dashboard",
                _runs.Count);
        }
        catch
        {
            // The runs log their own failures.
        }
    }
}
