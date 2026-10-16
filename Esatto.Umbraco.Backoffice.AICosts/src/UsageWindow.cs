namespace Esatto.Umbraco.Backoffice.AICosts;

/// <summary>
/// Which Umbraco.AI statistics cover a time range. Umbraco.AI rolls hourly statistics into daily ones
/// only for completed days (up to an hour after midnight UTC) and keeps hourly statistics ~30 days, so:
/// <b>Daily</b> before yesterday, <b>Hourly</b> for yesterday and today. Anchored to <i>now</i>, not to
/// the range end, so an older range (the previous period) is read from Daily only.
/// </summary>
public sealed record UsageWindow(DateTime From, DateTime To, DateTime HourlyFrom)
{
    public static UsageWindow Create(DateTime from, DateTime to, DateTime utcNow)
    {
        var yesterday = DateTime.SpecifyKind(utcNow.Date.AddDays(-1), DateTimeKind.Utc);
        var hourlyFrom = yesterday < from ? from : yesterday > to ? to : yesterday;
        return new UsageWindow(from, to, hourlyFrom);
    }

    public bool HasDaily => From < HourlyFrom;
    public bool HasHourly => HourlyFrom < To;
}
