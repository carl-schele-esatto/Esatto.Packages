using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core.Configuration;
using Umbraco.Cms.Core.Sync;
using Xunit;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

internal static class TestSupport
{
    public static IOptionsMonitor<ScheduledTasksOptions> Options(ScheduledTasksOptions? options = null)
    {
        var monitor = Substitute.For<IOptionsMonitor<ScheduledTasksOptions>>();
        monitor.CurrentValue.Returns(options ?? new ScheduledTasksOptions());
        return monitor;
    }

    public static ScheduledTaskSetting Setting(
        string taskId,
        bool isPaused = false,
        TimeSpan? period = null,
        string? propertiesJson = null,
        ScheduledTaskCalendar? calendar = null,
        DateTime? lastScheduledUtc = null)
        => new(taskId, isPaused, period, propertiesJson, DateTime.UtcNow, "Test editor", calendar, lastScheduledUtc);

    /// <summary>The calendar evaluator over Umbraco's own cron parser.</summary>
    public static ScheduledTaskCalendarEvaluator Calendars { get; } = new(new NCronTabParser());

    /// <summary>The change a test subject passed to <see cref="IScheduledTaskSettingsStore.Update"/> for a task.</summary>
    public static Func<ScheduledTaskSettingState, ScheduledTaskSettingState> ChangeSavedFor(
        this IScheduledTaskSettingsStore store, string taskId, string? expectedUpdatedBy = "Carl")
    {
        var call = Assert.Single(
            store.ReceivedCalls(),
            c => c.GetMethodInfo().Name == nameof(IScheduledTaskSettingsStore.Update) && Equals(c.GetArguments()[0], taskId));
        Assert.Equal(expectedUpdatedBy, call.GetArguments()[2]);
        return (Func<ScheduledTaskSettingState, ScheduledTaskSettingState>)call.GetArguments()[1]!;
    }
}

/// <summary>A recurring job whose behaviour each test scripts.</summary>
public sealed class ScriptedJob : RecurringBackgroundJobBase
{
    public ScriptedJob(TimeSpan period) : base(period) { }

    public Func<CancellationToken, Task> Body { get; set; } = _ => Task.CompletedTask;

    public int Runs { get; private set; }

    public ServerRole[] Roles { get; set; } = [ServerRole.Single, ServerRole.SchedulingPublisher];

    public override ServerRole[] ServerRoles => Roles;

    public TimeSpan DelayOverride { get; set; } = TimeSpan.FromMinutes(3);

    public override TimeSpan Delay => DelayOverride;

    /// <summary>Released after every run, so tests driving the real timer loop can wait for one.</summary>
    public SemaphoreSlim RunCompleted { get; } = new(0);

    // No back-off after a skipped tick, so skip tests don't wait.
    public override TimeSpan IgnoredDelay => TimeSpan.Zero;

    public void ChangeOwnPeriod(TimeSpan period) => Period = period;

    public override async Task RunJobAsync(CancellationToken cancellationToken)
    {
        Runs++;
        try
        {
            await Body(cancellationToken);
        }
        finally
        {
            RunCompleted.Release();
        }
    }
}

/// <summary>A distributed job whose behaviour each test scripts.</summary>
public sealed class ScriptedDistributedJob : IDistributedBackgroundJob
{
    public ScriptedDistributedJob(string name, TimeSpan period)
    {
        Name = name;
        Period = period;
    }

    public string Name { get; }

    public TimeSpan Period { get; }

    public Func<CancellationToken, Task> Body { get; set; } = _ => Task.CompletedTask;

    public int Runs { get; private set; }

    public Task ExecuteAsync() => ExecuteAsync(CancellationToken.None);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        Runs++;
        await Body(cancellationToken);
    }
}
