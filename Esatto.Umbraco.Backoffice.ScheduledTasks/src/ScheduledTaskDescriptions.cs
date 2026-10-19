using System.Globalization;
using System.Text.Json.Nodes;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>Plain-English descriptions of schedules and changes, for the change log.</summary>
internal static class ScheduledTaskDescriptions
{
    /// <summary>"Every 2 hours", "Every Monday and Thursday at 07:00 (Europe/Stockholm)", "On demand only".</summary>
    public static string Schedule(ScheduledTaskCalendar? calendar, TimeSpan period)
    {
        if (calendar is not null) return Calendar(calendar);
        return period == Timeout.InfiniteTimeSpan ? "On demand only" : $"Every {ScheduledTaskService.DescribePeriod(period)}";
    }

    public static string Calendar(ScheduledTaskCalendar calendar)
    {
        var at = $" at {calendar.Time.ToString("HH:mm", CultureInfo.InvariantCulture)}";
        var text = calendar.Kind switch
        {
            ScheduledTaskCalendarKind.Daily => $"Every day{at}",
            ScheduledTaskCalendarKind.Weekly => $"Every {Join(calendar.Weekdays.Distinct().OrderBy(d => ((int)d + 6) % 7).Select(d => d.ToString()))}{at}",
            ScheduledTaskCalendarKind.Monthly => $"Every month on day {calendar.DayOfMonth}{at}",
            ScheduledTaskCalendarKind.Yearly =>
                $"Every year on {calendar.DayOfMonth} {CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(Math.Clamp(calendar.Month, 1, 12))}{at}",
            _ => $"Cron {calendar.Cron}",
        };
        return $"{text} ({calendar.TimeZone})";
    }

    /// <summary>"Every hour → Every day at 07:00 (UTC)".</summary>
    public static string Change(string before, string after) => before == after ? after : $"{before} → {after}";

    /// <summary>The aliases whose stored value differs between two property JSON objects: "feed, timeout".</summary>
    public static string ChangedProperties(string? beforeJson, string? afterJson)
    {
        var before = Parse(beforeJson);
        var after = Parse(afterJson);
        var changed = before.Select(p => p.Key).Union(after.Select(p => p.Key), StringComparer.Ordinal)
            .Where(alias => !JsonNode.DeepEquals(before[alias], after[alias]))
            .Order(StringComparer.Ordinal)
            .ToList();
        return changed.Count == 0 ? "No values changed" : string.Join(", ", changed);
    }

    private static JsonObject Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonNode.Parse(json) as JsonObject ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    private static string Join(IEnumerable<string> items)
    {
        var list = items.ToList();
        return list.Count <= 1 ? string.Concat(list) : $"{string.Join(", ", list[..^1])} and {list[^1]}";
    }
}
