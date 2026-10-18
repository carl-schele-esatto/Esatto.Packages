using System.Globalization;
using System.Text.Json.Nodes;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

public sealed record ScheduledTasksOverviewDto(
    string ServerName,
    string ServerRole,
    int RunHistoryLimit,
    IReadOnlyList<ScheduledTaskDto> Tasks);

/// <summary>
/// A task as the dashboard sees it. Periods are in seconds; null means "never on its own"
/// (an infinite period or delay: the task only runs when triggered).
/// <c>CalendarUnavailableReason</c>: why the task can't use a calendar schedule (null when it can);
/// <c>MinimumPeriodSeconds</c>: the shortest interval the server accepts for it (besides its own period).
/// </summary>
public sealed record ScheduledTaskDto(
    string Id,
    string Name,
    string TypeName,
    string? AssemblyName,
    string Kind,
    bool IsUmbraco,
    double? PeriodSeconds,
    double? DefaultPeriodSeconds,
    bool HasPeriodOverride,
    double? DelaySeconds,
    IReadOnlyList<string> ServerRoles,
    bool RunsOnThisServer,
    bool IsManaged,
    bool IsPaused,
    bool IsRunning,
    DateTime? RunningSinceUtc,
    bool IsPossiblyStuck,
    DateTime? NextRunUtc,
    DateTime? LastRunUtc,
    ScheduledTaskRunDto? LastRun,
    int RunCount,
    int FailureCount,
    DateTime? SettingsUpdatedUtc,
    string? SettingsUpdatedBy,
    int PropertyCount,
    bool HasCustomProperties,
    ScheduledTaskScheduleDto? Schedule,
    double? Progress,
    string? ProgressMessage,
    string? CalendarUnavailableReason = null,
    double? MinimumPeriodSeconds = null);

/// <summary>
/// A calendar schedule: <c>kind</c> daily | weekly | monthly | yearly | cron; <c>time</c> "HH:mm";
/// <c>weekdays</c> e.g. ["Monday"]; <c>dayOfMonth</c> 1–31; <c>month</c> 1–12; <c>cron</c> five
/// fields; <c>timeZone</c> an IANA id such as "Europe/Stockholm".
/// </summary>
public sealed record ScheduledTaskScheduleDto(
    string Kind,
    string? Time,
    IReadOnlyList<string>? Weekdays,
    int? DayOfMonth,
    int? Month,
    string? Cron,
    string? TimeZone);

/// <summary>A calendar schedule for the task; null goes back to an interval.</summary>
public sealed record SetScheduledTaskScheduleRequest(string Id, ScheduledTaskScheduleDto? Schedule);

public sealed record PreviewScheduledTaskScheduleRequest(ScheduledTaskScheduleDto Schedule, int? Count);

public sealed record ScheduledTaskSchedulePreviewDto(IReadOnlyList<DateTime> NextRunsUtc, string? Error);

/// <summary>A property value as the property editor UI emits it (the Umbraco <c>{ alias, value }</c> shape).</summary>
public sealed record ScheduledTaskPropertyValueDto(string Alias, JsonNode? Value);

/// <summary>
/// A field of a task's properties form, in the shape Umbraco's own property settings use
/// (<c>PropertyEditorSettingsProperty</c>), ready for <c>&lt;umb-property&gt;</c>.
/// </summary>
public sealed record ScheduledTaskPropertyDefinitionDto(
    string Alias,
    string Label,
    string? Description,
    string Editor,
    string? PropertyEditorUiAlias,
    IReadOnlyList<ScheduledTaskPropertyConfig> Config,
    ScheduledTaskPropertyValidationDto Validation,
    string? Error);

public sealed record ScheduledTaskPropertyValidationDto(bool Mandatory);

public sealed record ScheduledTaskPropertiesDto(
    IReadOnlyList<ScheduledTaskPropertyDefinitionDto> Definitions,
    IReadOnlyList<ScheduledTaskPropertyValueDto> Values,
    bool IsCustomised,
    IReadOnlyList<string> Warnings);

/// <summary>New property values; null restores all defaults.</summary>
public sealed record SetScheduledTaskPropertiesRequest(string Id, IReadOnlyList<ScheduledTaskPropertyValueDto>? Values);

public sealed record ScheduledTaskRunDto(
    Guid Id,
    string Trigger,
    string? TriggeredBy,
    string Server,
    DateTime StartedUtc,
    DateTime? FinishedUtc,
    double? DurationMs,
    string Outcome,
    string? Message,
    string? Details,
    int Occurrences,
    DateTime LastOccurrenceUtc,
    double? Progress,
    string? ProgressMessage);

public sealed record ScheduledTaskIdRequest(string Id);

/// <summary>
/// <paramref name="RunningSinceUtc"/>: when the run the dashboard showed as stuck started; the reset
/// is refused if a different run is marked as running by now. Null resets whatever run is stuck.
/// </summary>
public sealed record ResetScheduledTaskRunRequest(string Id, DateTime? RunningSinceUtc = null);

/// <summary>New interval in seconds; null restores the task's own period.</summary>
public sealed record SetScheduledTaskPeriodRequest(string Id, double? PeriodSeconds);

public sealed record ScheduledTaskCommandResponse(string? Message, ScheduledTaskDto? Task);

