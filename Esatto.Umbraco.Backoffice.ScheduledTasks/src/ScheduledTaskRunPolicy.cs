using System.Text.Json;
using System.Text.Json.Serialization;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// What happens when a run of the task fails or takes too long, as an editor set it. Each null
/// value falls back to the job's <see cref="ScheduledTaskRunPolicyAttribute"/> (or the package
/// default: no retries, no time limit).
/// </summary>
/// <param name="Retries">How many times a failed run is tried again (0 = never).</param>
/// <param name="RetryDelay">How long to wait before each retry.</param>
/// <param name="Timeout">How long a run may take before it's asked to stop and counted as failed; <see cref="TimeSpan.Zero"/> = no limit.</param>
public sealed record ScheduledTaskRunPolicy(int? Retries, TimeSpan? RetryDelay, TimeSpan? Timeout)
{
    public bool IsEmpty => Retries is null && RetryDelay is null && Timeout is null;
}

/// <summary>A task's run policy with every value decided.</summary>
/// <param name="Timeout">Null = no time limit.</param>
public sealed record EffectiveRunPolicy(int Retries, TimeSpan RetryDelay, TimeSpan? Timeout)
{
    public static readonly EffectiveRunPolicy None = new(0, ScheduledTaskRunPolicies.DefaultRetryDelay, null);

    public int MaxAttempts => 1 + Retries;
}

/// <summary>
/// The default retries and time limit of a job, which editors can change in the dashboard:
/// <code>
/// [ScheduledTaskRunPolicy(Retries = 3, RetryDelaySeconds = 120, TimeoutSeconds = 600)]
/// public class ProductFeedImportJob : RecurringBackgroundJobBase { … }
/// </code>
/// </summary>
/// <remarks>
/// A time limit cancels the <see cref="CancellationToken"/> the job gets, so it only stops a job
/// that observes the token. Retries of a distributed job are scheduled through Umbraco's job table
/// (the job is due again after the delay), so waiting doesn't hold up Umbraco's other distributed jobs.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class ScheduledTaskRunPolicyAttribute : Attribute
{
    /// <summary>How many times a failed run is tried again. Default 0.</summary>
    public int Retries { get; set; }

    /// <summary>Seconds to wait before each retry. Default 60.</summary>
    public int RetryDelaySeconds { get; set; } = (int)ScheduledTaskRunPolicies.DefaultRetryDelay.TotalSeconds;

    /// <summary>Seconds a run may take before it's asked to stop and counted as failed. Default 0: no limit.</summary>
    public int TimeoutSeconds { get; set; }
}

/// <summary>Resolves, validates and stores run policies.</summary>
public static class ScheduledTaskRunPolicies
{
    public static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMinutes(1);

    public const int MaxRetries = 10;

    public static readonly TimeSpan MinRetryDelay = TimeSpan.FromSeconds(1);

    public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromDays(1);

    public static readonly TimeSpan MinTimeout = TimeSpan.FromSeconds(1);

    public static readonly TimeSpan MaxTimeout = TimeSpan.FromDays(7);

    /// <summary>The job's own defaults, from its <see cref="ScheduledTaskRunPolicyAttribute"/>.</summary>
    public static EffectiveRunPolicy Defaults(Type jobType)
    {
        // The wrapper the package hands Umbraco isn't the job; callers pass the job's own type.
        if (jobType.GetCustomAttributes(typeof(ScheduledTaskRunPolicyAttribute), inherit: true).FirstOrDefault()
            is not ScheduledTaskRunPolicyAttribute attribute)
        {
            return EffectiveRunPolicy.None;
        }

        return new EffectiveRunPolicy(
            Math.Clamp(attribute.Retries, 0, MaxRetries),
            Clamp(TimeSpan.FromSeconds(attribute.RetryDelaySeconds), MinRetryDelay, MaxRetryDelay),
            attribute.TimeoutSeconds > 0 ? Clamp(TimeSpan.FromSeconds(attribute.TimeoutSeconds), MinTimeout, MaxTimeout) : null);
    }

    /// <summary>The editor's values over the job's defaults.</summary>
    public static EffectiveRunPolicy Resolve(Type jobType, ScheduledTaskRunPolicy? overrides)
    {
        var defaults = Defaults(jobType);
        if (overrides is null) return defaults;

        return new EffectiveRunPolicy(
            overrides.Retries ?? defaults.Retries,
            overrides.RetryDelay ?? defaults.RetryDelay,
            overrides.Timeout is { } timeout ? timeout > TimeSpan.Zero ? timeout : null : defaults.Timeout);
    }

    /// <summary>Why the values can't be saved, or null when they can.</summary>
    public static string? Validate(ScheduledTaskRunPolicy policy)
    {
        if (policy.Retries is < 0 or > MaxRetries)
            return $"Retries must be between 0 and {MaxRetries}.";
        if (policy.RetryDelay is { } delay && (delay < MinRetryDelay || delay > MaxRetryDelay))
            return "The wait before a retry must be between 1 second and 1 day.";
        if (policy.Timeout is { } timeout && timeout != TimeSpan.Zero && (timeout < MinTimeout || timeout > MaxTimeout))
            return "The time limit must be between 1 second and 7 days.";
        return null;
    }

    /// <summary>"3 retries, 1 minute apart · time limit 10 minutes", for the change log.</summary>
    internal static string Describe(EffectiveRunPolicy policy)
    {
        var retries = policy.Retries switch
        {
            0 => "no retries",
            1 => $"1 retry after {ScheduledTaskService.DescribePeriod(policy.RetryDelay)}",
            _ => $"{policy.Retries} retries, {ScheduledTaskService.DescribePeriod(policy.RetryDelay)} apart",
        };
        var limit = policy.Timeout is { } timeout ? $"time limit {ScheduledTaskService.DescribePeriod(timeout)}" : "no time limit";
        return $"{retries} · {limit}";
    }

    private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max) => value < min ? min : value > max ? max : value;

    /// <summary>
    /// Seconds from a request or file as a TimeSpan: null for NaN or infinity, and values beyond
    /// TimeSpan's range land on its ends (which <see cref="Validate"/> then refuses with a message)
    /// instead of overflowing.
    /// </summary>
    internal static TimeSpan? SecondsToTimeSpan(double? seconds)
    {
        if (seconds is not { } value || !double.IsFinite(value)) return null;
        if (value >= TimeSpan.MaxValue.TotalSeconds - 1) return TimeSpan.MaxValue;
        if (value <= TimeSpan.MinValue.TotalSeconds + 1) return TimeSpan.MinValue;
        return TimeSpan.FromMilliseconds(Math.Round(value * 1000));
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal static string? Serialize(ScheduledTaskRunPolicy? policy)
        => policy is null || policy.IsEmpty
            ? null
            : JsonSerializer.Serialize(
                new StoredPolicy(policy.Retries, policy.RetryDelay?.TotalSeconds, policy.Timeout?.TotalSeconds),
                Json);

    /// <summary>The stored policy, or null when there is none or it can't be read (the defaults apply).</summary>
    internal static ScheduledTaskRunPolicy? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var stored = JsonSerializer.Deserialize<StoredPolicy>(json, Json);
            if (stored is null) return null;
            var policy = new ScheduledTaskRunPolicy(stored.Retries, SecondsToTimeSpan(stored.RetryDelaySeconds), SecondsToTimeSpan(stored.TimeoutSeconds));
            return policy.IsEmpty || Validate(policy) is not null ? null : policy;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record StoredPolicy(int? Retries, double? RetryDelaySeconds, double? TimeoutSeconds);
}
