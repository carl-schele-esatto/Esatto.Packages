using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Infrastructure.Services;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// Wires Esatto.Umbraco.Backoffice.ScheduledTasks into Umbraco:
/// <list type="bullet">
///   <item>Hosts every recurring job in <see cref="ManagedRecurringBackgroundJobHostedService{TJob}"/>
///   (by replacing the hosted-service factory Umbraco's job runner resolves).</item>
///   <item>Decorates <see cref="IDistributedJobService"/> with <see cref="ManagedDistributedJobService"/>.</item>
///   <item>Registers the settings table migration and the dashboard's services.</item>
/// </list>
/// Composers run after Umbraco's own registrations (<c>AddBackOffice()</c> precedes
/// <c>AddComposers()</c>), so both replacements land on top of the defaults. Jobs registered by any
/// composer are covered: the runner resolves the job list only when the host starts.
/// </summary>
public sealed class ScheduledTasksComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddOptions<ScheduledTasksOptions>()
            .Bind(builder.Config.GetSection(ScheduledTasksOptions.SectionName));

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddSingleton<ScheduledTasksSchemaState>();
        builder.Services.TryAddSingleton<IScheduledTaskSettingsStore, ScheduledTaskSettingsStore>();
        builder.Services.TryAddSingleton<IScheduledTaskRunLog, ScheduledTaskRunLog>();
        builder.Services.TryAddSingleton<RecurringJobHostRegistry>();
        builder.Services.TryAddSingleton<IDistributedJobTable, DistributedJobTable>();
        builder.Services.TryAddSingleton<IScheduledTaskService, ScheduledTaskService>();
        builder.Services.TryAddSingleton<ScheduledTaskCalendarEvaluator>();
        builder.Services.TryAddSingleton<ScheduledTaskPropertyValues>();
        builder.Services.TryAddSingleton<IScheduledTaskProperties, ScheduledTaskProperties>();
        builder.Services.TryAddSingleton<IScheduledTaskProgress, ScheduledTaskProgress>();
        builder.Services.TryAddSingleton<ManualDistributedRunTracker>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<ManualDistributedRunTracker>());
        builder.Services.AddHostedService<ScheduledTaskSettingsSynchronizer>();

        builder.Services.RemoveAll<Func<IRecurringBackgroundJob, IHostedService>>();
        builder.Services.AddSingleton<Func<IRecurringBackgroundJob, IHostedService>>(CreateRecurringJobHostFactory);

        // Code triggers go through the managed hosts too (history, pause, long-interval heartbeat).
        builder.Services.RemoveAll(typeof(IRecurringBackgroundJobTrigger<>));
        builder.Services.AddSingleton(typeof(IRecurringBackgroundJobTrigger<>), typeof(ManagedRecurringBackgroundJobTrigger<>));

        builder.Services.TryDecorate<IDistributedJobService>((sp, inner) =>
            ActivatorUtilities.CreateInstance<ManagedDistributedJobService>(sp, inner));

        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, RunScheduledTasksMigration>();
        builder.AddNotificationHandler<UmbracoApplicationStartedNotification, ApplyScheduledTaskSettingsOnStartup>();
    }

    private static Func<IRecurringBackgroundJob, IHostedService> CreateRecurringJobHostFactory(IServiceProvider serviceProvider)
        => job =>
        {
            var hostType = typeof(ManagedRecurringBackgroundJobHostedService<>).MakeGenericType(job.GetType());
            return (IHostedService)ActivatorUtilities.CreateInstance(serviceProvider, hostType, job);
        };
}

/// <summary>
/// Applies stored interval overrides to the recurring job hosts once the database is available.
/// (<see cref="ScheduledTaskSettingsSynchronizer"/> keeps them in step afterwards.)
/// </summary>
public sealed class ApplyScheduledTaskSettingsOnStartup : INotificationHandler<UmbracoApplicationStartedNotification>
{
    private readonly RecurringJobHostRegistry _registry;

    public ApplyScheduledTaskSettingsOnStartup(RecurringJobHostRegistry registry) => _registry = registry;

    public void Handle(UmbracoApplicationStartedNotification notification)
    {
        foreach (var host in _registry.All) host.SyncWithSettings();
    }
}

internal static class ScheduledTasksServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the registration of <typeparamref name="TService"/> with a decorator around it.
    /// Returns false (and changes nothing) when the service isn't registered.
    /// </summary>
    public static bool TryDecorate<TService>(
        this IServiceCollection services,
        Func<IServiceProvider, TService, TService> decorate)
        where TService : class
    {
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(TService) && !d.IsKeyedService);
        if (descriptor is null) return false;

        services.RemoveAll<TService>();
        services.Add(ServiceDescriptor.Describe(
            typeof(TService),
            sp => decorate(sp, (TService)CreateInner(sp, descriptor)),
            descriptor.Lifetime));
        return true;
    }

    private static object CreateInner(IServiceProvider serviceProvider, ServiceDescriptor descriptor)
        => descriptor.ImplementationInstance
           ?? descriptor.ImplementationFactory?.Invoke(serviceProvider)
           ?? ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType!);
}
