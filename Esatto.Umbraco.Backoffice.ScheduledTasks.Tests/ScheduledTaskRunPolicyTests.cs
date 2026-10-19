using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

[ScheduledTaskRunPolicy(Retries = 3, RetryDelaySeconds = 120, TimeoutSeconds = 600)]
public class RetryingJob : RecurringBackgroundJobBase
{
    public RetryingJob() : base(TimeSpan.FromMinutes(5)) { }

    public override Task RunJobAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class DerivedRetryingJob : RetryingJob;

[ScheduledTaskRunPolicy(Retries = 50, RetryDelaySeconds = 0, TimeoutSeconds = 30 * 24 * 3600)]
public sealed class OverreachingJob : RecurringBackgroundJobBase
{
    public OverreachingJob() : base(TimeSpan.FromMinutes(5)) { }

    public override Task RunJobAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public class ScheduledTaskRunPolicyTests
{
    [Fact]
    public void A_job_without_the_attribute_has_no_retries_and_no_time_limit()
    {
        var defaults = ScheduledTaskRunPolicies.Defaults(typeof(ScriptedJob));

        Assert.Equal(EffectiveRunPolicy.None, defaults);
        Assert.Equal(1, defaults.MaxAttempts);
        Assert.Null(defaults.Timeout);
    }

    [Fact]
    public void The_attribute_sets_the_jobs_defaults()
    {
        var defaults = ScheduledTaskRunPolicies.Defaults(typeof(RetryingJob));

        Assert.Equal(new EffectiveRunPolicy(3, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10)), defaults);
        Assert.Equal(4, defaults.MaxAttempts);
    }

    [Fact]
    public void A_derived_job_inherits_the_attribute()
        => Assert.Equal(3, ScheduledTaskRunPolicies.Defaults(typeof(DerivedRetryingJob)).Retries);

    [Fact]
    public void Attribute_values_outside_the_allowed_range_are_clamped()
    {
        var defaults = ScheduledTaskRunPolicies.Defaults(typeof(OverreachingJob));

        Assert.Equal(ScheduledTaskRunPolicies.MaxRetries, defaults.Retries);
        Assert.Equal(ScheduledTaskRunPolicies.MinRetryDelay, defaults.RetryDelay);
        Assert.Equal(ScheduledTaskRunPolicies.MaxTimeout, defaults.Timeout);
    }

    [Fact]
    public void Editor_values_replace_the_defaults_one_by_one()
    {
        var resolved = ScheduledTaskRunPolicies.Resolve(typeof(RetryingJob), new ScheduledTaskRunPolicy(1, null, null));

        Assert.Equal(new EffectiveRunPolicy(1, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10)), resolved);
    }

    [Fact]
    public void A_zero_time_limit_removes_the_jobs_own_limit()
        => Assert.Null(ScheduledTaskRunPolicies.Resolve(typeof(RetryingJob), new ScheduledTaskRunPolicy(null, null, TimeSpan.Zero)).Timeout);

    [Theory]
    [InlineData(-1, null, null, "Retries must be between 0 and 10.")]
    [InlineData(11, null, null, "Retries must be between 0 and 10.")]
    [InlineData(null, 0.5, null, "The wait before a retry must be between 1 second and 1 day.")]
    [InlineData(null, 86401d, null, "The wait before a retry must be between 1 second and 1 day.")]
    [InlineData(null, null, 0.5, "The time limit must be between 1 second and 7 days.")]
    [InlineData(null, null, -5d, "The time limit must be between 1 second and 7 days.")]
    [InlineData(null, null, 604801d, "The time limit must be between 1 second and 7 days.")]
    public void Values_outside_the_allowed_range_are_refused(int? retries, double? delaySeconds, double? timeoutSeconds, string expected)
        => Assert.Equal(expected, ScheduledTaskRunPolicies.Validate(Policy(retries, delaySeconds, timeoutSeconds)));

    [Theory]
    [InlineData(0, 1d, 0d)]
    [InlineData(10, 86400d, 604800d)]
    [InlineData(null, null, null)]
    public void Values_within_the_allowed_range_are_accepted(int? retries, double? delaySeconds, double? timeoutSeconds)
        => Assert.Null(ScheduledTaskRunPolicies.Validate(Policy(retries, delaySeconds, timeoutSeconds)));

    [Fact]
    public void A_stored_policy_reads_back_as_it_was_saved()
    {
        var policy = new ScheduledTaskRunPolicy(2, TimeSpan.FromSeconds(90), TimeSpan.Zero);

        var json = ScheduledTaskRunPolicies.Serialize(policy);

        Assert.Equal("""{"retries":2,"retryDelaySeconds":90,"timeoutSeconds":0}""", json);
        Assert.Equal(policy, ScheduledTaskRunPolicies.Deserialize(json));
    }

    [Fact]
    public void An_empty_policy_is_not_stored()
    {
        Assert.Null(ScheduledTaskRunPolicies.Serialize(null));
        Assert.Null(ScheduledTaskRunPolicies.Serialize(new ScheduledTaskRunPolicy(null, null, null)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"retries":99}""")]
    [InlineData("""{"retryDelaySeconds":-1}""")]
    public void A_stored_policy_that_is_missing_unreadable_or_out_of_range_falls_back_to_the_defaults(string? json)
        => Assert.Null(ScheduledTaskRunPolicies.Deserialize(json));

    [Theory]
    [InlineData(0, 60, null, "no retries · no time limit")]
    [InlineData(1, 60, null, "1 retry after 1 minute · no time limit")]
    [InlineData(3, 120, 600d, "3 retries, 2 minutes apart · time limit 10 minutes")]
    public void Policies_are_described_for_the_change_log(int retries, int delaySeconds, double? timeoutSeconds, string expected)
        => Assert.Equal(
            expected,
            ScheduledTaskRunPolicies.Describe(new EffectiveRunPolicy(
                retries, TimeSpan.FromSeconds(delaySeconds), timeoutSeconds is { } t ? TimeSpan.FromSeconds(t) : null)));

    private static ScheduledTaskRunPolicy Policy(int? retries, double? delaySeconds, double? timeoutSeconds)
        => new(
            retries,
            delaySeconds is { } delay ? TimeSpan.FromSeconds(delay) : null,
            timeoutSeconds is { } timeout ? TimeSpan.FromSeconds(timeout) : null);
}
