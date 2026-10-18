using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Infrastructure.HostedServices;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

public sealed class TriggerableJob : RecurringBackgroundJobBase, ITriggerableRecurringBackgroundJob
{
    public TriggerableJob() : base(TimeSpan.FromMinutes(5)) { }

    public override Task RunJobAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public class ManagedRecurringBackgroundJobTriggerTests
{
    [Fact]
    public void Triggers_from_code_go_to_the_jobs_managed_host()
    {
        var registry = new RecurringJobHostRegistry();
        var host = Substitute.For<IManagedRecurringJobHost>();
        host.TaskId.Returns(ScheduledTaskIdentity.ForRecurring(typeof(TriggerableJob)));
        host.Trigger(Arg.Any<NextExecutionStrategy>()).Returns(true);
        host.Trigger(Arg.Any<TimeSpan>()).Returns(true);
        registry.Register(host);
        var trigger = new ManagedRecurringBackgroundJobTrigger<TriggerableJob>(registry);

        Assert.True(trigger.TriggerExecution());
        Assert.True(trigger.TriggerExecution(NextExecutionStrategy.Replace));
        Assert.True(trigger.TriggerExecution(TimeSpan.FromMinutes(1)));

        host.Received(1).Trigger(NextExecutionStrategy.None);
        host.Received(1).Trigger(NextExecutionStrategy.Replace);
        host.Received(1).Trigger(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Like_Umbracos_trigger_it_returns_false_when_no_host_runs_the_job()
        => Assert.False(new ManagedRecurringBackgroundJobTrigger<TriggerableJob>(new RecurringJobHostRegistry()).TriggerExecution());
}

public class ManualDistributedRunTrackerTests
{
    [Fact]
    public async Task Shutdown_waits_for_runs_started_from_the_dashboard()
    {
        var tracker = new ManualDistributedRunTracker(NullLogger<ManualDistributedRunTracker>.Instance);
        var run = new TaskCompletionSource();
        tracker.Track(run.Task);

        var stopping = tracker.StoppedAsync(CancellationToken.None);
        await Task.Delay(50);
        Assert.False(stopping.IsCompleted);

        run.SetResult();
        await stopping.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Shutdown_gives_up_when_the_host_timeout_expires()
    {
        var tracker = new ManualDistributedRunTracker(NullLogger<ManualDistributedRunTracker>.Instance);
        tracker.Track(new TaskCompletionSource().Task);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await tracker.StoppedAsync(timeout.Token).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Finished_runs_are_forgotten()
    {
        var tracker = new ManualDistributedRunTracker(NullLogger<ManualDistributedRunTracker>.Instance);
        tracker.Track(Task.CompletedTask);

        await tracker.StoppedAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Shutdown_tells_Umbracos_poller_to_stop_before_waiting_for_dashboard_runs()
    {
        // Registered as in a site: Umbraco's poller (AddBackOffice) before the tracker (a composer).
        // Hosted services stop in reverse order, so waiting in StopAsync would keep the poller
        // starting jobs during the wait. The dashboard run here only ends once the poller has stopped.
        var pollerStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<string>();
        _ = pollerStopped.Task.ContinueWith(_ => run.TrySetResult(), TaskScheduler.Default);

        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(10));
                services.AddHostedService(_ => new FakePoller(() => { lock (order) order.Add("poller stopped"); pollerStopped.TrySetResult(); }));
                services.AddSingleton(new ManualDistributedRunTracker(NullLogger<ManualDistributedRunTracker>.Instance));
                services.AddHostedService(sp => sp.GetRequiredService<ManualDistributedRunTracker>());
            })
            .Build();
        await host.StartAsync();
        host.Services.GetRequiredService<ManualDistributedRunTracker>()
            .Track(run.Task.ContinueWith(_ => { lock (order) order.Add("run finished"); }, TaskScheduler.Default));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await host.StopAsync().WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(["poller stopped", "run finished"], order);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Shutdown took {stopwatch.Elapsed} (it waited out the timeout).");
    }

    private sealed class FakePoller(Action onStop) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            onStop();
            return Task.CompletedTask;
        }
    }
}

public class ScheduledTasksControllerTests
{
    [Fact]
    public void Errors_are_problem_details_the_backoffice_recognises()
    {
        // The backoffice only shows a ProblemDetails title when the body has type, title and status.
        var service = Substitute.For<IScheduledTaskService>();
        service.SetPeriod("recurring:X", Arg.Any<TimeSpan?>(), Arg.Any<string?>())
            .Returns(ScheduledTaskCommandResult.Invalid("The interval must be between 1 second and 10 years."));
        var controller = new ScheduledTasksController(service, Substitute.For<IBackOfficeSecurityAccessor>(), TestSupport.Options());

        var result = Assert.IsType<ObjectResult>(controller.SetPeriod(new SetScheduledTaskPeriodRequest("recurring:X", 0.5)));
        var problem = Assert.IsType<ProblemDetails>(result.Value);

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Error", problem.Type);
        Assert.Equal("The interval must be between 1 second and 10 years.", problem.Title);
        Assert.Equal(400, problem.Status);
    }

    [Theory]
    [InlineData(60479.99999999999, 60480)] // 0.7 days as a browser computes it
    [InlineData(245.99999999999997, 246)] // 4.1 minutes
    [InlineData(1.5, 1.5)]
    public void Intervals_from_the_browser_are_rounded_to_whole_milliseconds(double sent, double expectedSeconds)
    {
        var service = Substitute.For<IScheduledTaskService>();
        service.SetPeriod(Arg.Any<string>(), Arg.Any<TimeSpan?>(), Arg.Any<string?>())
            .Returns(ScheduledTaskCommandResult.Ok("Interval updated."));
        var controller = new ScheduledTasksController(service, Substitute.For<IBackOfficeSecurityAccessor>(), TestSupport.Options());

        controller.SetPeriod(new SetScheduledTaskPeriodRequest("recurring:X", sent));

        service.Received(1).SetPeriod("recurring:X", TimeSpan.FromSeconds(expectedSeconds), Arg.Any<string?>());
    }

    [Fact]
    public void An_interval_past_TimeSpan_range_reaches_the_service_as_the_maximum()
    {
        var service = Substitute.For<IScheduledTaskService>();
        service.SetPeriod(Arg.Any<string>(), Arg.Any<TimeSpan?>(), Arg.Any<string?>())
            .Returns(ScheduledTaskCommandResult.Invalid("Too long."));
        var controller = new ScheduledTasksController(service, Substitute.For<IBackOfficeSecurityAccessor>(), TestSupport.Options());

        controller.SetPeriod(new SetScheduledTaskPeriodRequest("recurring:X", 1e300));

        service.Received(1).SetPeriod("recurring:X", TimeSpan.MaxValue, Arg.Any<string?>());
    }
}
