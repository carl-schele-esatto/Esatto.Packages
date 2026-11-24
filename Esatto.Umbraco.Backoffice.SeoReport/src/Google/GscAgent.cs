using System.Globalization;
using System.Text.RegularExpressions;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Google;
using Google.Apis.SearchConsole.v1;
using Google.Apis.SearchConsole.v1.Data;

namespace Esatto.Umbraco.Backoffice.SeoReport.Google;

/// <summary>
/// One row of raw Search Analytics data: a period, a segment and the four measures Search Console itself delivers. No
/// year-on-year, no summing, no interpretation: that is the report mapper's job.
/// </summary>
/// <param name="Period">The month, <c>YYYY-MM</c>.</param>
/// <param name="Segment">One of <see cref="GscAgent.SegmentTotal"/>, <see cref="GscAgent.SegmentBrand"/>, <see cref="GscAgent.SegmentNonBrand"/>.</param>
/// <param name="Clicks">Clicks in the month.</param>
/// <param name="Impressions">Impressions in the month.</param>
/// <param name="CtrFraction">
/// Named like this on purpose: Search Console returns the click-through rate as a FRACTION (0.1228), not as a percentage
/// (12.28). The conversion happens in the report mapper, where it can be seen. A field that was just called "Ctr" is exactly
/// where a silent factor-of-a-hundred bug is born.
/// </param>
/// <param name="Position">Average position; zero when Search Console gave none.</param>
public record GscRow(
    string Period,
    string Segment,
    long Clicks,
    long Impressions,
    double CtrFraction,
    double Position);

/// <summary>
/// The result of a Search Console fetch: the rows, plus what the agent KNOWS about the limits of the source. Both limits are
/// fields so the two classic Search Console pitfalls show up in the report instead of being read as data.
/// </summary>
/// <param name="Rows">One row per (period, segment) THAT ACTUALLY HAS DATA.</param>
/// <param name="DataThrough">
/// The last date Search Console actually has finalised data for, <c>YYYY-MM-DD</c>. Measured, not guessed: see the probe in
/// <see cref="GscAgent.FetchAsync"/>. Null when the probe held no date.
/// </param>
/// <param name="LatestCompletePeriod">
/// The latest month FULLY covered by <paramref name="DataThrough"/>, <c>YYYY-MM</c>. For a fetch on the first of a month that
/// is the month before the previous one.
/// </param>
/// <param name="WindowStart">
/// The earliest month inside Search Console's 16-month window, <c>YYYY-MM</c>. Older periods cannot be fetched and must NEVER
/// become zeros.
/// </param>
/// <param name="OutOfWindowPeriods">The months of the curve that fall before the window (<c>YYYY-MM</c>), left empty in the report with a note.</param>
/// <param name="Queries">
/// The report month's search queries, already sorted by clicks by the API. An empty list when the call was not made or
/// answered nothing.
/// </param>
/// <param name="Pages">The report month's landing pages, sorted by clicks by the API.</param>
/// <param name="QueryPages">The report month's query-by-page pairs.</param>
public record GscFetch(
    IReadOnlyList<GscRow> Rows,
    string? DataThrough,
    string LatestCompletePeriod,
    string WindowStart,
    IReadOnlyList<string> OutOfWindowPeriods,
    IReadOnlyList<GscQueryRow>? Queries = null,
    IReadOnlyList<GscPageRow>? Pages = null,
    IReadOnlyList<GscQueryPageRow>? QueryPages = null);

/// <summary>A search query in the source's OWN units: CTR as a fraction (0.1228), not as a percentage. The conversion happens in the report mapper.</summary>
public record GscQueryRow(
    string Query,
    long Clicks,
    long Impressions,
    double CtrFraction,
    double Position);

/// <summary>
/// A search query AND the page it was shown for. This pair is what lets a target query name the page that ranks: the query and
/// page lists on their own never can.
/// </summary>
public record GscQueryPageRow(
    string Query,
    string Url,
    long Clicks,
    long Impressions,
    double CtrFraction,
    double Position);

