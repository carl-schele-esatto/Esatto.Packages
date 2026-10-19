using System.Globalization;
using Umbraco.Cms.Core.Configuration;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

public enum ScheduledTaskCalendarKind
{
    /// <summary>Every day at <see cref="ScheduledTaskCalendar.Time"/>.</summary>
    Daily,

    /// <summary>On each of <see cref="ScheduledTaskCalendar.Weekdays"/> at <see cref="ScheduledTaskCalendar.Time"/>.</summary>
    Weekly,

    /// <summary>On <see cref="ScheduledTaskCalendar.DayOfMonth"/> every month at <see cref="ScheduledTaskCalendar.Time"/>.</summary>
    Monthly,

    /// <summary>On <see cref="ScheduledTaskCalendar.DayOfMonth"/> <see cref="ScheduledTaskCalendar.Month"/> every year at <see cref="ScheduledTaskCalendar.Time"/>.</summary>
    Yearly,

    /// <summary>A five-field cron expression (<see cref="ScheduledTaskCalendar.Cron"/>).</summary>
    Cron,
}

/// <summary>
/// A calendar schedule, e.g. "every Monday at 07:00" or "every year on 15 March at 09:00": the
/// task runs at those wall-clock times in <see cref="TimeZone"/>, instead of every N minutes.
/// </summary>
public sealed record ScheduledTaskCalendar
{
    public ScheduledTaskCalendarKind Kind { get; init; }

    /// <summary>Time of day for every kind but <see cref="ScheduledTaskCalendarKind.Cron"/> (seconds are ignored).</summary>
    public TimeOnly Time { get; init; }

    /// <summary>The days a <see cref="ScheduledTaskCalendarKind.Weekly"/> schedule runs on.</summary>
    public IReadOnlyList<DayOfWeek> Weekdays { get; init; } = [];

    /// <summary>1–31, for <see cref="ScheduledTaskCalendarKind.Monthly"/> and <see cref="ScheduledTaskCalendarKind.Yearly"/>. Months without that day are skipped.</summary>
    public int DayOfMonth { get; init; } = 1;

    /// <summary>1–12, for <see cref="ScheduledTaskCalendarKind.Yearly"/>.</summary>
    public int Month { get; init; } = 1;

    /// <summary>minute hour day-of-month month day-of-week, for <see cref="ScheduledTaskCalendarKind.Cron"/>.</summary>
    public string? Cron { get; init; }

    /// <summary>IANA (e.g. Europe/Stockholm) or Windows time zone id the times are in.</summary>
    public string TimeZone { get; init; } = "UTC";

    /// <summary>
    /// When the schedule was saved. Occurrences before this don't count, so changing a schedule
    /// doesn't trigger a catch-up run for a time that has already passed.
    /// </summary>
    public DateTime SavedUtc { get; init; }

    /// <summary>The schedule as a five-field cron expression (local times in <see cref="TimeZone"/>).</summary>
    public string ToCronExpression() => Kind switch
    {
        ScheduledTaskCalendarKind.Daily => $"{Time.Minute} {Time.Hour} * * *",
        ScheduledTaskCalendarKind.Weekly =>
            $"{Time.Minute} {Time.Hour} * * {string.Join(',', Weekdays.Distinct().OrderBy(d => (int)d).Select(d => (int)d))}",
        ScheduledTaskCalendarKind.Monthly => $"{Time.Minute} {Time.Hour} {DayOfMonth} * *",
        ScheduledTaskCalendarKind.Yearly => $"{Time.Minute} {Time.Hour} {DayOfMonth} {Month} *",
        _ => (Cron ?? string.Empty).Trim(),
    };
}

/// <summary>
/// Works out when a <see cref="ScheduledTaskCalendar"/> runs, using Umbraco's own cron parser
/// (<see cref="ICronTabParser"/>) on wall-clock times in the schedule's time zone.
/// </summary>
/// <remarks>
/// <para>
/// Daylight saving: a time that doesn't exist (the hour skipped in spring) runs as soon as the
/// clock resumes; a time that occurs twice (the hour repeated in autumn) runs once, the first time.
/// The second instance is never returned, whatever the baseline, so a run that ends inside the
/// repeated hour doesn't make the same wall-clock time due again.
/// </para>
/// <para>
/// Cron: when both the day-of-month and the day-of-week field are restricted (neither starts with
/// <c>*</c>), a day matches when either does, as in standard cron (<c>0 9 1 * 1</c> = the 1st of
/// every month and every Monday). Umbraco's parser would require both.
/// </para>
/// </remarks>
public sealed class ScheduledTaskCalendarEvaluator
{
    /// <summary>Longest cron expression accepted, so a schedule always fits its database column.</summary>
    public const int MaxCronLength = 200;

