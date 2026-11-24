using System.Globalization;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Google.Apis.AnalyticsData.v1beta.Data;

namespace Esatto.Umbraco.Backoffice.SeoReport.Google;

/// <summary>
/// One row of raw GA4 data: a period (<c>YYYY-MM</c>), a segment and the number of sessions. That is all the fetch layer is
/// responsible for: no year-on-year and no interpretation here. The shape mirrors the Search Console row on purpose, so the
/// processing layer treats both sources alike: one row is one period, one segment and one set of measures.
/// </summary>
/// <param name="Period">The month, <c>YYYY-MM</c>.</param>
/// <param name="Segment">One of <see cref="Ga4Agent.SegmentOrganic"/>, <see cref="Ga4Agent.SegmentTotal"/>, <see cref="Ga4Agent.SegmentOrganicGoogle"/>.</param>
/// <param name="Sessions">Sessions in the month.</param>
/// <param name="EngagementRate">
/// A ratio, not a count, like <paramref name="AvgSessionSeconds"/>: ratios cope with a gap in the measurement much better than
/// the session count does, because they are computed over the sessions GA4 actually sees. Zero when the raw data is older than
/// the measure (a default on purpose).
/// </param>
/// <param name="AvgSessionSeconds">Average session duration in seconds; see <paramref name="EngagementRate"/>.</param>
/// <param name="KeyEvents">
/// Key events (conversions) in the month. ZERO MEANS TWO DIFFERENT THINGS, and the processing layer tells them apart: either no
/// key event is defined in GA4, or none happened. The number alone cannot say which, so the mapper stays silent about the KPI
/// when everything is zero and sets a note instead.
/// </param>
public record Ga4Row(
    string Period,
    string Segment,
    long Sessions,
    double EngagementRate = 0,
    double AvgSessionSeconds = 0,
    long KeyEvents = 0);

/// <summary>
/// The fetch layer against the GA4 Data API (Google's stable REST client). Two halves, kept apart on purpose:
/// <see cref="FetchAsync"/> is the transport and calls the API; <see cref="MapResponse"/> is the mapping, a pure function from
/// one response to <see cref="Ga4Row"/>s, which is what the tests exercise.
/// </summary>
/// <param name="propertyId">The GA4 property, as <c>properties/123456789</c>.</param>
/// <param name="clients">Where the authorised Google services come from.</param>
public sealed class Ga4Agent(string propertyId, IGoogleClients clients)
{
    /// <summary>
    /// How many whole calendar years of history are fetched, the report year included. Two: the report year and the year before.
    /// That gives a year-on-year comparison and nothing more, which is all the charts use. The charts derive their series from the
    /// data, so this is the one place to change; the year-on-year maths and the median window (six months either side of the
    /// baseline) still fit with room to spare.
    /// </summary>
    public const int HistoryYears = 2;

    /// <summary>Sessions through the channel group "Organic Search".</summary>
    public const string SegmentOrganic = "organic";

    /// <summary>Sessions through all channels. It cannot be derived from the organic segment.</summary>
    public const string SegmentTotal = "total";

    /// <summary>
    /// Organic search from GOOGLE specifically: the channel group AND the source.
    ///
    /// It exists so the comparison with Search Console is like for like. Search Console measures Google only, but the channel
    /// group "Organic Search" also holds Bing, DuckDuckGo and others, so comparing the two would hide part of the difference.
    ///
    /// A call of its own and not <c>sessionSource</c> as a second dimension on the organic call: with one more dimension the
    /// response has a row per month and source, and the engagement rate would have to be weighted across the sources (an average
    /// of ratios is not the ratio). One more call is cheaper than that maths, and GA4 does not charge per call.
    /// </summary>
    public const string SegmentOrganicGoogle = "organic_google";

    private const string OrganicSearch = "Organic Search";

    // The metrics are asked for in this order and read back by position, so BuildRequest and MapResponse must agree on it.
    // keyEvents is GA4's current name for conversions (called "conversions" before 2024). It costs no extra call, but an invalid
    // metric name fails the WHOLE request, so when a fetch suddenly breaks with INVALID_ARGUMENT from GA4, look at this list first.
    private static readonly string[] MetricNames = [Ga4Requests.SessionsMetric, "engagementRate", "averageSessionDuration", "keyEvents"];

    private const int SessionsColumn = 0;
    private const int EngagementRateColumn = 1;
    private const int AverageSessionColumn = 2;
    private const int KeyEventsColumn = 3;

    // Two years of months is 24 rows; GA4's own default limit is 10 000. This leaves a wide margin and keeps the request small.
    private const int MaxRows = 200;

    private static readonly string[] Segments = [SegmentOrganic, SegmentTotal, SegmentOrganicGoogle];

