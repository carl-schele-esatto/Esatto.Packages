using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

public class ScheduledTaskIdentityTests
{
    [Theory]
    [InlineData("Umbraco.Cms.Infrastructure.BackgroundJobs.Jobs.TempFileCleanupJob", "Temp file cleanup")]
    [InlineData("MemoryCacheSizeReportingJob", "Memory cache size reporting")]
    [InlineData("WebhookFiring", "Webhook firing")]
    [InlineData("SendHTMLReportJob", "Send HTML report")]
    [InlineData("OpenIddictCleanupJob", "OpenIddict cleanup")]
    [InlineData("PruneOpenIddictTokensJob", "Prune OpenIddict tokens")]
    [InlineData("My.Site.Jobs+NestedJob", "Nested")]
    [InlineData("GenericJob`1", "Generic")]
    [InlineData("Job", "Job")]
    [InlineData("nightly_export", "Nightly export")]
    [InlineData("", "")]
    public void Humanize_turns_type_and_job_names_into_sentence_case(string input, string expected)
        => Assert.Equal(expected, ScheduledTaskIdentity.Humanize(input));

    [Fact]
    public void Ids_are_prefixed_by_kind()
    {
        var recurring = ScheduledTaskIdentity.ForRecurring(typeof(ScheduledTaskIdentityTests));
        var distributed = ScheduledTaskIdentity.ForDistributed("WebhookFiring");

        Assert.Equal("recurring:Esatto.Umbraco.Backoffice.ScheduledTasks.Tests.ScheduledTaskIdentityTests", recurring);
        Assert.Equal("distributed:WebhookFiring", distributed);
        Assert.True(ScheduledTaskIdentity.IsRecurring(recurring));
        Assert.False(ScheduledTaskIdentity.IsDistributed(recurring));
        Assert.True(ScheduledTaskIdentity.IsDistributed(distributed));
    }
}
