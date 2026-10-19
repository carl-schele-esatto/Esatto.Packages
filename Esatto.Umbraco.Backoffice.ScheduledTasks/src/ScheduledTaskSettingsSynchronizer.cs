using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// Every <see cref="ScheduledTasksOptions.SettingsRefreshInterval"/>, re-applies the stored
/// interval settings to each recurring job host on this server. That makes an interval changed on
/// another server (or while a job was running) reach Umbraco's timer loop within the interval,
/// rather than at the job's next tick plus one more old period.
/// </summary>
internal sealed class ScheduledTaskSettingsSynchronizer : BackgroundService
{
    private readonly RecurringJobHostRegistry _registry;
    private readonly IRuntimeState _runtimeState;
    private readonly IOptionsMonitor<ScheduledTasksOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ScheduledTaskSettingsSynchronizer> _logger;

    public ScheduledTaskSettingsSynchronizer(
        RecurringJobHostRegistry registry,
        IRuntimeState runtimeState,
        IOptionsMonitor<ScheduledTasksOptions> options,
        TimeProvider timeProvider,
        ILogger<ScheduledTaskSettingsSynchronizer> logger)
    {
        _registry = registry;
        _runtimeState = runtimeState;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // As Umbraco does for its job hosts: don't let the loop capture the host's ExecutionContext
        // (Umbraco's ambient scope stack is AsyncLocal).
        using (ExecutionContext.IsFlowSuppressed() ? null : (IDisposable?)ExecutionContext.SuppressFlow())
        {
            return base.StartAsync(cancellationToken);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Clamped: a wait beyond what a timer can wait would fault this service and stop the site.
            var interval = _options.CurrentValue.EffectiveSettingsRefreshInterval;

            try
            {
                await Task.Delay(interval, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // Before the database is ready the settings read as defaults; don't apply those.
            if (_runtimeState.Level != RuntimeLevel.Run) continue;

            foreach (var host in _registry.All)
            {
                try
                {
                    host.SyncWithSettings();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not apply the Scheduled Tasks settings to {TaskId}", host.TaskId);
                }
            }
        }
    }
}
