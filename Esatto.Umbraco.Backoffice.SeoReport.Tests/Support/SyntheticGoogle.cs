using System.Globalization;
using System.Net;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Google.Apis.AnalyticsData.v1beta;
using Google.Apis.AnalyticsData.v1beta.Data;
using Google.Apis.Json;
using Google.Apis.SearchConsole.v1;
using Google.Apis.SearchConsole.v1.Data;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>
/// Both Google services in one <see cref="IGoogleClients"/>, real service objects over fake transports (see
/// <see cref="FakeGoogleTransport"/>), answering from made-up numbers according to what is asked:
/// <list type="bullet">
/// <item>GA4: a row per month of the requested range (monthly calls), a row per day (the daily curve), or two sources (one day).</item>
/// <item>Search Console: the probe up to <c>dataThrough</c>, one row of measures per month and segment, and the three lists.</item>
/// </list>
/// With <c>empty</c> both answer <c>{}</c> to everything, like a new site. <see cref="Hold"/> parks every GA4 service request until
/// it completes, so a test can freeze a fetch half way; <see cref="Ga4Failure"/> and <see cref="SearchConsoleFailure"/> make a
/// source answer with an error status.
/// </summary>
internal sealed class SyntheticGoogle : IGoogleClients
{
    private readonly DateOnly _dataThrough;
    private int _ga4ServiceRequests;
    private int _searchConsoleServiceRequests;
    private int _ga4FailuresLeft;

    public SyntheticGoogle(DateOnly dataThrough, bool empty = false)
    {
        _dataThrough = dataThrough;
        Empty = empty;
        Ga4 = new FakeGoogleTransport(call => Respond(call, Ga4Answer, NextGa4Failure()));
        SearchConsole = new FakeGoogleTransport(call => Respond(call, SearchConsoleAnswer, SearchConsoleFailure));
        AnalyticsService = new AnalyticsDataService(Ga4.Initializer());
        SearchConsoleService = new SearchConsoleService(SearchConsole.Initializer());
    }

    public FakeGoogleTransport Ga4 { get; }

    public FakeGoogleTransport SearchConsole { get; }

    private AnalyticsDataService AnalyticsService { get; }

    private SearchConsoleService SearchConsoleService { get; }

    /// <summary>Both sources answer <c>{}</c> to everything while this is true: a site with no data yet.</summary>
    public bool Empty { get; set; }

    /// <summary>When set, every GA4 service request waits for it (the fetch is frozen at its first call).</summary>
    public Task? Hold { get; set; }

    /// <summary>When set, GA4 answers every call with this status and an error body (use a status that is not retried, such as 403).</summary>
    public HttpStatusCode? Ga4Failure { get; set; }

    /// <summary>The next <paramref name="calls"/> GA4 calls (and only those) answer 403; the ones after them go through.</summary>
    public void FailNextGa4Calls(int calls) => Volatile.Write(ref _ga4FailuresLeft, calls);

    private HttpStatusCode? NextGa4Failure()
        => Ga4Failure ?? (Interlocked.Decrement(ref _ga4FailuresLeft) >= 0 ? HttpStatusCode.Forbidden : null);

    public HttpStatusCode? SearchConsoleFailure { get; set; }

    /// <summary>When set, a service request throws it, as the real clients do when there is no usable credential.</summary>
    public Exception? ServiceFailure { get; set; }

    public int Ga4ServiceRequests => Volatile.Read(ref _ga4ServiceRequests);

    public int SearchConsoleServiceRequests => Volatile.Read(ref _searchConsoleServiceRequests);

