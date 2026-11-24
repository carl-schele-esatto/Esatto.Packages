using System.Globalization;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Google.Apis.AnalyticsData.v1beta.Data;

namespace Esatto.Umbraco.Backoffice.SeoReport.Google;

/// <summary>One day: the date and the number of sessions.</summary>
public record Ga4DailyPoint(DateOnly Date, long Sessions);

/// <summary>One source of a single day's sessions.</summary>
public record Ga4DailySource(string Channel, string Source, long Sessions);

/// <summary>
/// Which range of days the curve shows, and what it is compared with.
///
/// TWO MODES. Without a month: the rolling last thirty days, compared with the thirty days before. With a month: that calendar
/// month, compared with the month before. The second mode lets someone go back and look at a campaign that already ran; without
/// it the curve would only reach one month back.
///
/// The server sends no label: the client builds the heading from <paramref name="Month"/> (null means the rolling window) and the dates.
/// </summary>
/// <param name="Month"><c>YYYY-MM</c> in the month mode, null in the rolling mode.</param>
public record Ga4DailyWindow(DateOnly Start, DateOnly End, DateOnly PreviousStart, DateOnly PreviousEnd, string? Month);

/// <summary>
/// The daily curve plus what makes it readable without doing the maths by hand.
///
/// THE BASELINE AND THE PEAK ARE COMPUTED ON THE SERVER, not in the client: two places computing the same thing separately would
/// sooner or later give different answers.
/// </summary>
/// <param name="Points">One point per day of the window, in order; days without traffic are zero.</param>
/// <param name="Baseline">
/// Average sessions per day over the PRECEDING period of the same length. It is drawn as a band behind the curve instead of a
/// second curve: GA4's own dashed comparison line is exactly what makes its cards hard to read, two wobbling lines crossing each other.
/// </param>
/// <param name="Total">Sessions in the window.</param>
/// <param name="PreviousTotal">Sessions in the preceding period.</param>
/// <param name="ChangePercent">Change against the preceding period in percent. Null when the preceding period is zero.</param>
/// <param name="Peak">The day with the most sessions in the window. Null when the window is empty.</param>
public record Ga4DailySeries(
    IReadOnlyList<Ga4DailyPoint> Points,
    double Baseline,
    long Total,
    long PreviousTotal,
    double? ChangePercent,
    Ga4DailyPoint? Peak);

/// <summary>
/// Day-fresh traffic from GA4, to see whether a campaign brought people to the site.
///
/// A CLASS OF ITS OWN, NOT A METHOD ON <see cref="Ga4Agent"/>. Monthly data is archived once per month and does not change
/// afterwards; daily data is rolling, its last days are revised, and "the last 30 days" has no meaning once stored. Putting both in
/// one class would force rolling data into a shape built for immutable months.
///
/// SESSIONS, NOT EVENTS. GA4's own start page leads with "event count", which is useless to a person: an event is a page view, a
/// scroll, a click, anything. The curve shows sessions, the same measure as the rest of the report.
///
/// ONE CALL for the whole curve. GA4 does accept several date ranges per request, but then it adds a <c>dateRange</c> dimension to
/// every row and the parsing gets harder for no gain. Instead both periods are fetched as ONE continuous range and split by date in
/// <see cref="Split"/>.
/// </summary>
/// <param name="propertyId">The GA4 property, as <c>properties/123456789</c>.</param>
/// <param name="clients">Where the authorised Google services come from.</param>
public sealed class Ga4DailyAgent(string propertyId, IGoogleClients clients)
{
    /// <summary>The length of the window, and so also of the comparison period.</summary>
    public const int WindowDays = 30;

    /// <summary>
    /// How many days back count as provisional. GA4 keeps processing for about a day; two is a margin, so a finished figure is
    /// marked provisional rather than the other way round.
    /// </summary>
    public const int ProvisionalDays = 2;

    // 60 days (the window and its comparison period) fit with a wide margin.
    private const int MaxSeriesRows = 200;

    private const int MaxSourceRows = 10;