internal static class ScheduledTaskDtoMapper
{
    public static ScheduledTaskDto ToDto(this ScheduledTaskInfo task) => new(
        task.Id,
        task.Name,
        task.TypeName,
        task.AssemblyName,
        task.Kind.ToString().ToLowerInvariant(),
        task.IsUmbraco,
        Seconds(task.Period),
        Seconds(task.DefaultPeriod),
        task.Setting?.PeriodOverride is not null,
        task.Delay is { } delay ? Seconds(delay) : null,
        task.ServerRoles,
        task.RunsOnThisServer,
        task.IsManaged,
        task.IsPaused,
        task.IsRunning,
        task.RunningSinceUtc,
        task.IsPossiblyStuck,
        task.NextRunUtc,
        task.LastRunUtc,
        task.Stats.Last?.ToDto(),
        task.Stats.Runs,
        task.Stats.Failures,
        task.Setting?.UpdatedUtc,
        task.Setting?.UpdatedBy,
        task.PropertyCount,
        task.Setting?.PropertiesJson is not null,
        task.Calendar?.ToDto(),
        task.Stats.Current?.Progress,
        task.Stats.Current?.ProgressMessage,
        task.CalendarUnavailableReason,
        task.MinimumPeriod is { } minimum ? Seconds(minimum) : null);

    public static ScheduledTaskScheduleDto ToDto(this ScheduledTaskCalendar calendar) => new(
        calendar.Kind.ToString().ToLowerInvariant(),
        calendar.Kind == ScheduledTaskCalendarKind.Cron ? null : calendar.Time.ToString("HH:mm", CultureInfo.InvariantCulture),
        calendar.Kind == ScheduledTaskCalendarKind.Weekly ? calendar.Weekdays.Select(d => d.ToString()).ToList() : null,
        calendar.Kind is ScheduledTaskCalendarKind.Monthly or ScheduledTaskCalendarKind.Yearly ? calendar.DayOfMonth : null,
        calendar.Kind == ScheduledTaskCalendarKind.Yearly ? calendar.Month : null,
        calendar.Kind == ScheduledTaskCalendarKind.Cron ? calendar.Cron : null,
        calendar.TimeZone);

    /// <summary>The calendar a request describes, or why it can't be read.</summary>
    public static ScheduledTaskCalendar? ToCalendar(this ScheduledTaskScheduleDto dto, out string? error)
    {
        error = null;
        if (ParseName<ScheduledTaskCalendarKind>(dto.Kind) is not { } kind)
        {
            error = $"Unknown schedule kind “{dto.Kind}”; use daily, weekly, monthly, yearly or cron.";
            return null;
        }

        var time = TimeOnly.MinValue;
        if (kind != ScheduledTaskCalendarKind.Cron
            && !TimeOnly.TryParseExact(dto.Time ?? string.Empty, ["HH:mm", "H:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
        {
            error = "Enter a time of day as HH:mm, e.g. 07:00.";
            return null;
        }

        var weekdays = new List<DayOfWeek>();
        foreach (var name in dto.Weekdays ?? [])
        {
            if (ParseName<DayOfWeek>(name) is not { } day)
            {
                error = $"Unknown weekday “{name}”; send one English day name per entry, e.g. [\"Monday\", \"Thursday\"].";
                return null;
            }

            weekdays.Add(day);
        }

        if (string.IsNullOrWhiteSpace(dto.TimeZone))
        {
            error = "Choose a time zone.";
            return null;
        }

        return new ScheduledTaskCalendar
        {
            Kind = kind,
            Time = new TimeOnly(time.Hour, time.Minute),
            Weekdays = weekdays,
            DayOfMonth = dto.DayOfMonth ?? 1,
            Month = dto.Month ?? 1,
            // Trimmed, so surrounding whitespace can't make the stored schedule outgrow its column.
            Cron = dto.Cron?.Trim(),
            TimeZone = dto.TimeZone.Trim(),
        };
    }

    public static ScheduledTaskPropertiesDto ToDto(this ScheduledTaskPropertiesForm form) => new(
        form.Definitions
            .Select(d => new ScheduledTaskPropertyDefinitionDto(
                d.Alias,
                d.Label,
                d.Description,
                d.Editor.ToString(),
                d.PropertyEditorUiAlias,
                d.Config,
                new ScheduledTaskPropertyValidationDto(d.Mandatory),
                d.Error))
            .ToList(),
        form.Values.Select(v => new ScheduledTaskPropertyValueDto(v.Key, v.Value)).ToList(),
        form.IsCustomised,
        form.Warnings);

    public static ScheduledTaskRunDto ToDto(this ScheduledTaskRun run) => new(
        run.Id,
        run.Trigger.ToString().ToLowerInvariant(),
        run.TriggeredBy,
        run.Server,
        run.StartedUtc,
        run.FinishedUtc,
        run.Duration?.TotalMilliseconds,
        run.Outcome.ToString().ToLowerInvariant(),
        run.Message,
        run.Details,
        run.Occurrences,
        run.LastOccurrenceUtc,
        run.Progress,
        run.ProgressMessage);

    private static double? Seconds(TimeSpan period)
        => period == Timeout.InfiniteTimeSpan ? null : period.TotalSeconds;

    // One exact name (any case). Enum.TryParse would also take "Monday,Tuesday" (OR-ing them into
    // Wednesday) and numbers.
    private static TEnum? ParseName<TEnum>(string? name) where TEnum : struct, Enum
        => Enum.GetNames<TEnum>().FirstOrDefault(n => n.Equals(name?.Trim(), StringComparison.OrdinalIgnoreCase)) is { } match
            ? Enum.Parse<TEnum>(match)
            : null;
}