    public async Task<AnalyticsDataService> GetAnalyticsDataAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref _ga4ServiceRequests);
        if (ServiceFailure is { } failure) throw failure;
        if (Hold is { } hold) await hold.WaitAsync(ct);
        return AnalyticsService;
    }

    public Task<SearchConsoleService> GetSearchConsoleAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref _searchConsoleServiceRequests);
        if (ServiceFailure is { } failure) throw failure;
        return Task.FromResult(SearchConsoleService);
    }

    /// <summary>The sessions of the "organic" segment in a month; "total" is three times as many, "organic_google" five fewer.</summary>
    public static long OrganicSessions(Period month) => (month.Year % 100 * 100) + (month.Month * 10);

    /// <summary>The clicks of a Search Console month and segment: distinct for each, so a row says where it came from.</summary>
    public static double Clicks(Period month, string segment)
        => (month.Year % 100 * 100) + (month.Month * 10) + segment switch { GscAgent.SegmentBrand => 1, GscAgent.SegmentNonBrand => 2, _ => 0 };

    /// <summary>The sessions of one day of the daily curve: the day of the month.</summary>
    public static long DailySessions(DateOnly day) => day.Day;

    private FakeGoogleTransport.Answer Respond(FakeGoogleTransport.Call call, Func<FakeGoogleTransport.Call, string> answer, HttpStatusCode? failure)
        => failure is { } status
            ? new FakeGoogleTransport.Answer(status, """{"error":{"code":403,"message":"made up","status":"PERMISSION_DENIED"}}""")
            : new FakeGoogleTransport.Answer(HttpStatusCode.OK, Empty ? "{}" : answer(call));

    // ---------------------------------------------------------------------------------------------------------------
    // GA4
    // ---------------------------------------------------------------------------------------------------------------

    private static string Ga4Answer(FakeGoogleTransport.Call call)
    {
        var request = NewtonsoftJsonSerializer.Instance.Deserialize<RunReportRequest>(call.Body);
        var range = request.DateRanges[0];
        var start = DateOnly.ParseExact(range.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var end = DateOnly.ParseExact(range.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        var rows = request.Dimensions[0].Name switch
        {
            "yearMonth" => MonthlyRows(start, end, request.DimensionFilter),
            "date" => DailyRows(start, end),
            _ => SourceRows(),
        };

        return NewtonsoftJsonSerializer.Instance.Serialize(new RunReportResponse { Rows = rows });
    }

    private static List<Row> MonthlyRows(DateOnly start, DateOnly end, FilterExpression? filter)
    {
        // Told apart the way the agent builds them: no filter is the total, an AND group is Google organic, a filter is organic.
        var segment = filter is null ? Ga4Agent.SegmentTotal : filter.AndGroup is not null ? Ga4Agent.SegmentOrganicGoogle : Ga4Agent.SegmentOrganic;

        var rows = new List<Row>();
        for (var month = Period.FromDate(start); month <= Period.FromDate(end); month = month.AddMonths(1))
        {
            var organic = OrganicSessions(month);
            var sessions = segment switch { Ga4Agent.SegmentTotal => organic * 3, Ga4Agent.SegmentOrganicGoogle => organic - 5, _ => organic };

            rows.Add(Row($"{month.Year:D4}{month.Month:D2}", sessions.ToString(CultureInfo.InvariantCulture), "0.6", "62.5", "3"));
        }

        return rows;
    }

    private static List<Row> DailyRows(DateOnly start, DateOnly end)
    {
        var rows = new List<Row>();
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            rows.Add(Row(day.ToString("yyyyMMdd", CultureInfo.InvariantCulture), DailySessions(day).ToString(CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static List<Row> SourceRows() =>
    [
        new Row { DimensionValues = [Value("Email"), Value("newsletter")], MetricValues = [Metric("40")] },
        new Row { DimensionValues = [Value("Organic Search"), Value("google")], MetricValues = [Metric("25")] },
    ];

    private static Row Row(string dimension, params string[] metrics)
        => new() { DimensionValues = [Value(dimension)], MetricValues = [.. metrics.Select(Metric)] };

    private static DimensionValue Value(string text) => new() { Value = text };

    private static MetricValue Metric(string text) => new() { Value = text };

    // ---------------------------------------------------------------------------------------------------------------
    // Search Console
    // ---------------------------------------------------------------------------------------------------------------

    private string SearchConsoleAnswer(FakeGoogleTransport.Call call)
    {
        var request = NewtonsoftJsonSerializer.Instance.Deserialize<SearchAnalyticsQueryRequest>(call.Body);

        List<ApiDataRow> rows;
        switch (string.Join(",", request.Dimensions ?? []))
        {
            case "date":
                rows = [];
                var last = Date(request.EndDate) < _dataThrough ? Date(request.EndDate) : _dataThrough;
                for (var day = Date(request.StartDate); day <= last; day = day.AddDays(1)) rows.Add(DataRow(1, 1, Iso(day)));
                break;
            case "query":
                rows = [DataRow(700, 5000, "acme"), DataRow(120, 9000, "example widget")];
                break;
            case "page":
                rows = [DataRow(500, 8000, "https://www.example.com/")];
                break;
            case "query,page":
                rows = [DataRow(650, 4000, "acme", "https://www.example.com/")];
                break;
            default:
                var segment = request.DimensionFilterGroups?[0].Filters[0].Operator__ switch
                {
                    "includingRegex" => GscAgent.SegmentBrand,
                    "excludingRegex" => GscAgent.SegmentNonBrand,
                    _ => GscAgent.SegmentTotal,
                };
                var clicks = Clicks(Period.FromDate(Date(request.StartDate)), segment);
                rows = [DataRow(clicks, clicks * 10)];
                break;
        }

        return NewtonsoftJsonSerializer.Instance.Serialize(new SearchAnalyticsQueryResponse { Rows = rows });
    }

    private static ApiDataRow DataRow(double clicks, double impressions, params string[] keys)
        => new() { Clicks = clicks, Impressions = impressions, Ctr = 0.05, Position = 7.5, Keys = keys.Length == 0 ? null : [.. keys] };

    private static DateOnly Date(string text) => DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