    /// <summary>
    /// Which range to show.
    ///
    /// YESTERDAY IS ALWAYS THE LAST DAY, in the rolling mode and as a ceiling in a month still running. Today is left out on
    /// purpose: half a day always looks like a collapse, and the curve's last point should not be an artefact of the time of day.
    /// A month that is blank or not <c>YYYY-MM</c> gives the rolling window.
    /// </summary>
    /// <param name="today">Today's date. Passed in so the curve can be tested.</param>
    /// <param name="month"><c>YYYY-MM</c> for a calendar month, null for the rolling thirty days.</param>
    public static Ga4DailyWindow WindowFor(DateOnly today, string? month)
    {
        var yesterday = today.AddDays(-1);

        if (!Period.TryParse(month, out var period))
        {
            var start = yesterday.AddDays(-WindowDays + 1);

            return new Ga4DailyWindow(
                Start: start,
                End: yesterday,
                PreviousStart: start.AddDays(-WindowDays),
                PreviousEnd: start.AddDays(-1),
                Month: null);
        }

        var first = new DateOnly(period.Year, period.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);

        // A running month ends yesterday, not on the 31st. Otherwise the curve runs into the future with a long tail of zero days,
        // which reads as a collapse in traffic.
        if (last > yesterday) last = yesterday;

        return new Ga4DailyWindow(
            Start: first,
            End: last,
            PreviousStart: first.AddMonths(-1),
            PreviousEnd: first.AddDays(-1),
            Month: period.ToString());
    }

    /// <summary>The daily curve for a window, with the preceding period as the comparison.</summary>
    /// <param name="today">Today's date. Passed in so the curve can be tested.</param>
    /// <param name="month"><c>YYYY-MM</c>, or null for the rolling thirty days.</param>
    public async Task<(Ga4DailySeries Series, Ga4DailyWindow Window)> FetchAsync(
        DateOnly today, string? month = null, CancellationToken ct = default)
    {
        Ga4Requests.RequirePropertyId(propertyId);

        var window = WindowFor(today, month);

        // ONE call for both periods: they lie side by side, so one continuous range is enough and is then split by date.
        var response = await Ga4Requests
            .RunAsync(clients, BuildSeriesRequest(propertyId, window.PreviousStart, window.End), ct)
            .ConfigureAwait(false);

        return (Split(MapPoints(response), window), window);
    }

    /// <summary>
    /// Where ONE day's sessions came from. Only called when someone clicks a day: that is the question that tells "the spike" from
    /// "the newsletter brought 400 visits".
    /// </summary>
    public async Task<IReadOnlyList<Ga4DailySource>> FetchSourcesAsync(DateOnly day, CancellationToken ct = default)
    {
        Ga4Requests.RequirePropertyId(propertyId);

        var response = await Ga4Requests
            .RunAsync(clients, BuildSourcesRequest(propertyId, day), ct)
            .ConfigureAwait(false);

        return MapSources(response);
    }

    // -------------------------------------------------------------------------
    // REQUESTS: static and pure, so their shape can be tested without calling Google.
    // -------------------------------------------------------------------------

    public static RunReportRequest BuildSeriesRequest(string propertyId, DateOnly start, DateOnly end) => new()
    {
        Property = propertyId,
        DateRanges = [Ga4Requests.Range(start, end)],
        Dimensions = [new Dimension { Name = "date" }], // the format is YYYYMMDD
        Metrics = [new Metric { Name = Ga4Requests.SessionsMetric }],
        Limit = MaxSeriesRows,
        OrderBys = [Ga4Requests.ByDimension("date")],
    };

    /// <summary>
    /// Channel group AND source. The channel group is the readable one ("Email", "Organic Social"); the source is the one that
    /// points out the campaign ("linkedin.com", the name in a utm_source). Either alone would leave the question half answered.
    /// </summary>
    public static RunReportRequest BuildSourcesRequest(string propertyId, DateOnly day) => new()
    {
        Property = propertyId,
        DateRanges = [Ga4Requests.Range(day, day)],
        Dimensions = [new Dimension { Name = Ga4Requests.ChannelGroupDimension }, new Dimension { Name = Ga4Requests.SourceDimension }],
        Metrics = [new Metric { Name = Ga4Requests.SessionsMetric }],
        Limit = MaxSourceRows,
        OrderBys = [new OrderBy { Metric = new MetricOrderBy { MetricName = Ga4Requests.SessionsMetric }, Desc = true }],
    };