/// <summary>A landing page in the source's own units. CTR as a fraction, not a percentage.</summary>
public record GscPageRow(
    string Url,
    long Clicks,
    long Impressions,
    double CtrFraction,
    double Position);

/// <summary>The answer to one month-and-segment call of a fetch.</summary>
/// <param name="Period">The month asked for.</param>
/// <param name="Segment">The segment asked for, as in <see cref="GscRow.Segment"/>.</param>
/// <param name="Response">The answer as it came; null when there is none.</param>
public sealed record GscPeriodResponse(Period Period, string Segment, SearchAnalyticsQueryResponse? Response);

/// <summary>
/// Every answer of one fetch, as Search Console gave them, which is what <see cref="GscAgent.MapResponse"/> turns into a
/// <see cref="GscFetch"/>. A call that was not made is null.
/// </summary>
/// <param name="Probe">The answer to the probe call: one row per day, the date in the first key.</param>
/// <param name="Periods">The answers of the month-and-segment calls, in the order they were made.</param>
/// <param name="Queries">The answer to the query list call.</param>
/// <param name="Pages">The answer to the page list call.</param>
/// <param name="QueryPages">The answer to the query-by-page call.</param>
public sealed record GscResponses(
    SearchAnalyticsQueryResponse? Probe,
    IReadOnlyList<GscPeriodResponse> Periods,
    SearchAnalyticsQueryResponse? Queries = null,
    SearchAnalyticsQueryResponse? Pages = null,
    SearchAnalyticsQueryResponse? QueryPages = null);

/// <summary>
/// The fetch layer against the Search Analytics API (Google's stable REST client, <c>Google.Apis.SearchConsole.v1</c>). Two halves,
/// kept apart on purpose: <see cref="FetchAsync"/> is the transport and calls the API; <see cref="MapResponse"/> is the mapping,
/// a pure function from the answers to a <see cref="GscFetch"/>, which is what the tests exercise.
///
/// THREE CALLS PER MONTH:
///   1. total      - no query filter at all
///   2. brand      - a dimension filter on <c>query</c>: includingRegex = the brand pattern
///   3. non-brand  - a dimension filter on <c>query</c>: excludingRegex = the brand pattern
///
/// The first call can look redundant, since brand plus non-brand ought to be the total. It is not. As soon as one filters on
/// query, Search Console leaves out anonymised queries (rare searches that could identify a person). Without the unfiltered
/// call the report's "clicks in Search Console" would silently sit below what the customer sees in the Search Console
/// interface, and nobody would know why. The report mapper works out the difference and notes it.
///
/// ONE MONTH PER CALL, not the <c>date</c> dimension. The API has no month dimension. Grouped by date, the click-through rate
/// and the average position would have to be weighted together here, which is arithmetic in the fetch layer on numbers where a
/// plain average gives the wrong answer. One call per month with no dimensions gives Search Console's own, correctly weighted
/// monthly figures.
/// </summary>
public sealed class GscAgent
{
    /// <summary>Search Console keeps 16 months. Older periods simply do not exist.</summary>
    public const int RetentionMonths = 16;

    /// <summary>
    /// How many search queries are fetched for the report month.
    ///
    /// 250 AND NOT 25, since the target queries arrived. The fetch is now a DATA EXTRACT that the analysis reads from, while the
    /// top list in the interface still shows 25 rows: the views cut it. The reason: a target query such as "web design" is never
    /// among the 25 most shown queries, because those are almost always brand searches. Without the depth the analysis cannot tell
    /// "we do not show up" from "we did not look far enough down".
    ///
    /// It costs nothing extra: the row limit is a field of a request that is made anyway, and the API allows 25,000 rows. The
    /// number of calls per fetch is unchanged.
    /// </summary>
    public const int QueryRowLimit = 250;

    /// <summary>
    /// How many rows for query by page. Higher than the other two on purpose: the combination is by nature more numerous, and the
    /// API sorts by CLICKS. The cases the analysis needs most, many impressions without clicks, therefore sit at the bottom of the
    /// answer and are cut first.
    ///
    /// Still a single call. Only the pairs whose query is also in the archived query list are kept (see the report mapper), so the
    /// archive does not grow by a thousand rows.
    /// </summary>
    public const int QueryPageRowLimit = 1000;

