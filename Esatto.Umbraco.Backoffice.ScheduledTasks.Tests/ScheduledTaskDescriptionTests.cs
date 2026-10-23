using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

[ScheduledTaskDescription("Only in English.")]
public sealed class EnglishOnlyDescribedJob : RecurringBackgroundJobBase
{
    public EnglishOnlyDescribedJob() : base(TimeSpan.FromMinutes(5)) { }

    public override Task RunJobAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public class ScheduledTaskDescriptionTests
{
    private readonly ServiceHarness _harness = new();

    [Fact]
    public void The_attribute_gives_both_languages()
    {
        var description = ScheduledTaskJobDescriptions.For(typeof(FeedJob));

        Assert.Equal(new ScheduledTaskJobDescription("Imports the product feed.", "Importerar produktflödet."), description);
    }

    [Fact]
    public void Swedish_falls_back_to_english_when_not_given()
    {
        var description = ScheduledTaskJobDescriptions.For(typeof(EnglishOnlyDescribedJob));

        Assert.Equal("Only in English.", description!.English);
        Assert.Equal("Only in English.", description.Swedish);
    }

    [Fact]
    public void An_umbraco_job_gets_its_canned_description()
    {
        // Umbraco's jobs are internal, so the type is looked up by name.
        var type = typeof(RecurringBackgroundJobBase).Assembly
            .GetType("Umbraco.Cms.Infrastructure.BackgroundJobs.Jobs.DistributedJobs.ScheduledPublishingJob")!;

        var description = ScheduledTaskJobDescriptions.For(type);

        Assert.NotNull(description);
        Assert.Contains("Publishes", description.English);
        Assert.Contains("Publicerar", description.Swedish);
    }

    [Fact]
    public void An_undecorated_custom_job_has_no_description()
        => Assert.Null(ScheduledTaskJobDescriptions.For(typeof(ScriptedJob)));

    [Fact]
    public void Every_canned_type_exists_in_umbraco()
    {
        var umbraco = typeof(RecurringBackgroundJobBase).Assembly;

        foreach (var name in ScheduledTaskJobDescriptions.CannedTypeNames)
        {
            Assert.True(umbraco.GetType(name) is not null, $"{name} is not a type in Umbraco.Infrastructure");
        }
    }

    [Fact]
    public void The_overview_carries_both_languages()
    {
        var dto = _harness.Create().Get(ServiceHarness.FeedId)!.ToDto();

        Assert.Equal("Imports the product feed.", dto.DescriptionEn);
        Assert.Equal("Importerar produktflödet.", dto.DescriptionSv);
    }

    [Fact]
    public void The_overview_has_no_description_for_an_undecorated_job()
    {
        var dto = _harness.Create().Get(ServiceHarness.RecurringId)!.ToDto();

        Assert.Null(dto.DescriptionEn);
        Assert.Null(dto.DescriptionSv);
    }
}
