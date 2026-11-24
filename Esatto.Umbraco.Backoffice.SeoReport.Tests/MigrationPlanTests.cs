using Esatto.Umbraco.Backoffice.SeoReport.Jobs;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>The migration plan that creates the tables, and the startup hook that runs it.</summary>
public class MigrationPlanTests
{
    [Fact]
    public void The_plan_has_the_documented_name()
        => Assert.Equal("Esatto.Umbraco.Backoffice.SeoReport", new SeoReportMigrationPlan().Name);

    [Fact]
    public void The_plan_is_one_step_from_nothing_to_the_tables()
    {
        var plan = new SeoReportMigrationPlan();

        // A fresh database starts from the empty state and one step reaches the final state; the plan also lists the
        // final state itself, with nothing after it.
        Assert.Equal(string.Empty, plan.InitialState);
        Assert.Equal("create-tables", plan.FinalState);
        Assert.Equal(typeof(CreateSeoReportTablesMigration), plan.Transitions[string.Empty]!.MigrationType);
        Assert.Single(plan.Transitions, transition => transition.Value is not null);
        Assert.Null(plan.Transitions["create-tables"]);
    }

    [Fact]
    public void The_composer_runs_the_migration_at_startup_schedules_the_refresh_job_and_registers_the_services()
    {
        var services = new ServiceCollection();
        var builder = Substitute.For<IUmbracoBuilder>();
        builder.Services.Returns(services);

        new SeoReportComposer().Compose(builder);

        // Each of the three is asserted by itself, so dropping any one of them fails this test: the tables would not be created,
        // the safety net behind Fetch now would not run, or the controller would not be able to resolve what it needs.
        Assert.Single(services, d =>
            d.ServiceType == typeof(INotificationAsyncHandler<UmbracoApplicationStartingNotification>)
            && d.ImplementationType == typeof(RunSeoReportMigration));

        Assert.Single(services, d =>
            d.ServiceType == typeof(IRecurringBackgroundJob)
            && d.ImplementationType == typeof(SeoReportRefreshJob)
            && d.Lifetime == ServiceLifetime.Singleton);

        Assert.Single(services, d => d.ServiceType == typeof(SettingsService) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Single(services, d => d.ServiceType == typeof(FetchCoordinator) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Single(services, d => d.ServiceType == typeof(ISeoReportStore));
    }
}