    // -------------------------------------------------------------------------
    // INTERPRETATION: pure functions, testable without the API.
    // -------------------------------------------------------------------------

    /// <summary>
    /// The rows' dates and sessions. Rows with a date GA4 does not recognise are skipped rather than thrown on: an unexpected date
    /// format must not take the dashboard down.
    /// </summary>
    public static IReadOnlyList<Ga4DailyPoint> MapPoints(RunReportResponse response)
    {
        var points = new List<Ga4DailyPoint>();
        foreach (var row in Ga4Requests.RowsOf(response))
        {
            var raw = row.DimensionValues?.FirstOrDefault()?.Value;
            if (!DateOnly.TryParseExact(raw, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;

            points.Add(new Ga4DailyPoint(date, SessionsOf(row)));
        }

        return points;
    }

    /// <summary>The sources of a day, with the ones that have no sessions left out.</summary>
    public static IReadOnlyList<Ga4DailySource> MapSources(RunReportResponse response)
    {
        var sources = new List<Ga4DailySource>();
        foreach (var row in Ga4Requests.RowsOf(response))
        {
            var channel = row.DimensionValues?.ElementAtOrDefault(0)?.Value ?? "";
            var source = row.DimensionValues?.ElementAtOrDefault(1)?.Value ?? "";
            var sessions = SessionsOf(row);

            if (sessions > 0) sources.Add(new Ga4DailySource(channel, source, sessions));
        }

        return sources;
    }

    /// <summary>The row's first metric as a number; a missing or unreadable value counts as zero.</summary>
    private static long SessionsOf(Row row)
    {
        _ = long.TryParse(row.MetricValues?.FirstOrDefault()?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sessions);
        return sessions;
    }

    /// <summary>
    /// Splits the fetched range into the current and the preceding period, and works out what makes the curve readable.
    ///
    /// DAYS WITHOUT TRAFFIC ARE FILLED WITH ZERO. GA4 leaves rows out altogether when a day has no visits, and a curve that skips the
    /// day compresses the time axis: the spike ends up in the wrong place relative to when the campaign actually went out.
    /// </summary>
    public static Ga4DailySeries Split(IEnumerable<Ga4DailyPoint> points, Ga4DailyWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        // The last value wins if GA4 ever answered with the same date twice: ToDictionary would have thrown, and a duplicate must not take the curve down.
        var sessionsByDay = new Dictionary<DateOnly, long>();
        foreach (var point in points) sessionsByDay[point.Date] = point.Sessions;

        var current = Fill(sessionsByDay, window.Start, window.End);
        var previous = Fill(sessionsByDay, window.PreviousStart, window.PreviousEnd);

        var total = current.Sum(x => x.Sessions);
        var previousTotal = previous.Sum(x => x.Sessions);

        // No change to show when there is nothing to compare with: "infinitely many percent up from zero" means nothing.
        double? change = previousTotal > 0 ? ((double)total - previousTotal) / previousTotal * 100 : null;

        var peak = current.Count > 0 && total > 0 ? current.MaxBy(x => x.Sessions) : null;
        var baseline = previous.Count > 0 ? (double)previousTotal / previous.Count : 0;

        return new Ga4DailySeries(current, baseline, total, previousTotal, change, peak);
    }

    private static List<Ga4DailyPoint> Fill(Dictionary<DateOnly, long> sessionsByDay, DateOnly from, DateOnly to)
    {
        var days = new List<Ga4DailyPoint>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            days.Add(new Ga4DailyPoint(day, sessionsByDay.GetValueOrDefault(day)));
        }

        return days;
    }
}