    // A schedule that can't run within this window is treated as never running (e.g. 31 February).
    private static readonly TimeSpan Horizon = TimeSpan.FromDays(366 * 5);

    // Enough rounds to skip every minute of a repeated hour (or two) whose first instance has passed.
    private const int MaxSkippedCandidates = 24 * 60;

    // Zones newer than the server's time zone data (ICU on Windows, tzdata elsewhere) that browsers
    // already offer, mapped to an older zone with the same clock times.
    private static readonly IReadOnlyDictionary<string, string> ZoneAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["America/Ciudad_Juarez"] = "America/Denver",
        ["America/Coyhaique"] = "America/Punta_Arenas",
        ["Antarctica/Vostok"] = "Asia/Tashkent",
        ["Asia/Urumqi"] = "Asia/Bishkek",
        ["Etc/Zulu"] = "UTC",
        ["Z"] = "UTC",
    };

    private readonly ICronTabParser _parser;

    public ScheduledTaskCalendarEvaluator(ICronTabParser parser) => _parser = parser;

    /// <summary>Why the schedule can't be used, or null when it can.</summary>
    public string? Validate(ScheduledTaskCalendar calendar, DateTime utcNow)
    {
        switch (calendar.Kind)
        {
            case ScheduledTaskCalendarKind.Weekly when calendar.Weekdays.Count == 0:
                return "Choose at least one weekday.";
            case ScheduledTaskCalendarKind.Monthly when calendar.DayOfMonth is < 1 or > 31:
                return "The day of the month must be between 1 and 31.";
            case ScheduledTaskCalendarKind.Yearly when calendar.Month is < 1 or > 12:
                return "The month must be between 1 and 12.";
            case ScheduledTaskCalendarKind.Yearly when calendar.DayOfMonth < 1 || calendar.DayOfMonth > DateTime.DaysInMonth(2024, calendar.Month):
                return $"{CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(calendar.Month)} has no day {calendar.DayOfMonth}.";
            case ScheduledTaskCalendarKind.Cron when string.IsNullOrWhiteSpace(calendar.Cron):
                return "Enter a cron expression.";
            case ScheduledTaskCalendarKind.Cron when calendar.Cron!.Trim().Length > MaxCronLength:
                return $"The cron expression can be at most {MaxCronLength} characters long.";
            case ScheduledTaskCalendarKind.Cron when Fields(calendar.Cron!).Length != 5 || !IsValidCron(calendar.Cron!.Trim()):
                return "Enter a valid five-field cron expression: minute hour day-of-month month day-of-week (Sunday = 0).";
        }

        if (FindZone(calendar.TimeZone) is null)
            return $"Unknown time zone “{calendar.TimeZone}”: this server can't convert times in it. Choose a zone with the same clock times, or UTC.";
        if (NextOccurrenceUtc(calendar, utcNow) is null) return "This schedule doesn't run in the next five years.";
        return null;
    }

    /// <summary>The first occurrence strictly after <paramref name="afterUtc"/>, or null if there is none within five years.</summary>
    public DateTime? NextOccurrenceUtc(ScheduledTaskCalendar calendar, DateTime afterUtc)
    {
        if (FindZone(calendar.TimeZone) is not { } zone) return null;

        var after = DateTime.SpecifyKind(afterUtc, DateTimeKind.Utc);
        var start = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(after, zone), DateTimeKind.Unspecified);
        var expressions = CronExpressions(calendar);
        var localBase = start;

        // A candidate is skipped (and the next one tried) only when its wall time was already passed:
        // one in a repeated hour whose first instance is at or before 'after'.
        for (var round = 0; round <= MaxSkippedCandidates; round++)
        {
            if (NextLocal(expressions, localBase) is not { } local || local - start > Horizon) return null;

            var utc = ToUtc(local, zone);
            if (utc > after) return utc;

            localBase = local;
        }

        return null;
    }

    /// <summary>
    /// The latest occurrence after <paramref name="afterUtc"/> and at or before
    /// <paramref name="untilUtc"/>, or null when there is none: the occurrence a run at
    /// <paramref name="untilUtc"/> serves when several were missed (e.g. while the site was down).
    /// </summary>
    public DateTime? LatestOccurrenceUtc(ScheduledTaskCalendar calendar, DateTime afterUtc, DateTime untilUtc)
    {
        var after = DateTime.SpecifyKind(afterUtc, DateTimeKind.Utc);
        var until = DateTime.SpecifyKind(untilUtc, DateTimeKind.Utc);
        if (until <= after) return null;

        // Look back over a growing window, so a frequent schedule needs few steps.
        foreach (var window in LookBackWindows)
        {
            var from = until - after > window ? until - window : after;
            DateTime? latest = null;
            var cursor = from;
            // Every occurrence in the window is visited (the window is bounded and the cursor only moves
            // forward); stopping early would return an earlier one, making the rest due again.
            while (NextOccurrenceUtc(calendar, cursor) is { } next && next <= until)
            {
                latest = next;
                cursor = next;
            }

            if (latest is not null || from == after) return latest;
        }

        return null;
    }

    private static readonly TimeSpan[] LookBackWindows =
        [TimeSpan.FromDays(1), TimeSpan.FromDays(32), TimeSpan.FromDays(400), Horizon + TimeSpan.FromDays(366)];

    /// <summary>
    /// The period that makes Umbraco's distributed-job poller (due when <c>LastRun + Period</c> has
    /// passed) run the job at the calendar's next occurrence after <paramref name="lastRunUtc"/>
    /// (or after the schedule was saved, whichever is later). Ten years when it never runs again.
    /// </summary>
    public TimeSpan PeriodUntilNextOccurrence(ScheduledTaskCalendar calendar, DateTime lastRunUtc)
    {
        var lastRun = DateTime.SpecifyKind(lastRunUtc, DateTimeKind.Utc);
        var baseline = lastRun > calendar.SavedUtc ? lastRun : calendar.SavedUtc;
        return NextOccurrenceUtc(calendar, baseline) is { } next ? next - lastRun : TimeSpan.FromDays(3650);
    }

    /// <summary>The next <paramref name="count"/> occurrences after <paramref name="afterUtc"/>.</summary>
    public IReadOnlyList<DateTime> NextOccurrencesUtc(ScheduledTaskCalendar calendar, DateTime afterUtc, int count)
    {
        var occurrences = new List<DateTime>(count);
        var cursor = afterUtc;
        while (occurrences.Count < count && NextOccurrenceUtc(calendar, cursor) is { } next)
        {
            occurrences.Add(next);
            cursor = next;
        }

        return occurrences;
    }

    /// <summary>
    /// The time zone for an IANA or Windows id, or null when unknown. Zones the server's time zone
    /// data doesn't have yet resolve to an older zone with the same clock times.
    /// </summary>
    public static TimeZoneInfo? FindZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        id = id.Trim();
        return TryFindZone(id) ?? (ZoneAliases.TryGetValue(id, out var alias) ? TryFindZone(alias) : null);
    }

    private static TimeZoneInfo? TryFindZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }

    private static string[] Fields(string cron) => cron.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

    // Umbraco's parser throws on numbers too large for an Int32 (OverflowException) rather than
    // reporting them as invalid.
    private bool IsValidCron(string expression)
    {
        try
        {
            return CronExpressions(expression).All(_parser.IsValidCronTab);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static IReadOnlyList<string> CronExpressions(ScheduledTaskCalendar calendar)
        => calendar.Kind == ScheduledTaskCalendarKind.Cron ? CronExpressions(calendar.Cron ?? string.Empty) : [calendar.ToCronExpression()];

    /// <summary>
    /// The expression(s) whose earliest match is the schedule's: standard cron matches a day on
    /// day-of-month OR day-of-week when both are restricted, which is two expressions for Umbraco's
    /// parser (which would require both).
    /// </summary>
    internal static IReadOnlyList<string> CronExpressions(string cron)
    {
        var fields = Fields(cron);
        if (fields.Length != 5 || fields[2].StartsWith('*') || fields[4].StartsWith('*')) return [string.Join(' ', fields)];

        return
        [
            string.Join(' ', fields[0], fields[1], fields[2], fields[3], "*"),
            string.Join(' ', fields[0], fields[1], "*", fields[3], fields[4]),
        ];
    }

    /// <summary>The earliest local wall time after <paramref name="localBase"/> any of the expressions match, or null.</summary>
    private DateTime? NextLocal(IReadOnlyList<string> expressions, DateTime localBase)
    {
        DateTime? earliest = null;
        foreach (var expression in expressions)
        {
            DateTime local;
            try
            {
                local = _parser.GetNextOccurrence(expression, localBase);
            }
            catch (Exception)
            {
                // Validated expressions parse; anything else (e.g. a stored value edited by hand) never runs.
                return null;
            }

            if (local != DateTime.MaxValue && (earliest is null || local < earliest)) earliest = local;
        }

        return earliest;
    }

    /// <summary>
    /// The UTC instant a local wall-clock time runs at: when the clock resumes if it doesn't exist
    /// (spring), the first instance if it occurs twice (autumn).
    /// </summary>
    private static DateTime ToUtc(DateTime local, TimeZoneInfo zone)
    {
        var guard = 0;
        while (zone.IsInvalidTime(local) && guard++ < 24 * 60) local = local.AddMinutes(1);

        // The larger offset is the earlier instant.
        return zone.IsAmbiguousTime(local)
            ? DateTime.SpecifyKind(local - zone.GetAmbiguousTimeOffsets(local).Max(), DateTimeKind.Utc)
            : TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }
}
