using System.Text.Json.Nodes;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// Every task's editor settings, as a file to move them to another environment (e.g. from staging
/// to production). Only tasks with settings are listed; the rest run on their defaults.
/// </summary>
public sealed record ScheduledTasksExport(
    string Format,
    int Version,
    DateTime ExportedUtc,
    string? ExportedFrom,
    IReadOnlyList<ScheduledTaskExportEntry> Tasks)
{
    public const string FormatName = "esatto-scheduled-tasks";
    public const int CurrentVersion = 1;
}

/// <summary>One task's settings in an export.</summary>
/// <param name="Name">For people reading the file; the <paramref name="Id"/> identifies the task.</param>
/// <param name="IntervalSeconds">An interval replacing the task's own; null = its own (or <paramref name="Schedule"/>).</param>
/// <param name="Schedule">A calendar schedule; null = runs on an interval.</param>
/// <param name="Properties">The property values that differ from their defaults, by alias.</param>
/// <param name="RunPolicy">Retries and time limit that differ from the task's defaults.</param>
public sealed record ScheduledTaskExportEntry(
    string Id,
    string? Name,
    bool Paused,
    double? IntervalSeconds,
    ScheduledTaskScheduleDto? Schedule,
    JsonObject? Properties,
    ScheduledTaskRunPolicyDto? RunPolicy);

/// <summary>Retries and time limit in seconds; <c>timeoutSeconds</c> 0 = no time limit; a null value = the task's default.</summary>
public sealed record ScheduledTaskRunPolicyDto(int? Retries, double? RetryDelaySeconds, double? TimeoutSeconds);

/// <summary>What an import does (or did) to one task.</summary>
/// <param name="Status">unchanged | changed | reset | unknownTask | invalid.</param>
/// <param name="Changes">What changes, e.g. "Schedule: Every hour → Every day at 07:00 (UTC)".</param>
/// <param name="Errors">Why the task's settings can't be used (then nothing is imported).</param>
public sealed record ScheduledTaskImportItem(
    string Id,
    string? Name,
    string Status,
    IReadOnlyList<string> Changes,
    IReadOnlyList<string> Errors);

/// <param name="Applied">Whether the settings were saved (false for a preview, or when something can't be used).</param>
/// <param name="Error">Why the file can't be imported at all.</param>
public sealed record ScheduledTasksImportResult(
    bool Applied,
    bool HasErrors,
    IReadOnlyList<ScheduledTaskImportItem> Tasks,
    string? Error = null);

internal sealed partial class ScheduledTaskService
{
    public ScheduledTasksExport Export()
    {
        var tasks = new List<ScheduledTaskExportEntry>();
        foreach (var (id, setting) in _settings.GetAll().OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            // Settings of a task that's no longer registered stay behind; there's nothing to move.
            if (FindJob(id) is not { } job || setting.State.IsDefault) continue;

            tasks.Add(new ScheduledTaskExportEntry(
                id,
                TaskName(id, job),
                setting.IsPaused,
                setting.Calendar is null && setting.PeriodOverride is { } period ? period.TotalSeconds : null,
                setting.Calendar?.ToDto(),
                string.IsNullOrWhiteSpace(setting.PropertiesJson) ? null : JsonNode.Parse(setting.PropertiesJson) as JsonObject,
                setting.RunPolicy is { } policy
                    ? new ScheduledTaskRunPolicyDto(policy.Retries, policy.RetryDelay?.TotalSeconds, policy.Timeout?.TotalSeconds)
                    : null));
        }

        return new ScheduledTasksExport(
            ScheduledTasksExport.FormatName, ScheduledTasksExport.CurrentVersion, DateTime.UtcNow, Environment.MachineName, tasks);
    }

