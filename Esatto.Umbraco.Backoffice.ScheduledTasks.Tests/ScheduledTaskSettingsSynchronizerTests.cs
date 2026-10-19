using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

public class ScheduledTaskSettingsSynchronizerTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 18, 8, 0, 0, TimeSpan.Zero));
    private readonly IRuntimeState _runtimeState = Substitute.For<IRuntimeState>();
    private readonly RecurringJobHostRegistry _registry = new();
    private readonly IManagedRecurringJobHost _host = Substitute.For<IManagedRecurringJobHost>();

    public ScheduledTaskSettingsSynchronizerTests()
    {
        _runtimeState.Level.Returns(RuntimeLevel.Run);
        _host.TaskId.Returns("recurring:Test");
        _registry.Register(_host);
    }

    [Theory]
    [InlineData("00:00:00", "00:00:01")]
    [InlineData("00:00:30", "00:00:30")]
    [InlineData("24.00:00:00", "24.00:00:00")]
    [InlineData("365.00:00:00", "24.00:00:00")]
    public void The_refresh_interval_is_kept_within_what_a_timer_can_wait(string configured, string effective)
    {
        var options = new ScheduledTasksOptions { SettingsRefreshInterval = TimeSpan.Parse(configured) };

        Assert.Equal(TimeSpan.Parse(effective), options.EffectiveSettingsRefreshInterval);
    }

    [Fact]
    public async Task A_refresh_interval_longer_than_a_timer_can_wait_does_not_fault_the_service()
    {
        // Regression: 365 days faulted the BackgroundService at startup, which stops the site.
        var options = TestSupport.Options(new ScheduledTasksOptions { SettingsRefreshInterval = TimeSpan.FromDays(365) });
        using var synchronizer = new ScheduledTaskSettingsSynchronizer(
            _registry, _runtimeState, options, _time, NullLogger<ScheduledTaskSettingsSynchronizer>.Instance);

        await synchronizer.StartAsync(CancellationToken.None);
        try
        {
            await Task.Delay(200); // let the loop arm its wait
            Assert.False(synchronizer.ExecuteTask!.IsFaulted, synchronizer.ExecuteTask.Exception?.ToString());

            _time.Advance(TimeSpan.FromDays(24));
            for (var attempt = 0; attempt < 50 && _host.ReceivedCalls().All(c => c.GetMethodInfo().Name != nameof(IManagedRecurringJobHost.SyncWithSettings)); attempt++)
            {
                await Task.Delay(20);
            }

            _host.Received().SyncWithSettings();
            Assert.False(synchronizer.ExecuteTask.IsCompleted);
        }
        finally
        {
            await synchronizer.StopAsync(CancellationToken.None);
        }
    }
}