    /// <summary>
    /// How many landing pages.
    ///
    /// 200 AND NOT 25, since the analysis tab arrived. The page list is now a BASIS the analysis searches in: "which pages lost the
    /// most against last year" cannot be answered from a top 25, because a page that fell out of the top list is exactly the one to
    /// find. The interface still shows 25 rows; the views cut it.
    ///
    /// The same reasoning and the same cost as <see cref="QueryRowLimit"/>: a field of a request that is made anyway.
    /// </summary>
    public const int PageRowLimit = 200;

    /// <summary>
    /// The documented data lag. Used ONLY as a fallback when the probe held no date and so could not measure the real lag.
    /// </summary>
    public const int FallbackLagDays = 3;

    /// <summary>
    /// How many months back the curves reach. The report month and this many before it are thirteen months, which is the archive
    /// depth and stays within <see cref="RetentionMonths"/>.
    /// </summary>
    public const int WindowMonths = 12;

    public const string SegmentBrand = "brand";
    public const string SegmentNonBrand = "non_brand";
    public const string SegmentTotal = "total";

    /// <summary>"final" = only finalised data. With "all" the last days can change afterwards, and an archived report must not be able to turn out wrong.</summary>
    private const string FinalData = "final";

    private const int MaxAttempts = 4;

    private static readonly string[] Segments = [SegmentTotal, SegmentBrand, SegmentNonBrand];

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    private readonly string _siteUrl;
    private readonly string _brandRegex;
    private readonly IGoogleClients _clients;
    private readonly DateOnly _today;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    /// <param name="siteUrl">
    /// The Search Console property. A domain property is <c>sc-domain:example.com</c>; a URL-prefix property is
    /// <c>https://www.example.com/</c> (the trailing slash counts and must match).
    /// </param>
    /// <param name="brandRegex">The brand pattern, <c>(?i)(a|b)</c>, as <see cref="BrandPattern.Build"/> makes it.</param>
    /// <param name="clients">
    /// Where the authorised Search Console service comes from. The same key serves both sources, but the permission has to be
    /// given separately in each product.
    /// </param>
    /// <param name="today">Injectable for testability; the default is today's date (UTC).</param>
    public GscAgent(string siteUrl, string brandRegex, IGoogleClients clients, DateOnly? today = null)
        : this(siteUrl, brandRegex, clients, today, Task.Delay)
    {
    }

    /// <summary>The same, with the wait between attempts replaced, so a test does not sit out the seconds.</summary>
    internal GscAgent(string siteUrl, string brandRegex, IGoogleClients clients, DateOnly? today, Func<TimeSpan, CancellationToken, Task> delay)
    {
        _siteUrl = siteUrl;
        _brandRegex = brandRegex;
        _clients = clients;
        _today = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
        _delay = delay;
    }

    // -------------------------------------------------------------------------
    // TRANSPORT
    // -------------------------------------------------------------------------