    public ScheduledTasksImportResult Import(ScheduledTasksExport document, bool apply, bool resetMissing, string? updatedBy)
    {
        if (!string.Equals(document.Format, ScheduledTasksExport.FormatName, StringComparison.Ordinal))
            return new ScheduledTasksImportResult(false, true, [], "This file isn't a Scheduled Tasks export.");
        if (document.Version > ScheduledTasksExport.CurrentVersion)
            return new ScheduledTasksImportResult(false, true, [], "This file was exported by a newer version of Scheduled Tasks; update the package here first.");

        var current = _settings.GetAll();
        var items = new List<ScheduledTaskImportItem>();
        var plans = new List<ImportPlan>();
        var listed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in document.Tasks ?? [])
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.Id)) continue;
            if (!listed.Add(entry.Id))
            {
                items.Add(new ScheduledTaskImportItem(entry.Id, entry.Name, "invalid", [], ["The task is listed more than once."]));
                continue;
            }

            if (FindJob(entry.Id) is not { } job)
            {
                items.Add(new ScheduledTaskImportItem(entry.Id, entry.Name, "unknownTask", [], []));
                continue;
            }

            var setting = current.GetValueOrDefault(entry.Id);
            var (target, notes, errors) = ImportTarget(entry, job, setting);
            if (errors.Count > 0)
            {
                items.Add(new ScheduledTaskImportItem(entry.Id, TaskName(entry.Id, job), "invalid", [], errors));
                continue;
            }

            var differences = Differences(job, setting, target!);
            var changed = differences.Count > 0;
            var changes = differences.Concat(notes).ToList();
            items.Add(new ScheduledTaskImportItem(entry.Id, TaskName(entry.Id, job), changed ? "changed" : "unchanged", changes, []));
            if (changed) plans.Add(new ImportPlan(entry.Id, job, target!, changes));
        }

        if (resetMissing)
        {
            foreach (var (id, setting) in current.OrderBy(s => s.Key, StringComparer.Ordinal))
            {
                if (listed.Contains(id) || FindJob(id) is not { } job || setting.State.IsDefault) continue;
                var changes = Differences(job, setting, ScheduledTaskSettingState.Default);
                items.Add(new ScheduledTaskImportItem(id, TaskName(id, job), "reset", changes, []));
                plans.Add(new ImportPlan(id, job, ScheduledTaskSettingState.Default, changes));
            }
        }

        var hasErrors = items.Any(i => i.Status == "invalid");
        if (!apply || hasErrors) return new ScheduledTasksImportResult(false, hasErrors, items);

        foreach (var plan in plans)
        {
            _settings.Update(plan.Id, _ => plan.Target, updatedBy);
            ApplySchedule(plan.Id, plan.Target.PeriodOverride ?? JobPeriod(plan.Job));
            Audit(plan.Id, ScheduledTaskAuditActions.Imported, updatedBy, string.Join("; ", plan.Changes));
        }

        return new ScheduledTasksImportResult(true, false, items);
    }

    /// <summary>The settings an export entry asks for, notes on what's ignored, or why it can't be used.</summary>
    private (ScheduledTaskSettingState? Target, List<string> Notes, List<string> Errors) ImportTarget(
        ScheduledTaskExportEntry entry,
        object job,
        ScheduledTaskSetting? setting)
    {
        var errors = new List<string>();
        var notes = new List<string>();
        var options = _options.CurrentValue;
        var defaultPeriod = JobPeriod(job);

        TimeSpan? periodOverride = null;
        ScheduledTaskCalendar? calendar = null;
        if (entry.Schedule is { } schedule)
        {
            if (entry.IntervalSeconds is not null) errors.Add("It has both an interval and a calendar schedule; keep one.");
            if (schedule.ToCalendar(out var scheduleError) is not { } parsed)
            {
                errors.Add(scheduleError ?? "The calendar schedule can't be read.");
            }
            else if (job is IDistributedBackgroundJob distributed && DistributedJobClock.IsAlignedToClock(distributed))
            {
                errors.Add(AlignedToClockReason);
            }
            else if (_calendarEvaluator.Validate(parsed, DateTime.UtcNow) is { } invalid)
            {
                errors.Add(invalid);
            }
            else
            {
                // The same schedule as now keeps its bookkeeping (no occurrence runs twice or is lost).
                calendar = setting?.Calendar is { } existing && SameCalendar(existing, parsed) ? existing : parsed with { SavedUtc = DateTime.UtcNow };
            }
        }
        else if (entry.IntervalSeconds is { } seconds)
        {
            if (!double.IsFinite(seconds) || seconds <= 0)
            {
                errors.Add("The interval must be a positive number of seconds.");
            }
            else
            {
                var period = seconds >= TimeSpan.MaxValue.TotalSeconds - 1 ? TimeSpan.MaxValue : TimeSpan.FromMilliseconds(Math.Round(seconds * 1000));
                var minimum = job is IDistributedBackgroundJob ? DistributedMinimumPeriod() : options.MinimumPeriod;
                if (period != defaultPeriod && (period < minimum || period > options.MaximumPeriod))
                    errors.Add($"The interval must be between {DescribePeriod(minimum)} and {DescribePeriod(options.MaximumPeriod)}.");
                else if (period != defaultPeriod)
                    periodOverride = period;
            }
        }

        string? propertiesJson = null;
        if (entry.Properties is { Count: > 0 } properties)
        {
            if (ScheduledTaskPropertyValues.PropertiesTypeOf(job) is not { } propertiesType)
            {
                notes.Add("Ignored its properties: the task has none here.");
            }
            else
            {
                var known = ScheduledTaskPropertySchema.For(propertiesType).Select(d => d.Alias).ToHashSet(StringComparer.Ordinal);
                foreach (var alias in properties.Select(p => p.Key).Where(a => !known.Contains(a)))
                    notes.Add($"Ignored property “{alias}”: the task doesn't have it here.");

                var values = properties.ToDictionary(p => p.Key, p => p.Value?.DeepClone(), StringComparer.Ordinal);
                var (json, propertyErrors) = _propertyValues.Prepare(propertiesType, values);
                if (propertyErrors is not null) errors.AddRange(propertyErrors.SelectMany(e => e.Value.Select(m => $"{e.Key}: {m}")));
                else propertiesJson = json;
            }
        }

        ScheduledTaskRunPolicy? runPolicy = null;
        if (entry.RunPolicy is { } policyDto)
        {
            var policy = new ScheduledTaskRunPolicy(
                policyDto.Retries,
                ScheduledTaskRunPolicies.SecondsToTimeSpan(policyDto.RetryDelaySeconds),
                ScheduledTaskRunPolicies.SecondsToTimeSpan(policyDto.TimeoutSeconds));
            if (ScheduledTaskRunPolicies.Validate(policy) is { } policyError) errors.Add(policyError);
            else runPolicy = WithoutDefaults(policy, ScheduledTaskRunPolicies.Defaults(job.GetType()));
        }

        if (errors.Count > 0) return (null, notes, errors);
        return (new ScheduledTaskSettingState(entry.Paused, periodOverride, propertiesJson, calendar) { RunPolicy = runPolicy }, notes, errors);
    }

    /// <summary>What applying <paramref name="target"/> changes, in words; empty when nothing does.</summary>
    private static List<string> Differences(object job, ScheduledTaskSetting? setting, ScheduledTaskSettingState target)
    {
        var changes = new List<string>();
        var current = setting?.State ?? ScheduledTaskSettingState.Default;
        var defaultPeriod = JobPeriod(job);

        if (current.IsPaused != target.IsPaused) changes.Add(target.IsPaused ? "Paused" : "Resumed");

        var scheduleBefore = ScheduledTaskDescriptions.Schedule(current.Calendar, current.PeriodOverride ?? defaultPeriod);
        var scheduleAfter = ScheduledTaskDescriptions.Schedule(target.Calendar, target.PeriodOverride ?? defaultPeriod);
        if (scheduleBefore != scheduleAfter) changes.Add($"Schedule: {scheduleBefore} → {scheduleAfter}");

        if (!SameJson(current.PropertiesJson, target.PropertiesJson))
            changes.Add(target.PropertiesJson is null ? "Properties: back to defaults" : $"Properties: {ScheduledTaskDescriptions.ChangedProperties(current.PropertiesJson, target.PropertiesJson)}");

        var policyBefore = ScheduledTaskRunPolicies.Describe(ScheduledTaskRunPolicies.Resolve(job.GetType(), current.RunPolicy));
        var policyAfter = ScheduledTaskRunPolicies.Describe(ScheduledTaskRunPolicies.Resolve(job.GetType(), target.RunPolicy));
        if (policyBefore != policyAfter) changes.Add($"Retries and time limit: {policyBefore} → {policyAfter}");

        return changes;
    }

    private static bool SameJson(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return string.IsNullOrWhiteSpace(a) == string.IsNullOrWhiteSpace(b);
        try
        {
            return JsonNode.DeepEquals(JsonNode.Parse(a), JsonNode.Parse(b));
        }
        catch (System.Text.Json.JsonException)
        {
            return a == b;
        }
    }

    private static bool SameCalendar(ScheduledTaskCalendar a, ScheduledTaskCalendar b)
        => a.ToCronExpression() == b.ToCronExpression() && string.Equals(a.TimeZone, b.TimeZone, StringComparison.OrdinalIgnoreCase);

    private static TimeSpan JobPeriod(object job) => job switch
    {
        IRecurringBackgroundJob recurring => recurring.Period,
        IDistributedBackgroundJob distributed => distributed.Period,
        _ => Timeout.InfiniteTimeSpan,
    };

    private static string TaskName(string id, object job)
        => ScheduledTaskIdentity.Humanize(ScheduledTaskIdentity.IsDistributed(id) ? id[ScheduledTaskIdentity.DistributedPrefix.Length..] : job.GetType().Name);

    private sealed record ImportPlan(string Id, object Job, ScheduledTaskSettingState Target, IReadOnlyList<string> Changes);
}