    // -------------------------------------------------------------------------
    // TRANSPORT
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fetches sessions, engagement, duration and key events per month for the three segments, from January of the year before
    /// <paramref name="period"/> up to and including that month, one call per segment in a fixed order (organic, total, organic from
    /// Google). A month GA4 has no data for is simply absent from the result.
    /// </summary>
    /// <exception cref="InvalidOperationException">The property ID is missing, or sessions come back as something other than an integer.</exception>
    /// <exception cref="ReportFetchException">There is no usable Google credential.</exception>
    public async Task<IReadOnlyList<Ga4Row>> FetchAsync(Period period, CancellationToken ct)
    {
        Ga4Requests.RequirePropertyId(propertyId);

        var month = new DateOnly(period.Year, period.Month, 1);
        var rows = new List<Ga4Row>();
        foreach (var segment in Segments)
        {
            var response = await Ga4Requests.RunAsync(clients, BuildRequest(propertyId, month, segment), ct).ConfigureAwait(false);
            rows.AddRange(MapResponse(response, segment));
        }

        return rows;
    }

    /// <summary>
    /// Builds the runReport request for one segment. Organic search is filtered through the default channel group; the total has
    /// no filter at all (so does any segment name that is not one of the three).
    /// </summary>
    public static RunReportRequest BuildRequest(string propertyId, DateOnly reportMonth, string segment)
    {
        var (start, end) = DateRangeFor(reportMonth);

        return new RunReportRequest
        {
            Property = propertyId,
            DateRanges = [Ga4Requests.Range(start, end)],
            Dimensions = [new Dimension { Name = "yearMonth" }], // the format is YYYYMM
            Metrics = [.. MetricNames.Select(name => new Metric { Name = name })],
            Limit = MaxRows,
            OrderBys = [Ga4Requests.ByDimension("yearMonth")],
            DimensionFilter = segment switch
            {
                SegmentOrganic => Exact(Ga4Requests.ChannelGroupDimension, OrganicSearch),

                // BOTH conditions are needed: `sessionSource = google` alone would pull in google / cpc, that is Google Ads,
                // which can be more traffic than the organic.
                SegmentOrganicGoogle => new FilterExpression
                {
                    AndGroup = new FilterExpressionList
                    {
                        Expressions = [Exact(Ga4Requests.ChannelGroupDimension, OrganicSearch), Exact(Ga4Requests.SourceDimension, "google")],
                    },
                },
                _ => null,
            },
        };
    }

    private static FilterExpression Exact(string field, string value) => new()
    {
        Filter = new Filter
        {
            FieldName = field,
            StringFilter = new StringFilter { MatchType = "EXACT", Value = value },
        },
    };

    /// <summary>The whole calendar years of history: from January 1 of the earliest of <see cref="HistoryYears"/> years up to the last day of the report month.</summary>
    public static (DateOnly Start, DateOnly End) DateRangeFor(DateOnly reportMonth)
    {
        var start = new DateOnly(reportMonth.Year - (HistoryYears - 1), 1, 1);
        var end = new DateOnly(reportMonth.Year, reportMonth.Month, DateTime.DaysInMonth(reportMonth.Year, reportMonth.Month));
        return (start, end);
    }

    public static (DateOnly Start, DateOnly End) DateRangeFor(Period period) => DateRangeFor(new DateOnly(period.Year, period.Month, 1));

    // -------------------------------------------------------------------------
    // MAPPING: a pure function, no IO. This is the part that is tested.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Maps one runReport response to <see cref="Ga4Row"/>s of <paramref name="segment"/>.
    ///
    /// GA4 returns NO row for a month without data, so there is no real zero to confuse with a gap here. Months missing from the
    /// response are missing from the result, and the processing layer renders them as null.
    /// </summary>
    /// <exception cref="InvalidOperationException">Sessions of a row are not an integer.</exception>
    public static IReadOnlyList<Ga4Row> MapResponse(RunReportResponse response, string segment)
    {
        var rows = new List<Ga4Row>();
        foreach (var row in Ga4Requests.RowsOf(response))
        {
            if (row.DimensionValues is not { Count: > 0 } dimensions || row.MetricValues is not { Count: > 0 } metrics) continue;

            var yearMonth = dimensions[0]?.Value ?? ""; // "202607"

            // GA4 delivers metrics as STRINGS. An absurd string must not quietly become 0: that would make a parse error look like a month without traffic.
            var sessionsText = metrics[SessionsColumn]?.Value ?? "";
            if (!long.TryParse(sessionsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sessions))
            {
                throw new InvalidOperationException($"GA4 returned '{sessionsText}' as sessions for {yearMonth}, which is not an integer.");
            }

            rows.Add(new Ga4Row(
                FormatPeriod(yearMonth),
                segment,
                sessions,
                EngagementRate: Ratio(metrics, EngagementRateColumn),
                AvgSessionSeconds: Ratio(metrics, AverageSessionColumn),
                KeyEvents: (long)Math.Round(Ratio(metrics, KeyEventsColumn))));
        }

        return rows;
    }

    /// <summary>A measure that is absent or not a number counts as zero, as it did when the raw data predated the measure.</summary>
    private static double Ratio(IList<MetricValue> metrics, int column)
        => column < metrics.Count && double.TryParse(metrics[column]?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    /// <summary>"202607" becomes "2026-07"; anything else is kept as it is.</summary>
    private static string FormatPeriod(string yearMonth)
        => yearMonth.Length == 6 ? $"{yearMonth[..4]}-{yearMonth.Substring(4, 2)}" : yearMonth;
}