    /// <summary>
    /// Makes the probe call and then three calls for each month of the curve that is inside the window and complete, and the
    /// three lists of the report month, and maps the answers. Calls go one at a time, in that order.
    /// </summary>
    /// <remarks>
    /// A failed call is not swallowed, the probe's included: it stops the run, because what is fetched is archived for good and a
    /// guessed lag could archive a month that is not complete. The documented lag stands in only when the probe SUCCEEDS but
    /// holds no date.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The Search Console property or the brand pattern is missing.</exception>
    /// <exception cref="ArgumentException">The brand pattern is not one Search Console can use.</exception>
    /// <exception cref="ReportFetchException">There is no usable Google credential.</exception>
    /// <exception cref="GoogleApiException">Search Console refused a call, or kept failing after the attempts.</exception>
    public async Task<GscFetch> FetchAsync(Period period, CancellationToken ct)
    {
        RequireSiteUrl(_siteUrl);
        ValidateBrandRegex(_brandRegex);

        var windowStart = EarliestAvailableMonth(_today);
        var service = await _clients.GetSearchConsoleAsync(ct).ConfigureAwait(false);

        // Measured, not guessed: ask Search Console how far the data actually reaches.
        var probe = await QueryAsync(service, BuildProbeRequest(_today), ct).ConfigureAwait(false);
        var latestComplete = LatestCompleteMonthOrFallback(DataThroughFrom(probe), _today);

        var periods = new List<GscPeriodResponse>();
        foreach (var month in CurveMonths(period))
        {
            // The 16-month window. Outside it there is no data to fetch, and a call would answer empty, which cannot be told from
            // "zero clicks". We skip it and say so instead.
            if (month < windowStart) continue;

            // Months that are not complete yet are not fetched at all: half a month drawn next to whole months READS as a drop.
            if (month > latestComplete) continue;

            foreach (var segment in Segments)
            {
                var response = await QueryAsync(service, BuildPeriodRequest(FirstDay(month), segment, _brandRegex), ct).ConfigureAwait(false);
                periods.Add(new GscPeriodResponse(month, segment, response));
            }
        }

        // The lists of the report month: three more calls, and only for a complete month (the top list of half a month looks
        // like a full one and is not).
        var listMonth = period > latestComplete ? latestComplete : period;
        if (listMonth < windowStart) return MapResponse(_today, period, new GscResponses(probe, periods));

        var first = FirstDay(listMonth);
        var queries = await QueryAsync(service, BuildQueriesRequest(first), ct).ConfigureAwait(false);
        var pages = await QueryAsync(service, BuildPagesRequest(first), ct).ConfigureAwait(false);
        var queryPages = await QueryAsync(service, BuildQueryPagesRequest(first), ct).ConfigureAwait(false);

        return MapResponse(_today, period, new GscResponses(probe, periods, queries, pages, queryPages));
    }

    /// <summary>
    /// The report month's most clicked search queries. One call, dimension <c>query</c>, sorted by clicks by the API. NO brand
    /// filter: this is the top list for the whole, and filtering on query would also throw the anonymised queries away twice.
    /// </summary>
    public static SearchAnalyticsQueryRequest BuildQueriesRequest(DateOnly month) => MonthRequest(month, QueryRowLimit, "query");

    /// <summary>
    /// Query by page for the report month: BOTH dimensions in the same request, the only way to learn which page was shown for a
    /// given search. One call.
    /// </summary>
    public static SearchAnalyticsQueryRequest BuildQueryPagesRequest(DateOnly month) => MonthRequest(month, QueryPageRowLimit, "query", "page");

    /// <summary>
    /// The report month's most clicked landing pages. Identical to the query call except for the dimension, <c>page</c>: the two
    /// answer different halves of the same question.
    /// </summary>
    public static SearchAnalyticsQueryRequest BuildPagesRequest(DateOnly month) => MonthRequest(month, PageRowLimit, "page");

    /// <summary>
    /// The probe call: grouped by date over the last 45 days. The last date in the answer is the real end of the finalised data.
    /// </summary>
    public static SearchAnalyticsQueryRequest BuildProbeRequest(DateOnly today) => new()
    {
        StartDate = Iso(today.AddDays(-45)),
        EndDate = Iso(today),
        Dimensions = ["date"],
        DataState = FinalData,
        RowLimit = 60,
    };

    /// <summary>
    /// One call is one month and one segment. No dimensions: Search Console then returns a single row with its OWN aggregates for
    /// clicks, impressions, CTR and average position, so nothing is summed up here. Any segment name other than brand and
    /// non-brand gives the unfiltered total.
    /// </summary>
    public static SearchAnalyticsQueryRequest BuildPeriodRequest(DateOnly month, string segment, string brandRegex)
    {
        var request = MonthRequest(month, 1);

        var op = segment switch
        {
            SegmentBrand => "includingRegex",
            SegmentNonBrand => "excludingRegex",
            _ => null, // total: no query filter at all
        };

        if (op is not null)
        {
            // THE BRAND / NON-BRAND SPLIT. The regex route is the reproducible one: the same expression can be pasted into the
            // Search Console interface and gives the same answer. Search Console runs RE2 and matches a substring.
            request.DimensionFilterGroups =
            [
                new ApiDimensionFilterGroup
                {
                    GroupType = "and",
                    Filters = [new ApiDimensionFilter { Dimension = "query", Operator__ = op, Expression = brandRegex }],
                },
            ];
        }

        return request;
    }

    /// <summary>
    /// A request for the whole of <paramref name="month"/> (any day of it will do), final data only, grouped by
    /// <paramref name="dimensions"/>.
    /// </summary>
    private static SearchAnalyticsQueryRequest MonthRequest(DateOnly month, int rowLimit, params string[] dimensions)
    {
        var first = FirstDay(Period.FromDate(month));
        var last = new DateOnly(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));

        return new SearchAnalyticsQueryRequest
        {
            StartDate = Iso(first),
            EndDate = Iso(last),
            DataState = FinalData,
            Dimensions = dimensions.Length == 0 ? null : [.. dimensions],
            RowLimit = rowLimit,
        };
    }

    /// <summary>
    /// Sends one request to the site and tries again after a transient failure (429 or any 5xx): the quota is generous against the
    /// 40 or so calls of a fetch, but a single 503 in the middle should not cost the whole report. Waits 2, 4 and 8 seconds, then
    /// gives up and lets the Google exception through. Any other failure is not retried and not caught: waiting cannot cure it.
    /// </summary>
    private async Task<SearchAnalyticsQueryResponse> QueryAsync(SearchConsoleService service, SearchAnalyticsQueryRequest request, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                // The property goes in the address (the client escapes it); the body is the request alone.
                return await service.Searchanalytics.Query(request, _siteUrl).ExecuteAsync(ct).ConfigureAwait(false);
            }
            catch (GoogleApiException ex) when (attempt < MaxAttempts && IsTransient(ex))
            {
                await _delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct).ConfigureAwait(false);
            }
        }
    }

    private static bool IsTransient(GoogleApiException ex) => (int)ex.HttpStatusCode is 429 or >= 500;

    private static void RequireSiteUrl(string siteUrl)
    {
        if (string.IsNullOrWhiteSpace(siteUrl) || siteUrl.Contains("XXXX", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The Search Console property is not set. Enter it on the Settings tab, or set the configuration value "
                + $"'{SeoReportSettings.SectionName}:{nameof(SeoReportSettings.SearchConsoleSite)}'. Use 'sc-domain:example.com' for a "
                + "domain property and 'https://www.example.com/' (with the trailing slash) for a URL-prefix property. "
                + "The wrong form returns empty answers without an error.");
        }
    }

    // -------------------------------------------------------------------------
    // MAPPING: a pure function, no IO. This is the part that is tested.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Maps the answers of one fetch to a <see cref="GscFetch"/>. Everything is derived from the answers and the date: the lag
    /// from the probe, the window from <paramref name="today"/>. Nothing is precomputed.
    /// </summary>
    /// <param name="today">The day of the fetch: the 16-month window and the documented lag count from it.</param>
    /// <param name="reportPeriod">The month the report is for; the curve is this month and the twelve before it.</param>
    /// <param name="responses">The answers; a call that was not made is null, an answer without rows is read as nothing.</param>
    public static GscFetch MapResponse(DateOnly today, Period reportPeriod, GscResponses responses)
    {
        var windowStart = EarliestAvailableMonth(today);
        var dataThrough = DataThroughFrom(responses.Probe);

        return new GscFetch(
            MapPeriodRows(responses.Periods),
            dataThrough is { } through ? Iso(through) : null,
            LatestCompleteMonthOrFallback(dataThrough, today).ToString(),
            windowStart.ToString(),

            // The months the report's curve wants but that lie before the window.
            [.. CurveMonths(reportPeriod).Where(month => month < windowStart).Select(month => month.ToString())],

            // The dimension value is in Keys, in the order the request named the dimensions. The API sorts by clicks, so the
            // order of the answer is the top list and is kept.
            MapRows(responses.Queries, 1, (keys, row) => new GscQueryRow(keys[0], Count(row.Clicks), Count(row.Impressions), row.Ctr ?? 0, row.Position ?? 0)),
            MapRows(responses.Pages, 1, (keys, row) => new GscPageRow(keys[0], Count(row.Clicks), Count(row.Impressions), row.Ctr ?? 0, row.Position ?? 0)),

            // Both dimensions in the same row: Keys[0] is the query, Keys[1] the page.
            MapRows(responses.QueryPages, 2, (keys, row) => new GscQueryPageRow(keys[0], keys[1], Count(row.Clicks), Count(row.Impressions), row.Ctr ?? 0, row.Position ?? 0)));
    }

    private static List<GscRow> MapPeriodRows(IReadOnlyList<GscPeriodResponse> periods)
    {
        var rows = new List<GscRow>();
        foreach (var call in periods)
        {
            // AN EMPTY ANSWER IS NO ROW.
            //
            // This was wrong at first. An empty answer was mapped to zeros, and the chart then drew a line along zero where there
            // should be a break: exactly the "a zero is read as a drop" the whole handling exists to avoid. Search Console answers
            // empty both when a month has no impressions and when the property was not verified yet. The API gives nothing to tell
            // them apart, so the only honest reading is "not measured", not "measured as zero".
            if (call.Response?.Rows?.FirstOrDefault() is not { } row) continue;

            rows.Add(new GscRow(call.Period.ToString(), call.Segment, Count(row.Clicks), Count(row.Impressions), row.Ctr ?? 0, row.Position ?? 0));
        }

        return rows;
    }

    /// <summary>
    /// The rows of an answer that carry the <paramref name="keyCount"/> dimension values the request asked for. A row without
    /// them (no key list, too few keys, a blank key) is skipped: the dimension value is the row's identity, so it cannot be a
    /// query or a page without it. REST leaves <c>rows</c> and <c>keys</c> out when they are empty, so both can be null.
    /// </summary>
    private static List<T> MapRows<T>(SearchAnalyticsQueryResponse? response, int keyCount, Func<IList<string>, ApiDataRow, T> map)
    {
        var mapped = new List<T>();
        foreach (var row in response?.Rows ?? [])
        {
            if (row.Keys is { } keys && keys.Count >= keyCount && keys.Take(keyCount).All(key => !string.IsNullOrWhiteSpace(key)))
            {
                mapped.Add(map(keys, row));
            }
        }

        return mapped;
    }

    /// <summary>Whole numbers for counts, rounded half to even like <see cref="Math.Round(double)"/>; a missing count is zero.</summary>
    private static long Count(double? value) => (long)Math.Round(value ?? 0);

    /// <summary>The last date of the probe's answer = the real end of the finalised data; null when there is none to read.</summary>
    public static DateOnly? DataThroughFrom(SearchAnalyticsQueryResponse? probe)
    {
        DateOnly? latest = null;
        foreach (var row in probe?.Rows ?? [])
        {
            if (DateOnly.TryParse(row.Keys?.FirstOrDefault(), CultureInfo.InvariantCulture, out var day) && (latest is null || day > latest))
            {
                latest = day;
            }
        }

        return latest;
    }

    // -------------------------------------------------------------------------
    // The calendar, and what is known about the source
    // -------------------------------------------------------------------------

    /// <summary>
    /// The earliest month inside the 16-month window. Periods before it cannot be fetched, now or later, and are shown as MISSING.
    /// </summary>
    public static Period EarliestAvailableMonth(DateOnly today) => Period.FromDate(today.AddMonths(-(RetentionMonths - 1)));

    /// <summary>
    /// The latest month FULLY covered by the available data.
    ///
    /// That is the whole point of the report's latest period: on the first of a month the data does not even reach the end of the
    /// previous month, and a report that shows that month anyway shows an amputated figure that is read as a drop.
    /// </summary>
    public static Period LatestCompleteMonth(DateOnly dataThrough)
    {
        var month = Period.FromDate(dataThrough);
        return dataThrough.Day < DateTime.DaysInMonth(dataThrough.Year, dataThrough.Month) ? month.Previous() : month;
    }

    private static Period LatestCompleteMonthOrFallback(DateOnly? dataThrough, DateOnly today)
        => LatestCompleteMonth(dataThrough ?? today.AddDays(-FallbackLagDays));

    /// <summary>The months of the curve, oldest first: the report month and <see cref="WindowMonths"/> before it.</summary>
    private static IEnumerable<Period> CurveMonths(Period reportPeriod)
        => Enumerable.Range(0, WindowMonths + 1).Select(i => reportPeriod.AddMonths(i - WindowMonths));

    private static DateOnly FirstDay(Period month) => new(month.Year, month.Month, 1);

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // -------------------------------------------------------------------------
    // The brand pattern preflight
    // -------------------------------------------------------------------------

    // Fixed sentences: a message never carries any of the pattern's text (not even the parser's own, which repeats the pattern),
    // so what somebody typed cannot travel through an exception into a log or a response.
    private const string LookaroundMessage =
        "The brand pattern uses lookahead or lookbehind, which Search Console's RE2 engine does not support. Rewrite it with plain alternation.";

    private const string BackreferenceMessage =
        "The brand pattern uses a backreference (\\1 and the like), which Search Console's RE2 engine does not support.";

    private const string NotARegexMessage =
        "The brand pattern is not a valid regular expression. Check the brand terms on the Settings tab.";

    private static readonly string[] Lookarounds = ["(?=", "(?!", "(?<=", "(?<!"];

    /// <summary>
    /// A preflight on the brand pattern. Search Console runs RE2, which has no lookaround and no backreferences; they make the API
    /// answer 400 with a message that does not point at the setting. Better to fail here, with the setting in hand. Only .NET
    /// syntax can be checked here: a pattern .NET reads but RE2 does not (an atomic group <c>(?&gt;...)</c>, a possessive
    /// quantifier) passes. The patterns <see cref="BrandPattern.Build"/> makes use none of that: an inline <c>(?i)</c>, a group,
    /// plain alternation and escaped literals.
    /// </summary>
    /// <exception cref="InvalidOperationException">The pattern is missing (null, empty or blank).</exception>
    /// <exception cref="ArgumentException">The pattern uses lookaround or a backreference, or is not a regular expression.</exception>
    public static void ValidateBrandRegex(string? brandRegex)
    {
        if (string.IsNullOrWhiteSpace(brandRegex))
        {
            throw new InvalidOperationException(
                "The brand pattern is empty. Add at least one brand term on the Settings tab, or set the configuration value "
                + $"'{SeoReportSettings.SectionName}:{nameof(SeoReportSettings.BrandTerms)}'. Without it the brand and non-brand split "
                + "cannot be made, and a Search Console report without that split hides the very figure it exists for.");
        }

        if (Lookarounds.Any(token => brandRegex.Contains(token, StringComparison.Ordinal)))
        {
            throw new ArgumentException(LookaroundMessage, nameof(brandRegex));
        }

        // A backslash and a digit, where the backslash is not itself escaped: an odd run of backslashes before the digit. An
        // escaped backslash before a digit (a term like c:\2024, which BrandPattern writes as c:\\2024) is a literal.
        if (Regex.IsMatch(brandRegex, @"(?<!\\)(?:\\\\)*\\[1-9]", RegexOptions.None, MatchTimeout))
        {
            throw new ArgumentException(BackreferenceMessage, nameof(brandRegex));
        }

        try
        {
            // The pattern is only parsed here, never matched, so the timeout is a guard and not a limit; Search Console does the matching.
            _ = new Regex(brandRegex, RegexOptions.None, MatchTimeout);
        }
        catch (ArgumentException)
        {
            // Not rethrown as the inner exception: its message repeats the pattern.
            throw new ArgumentException(NotARegexMessage, nameof(brandRegex));
        }
    }
}
