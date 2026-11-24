using System.Net;
using System.Text.Json.Nodes;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Google;
using Google.Apis.Json;
using Google.Apis.SearchConsole.v1.Data;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The other half of <see cref="GscAgentTests"/>: the agent fetching through Google's real request pipeline over a fake
/// transport. A synthetic Search Console answers according to what is asked (the probe's dates, one month and segment, the three
/// lists), so what is checked is which calls go out, in which order, with which request and to which address, and what comes
/// back mapped.
/// </summary>
public partial class GscAgentTests
{
    // ---------------------------------------------------------------------------------------------------------------
    // A synthetic Search Console
    // ---------------------------------------------------------------------------------------------------------------

    private static FakeGoogleTransport.Answer Ok(SearchAnalyticsQueryResponse response)
        => new(HttpStatusCode.OK, NewtonsoftJsonSerializer.Instance.Serialize(response));

    private static string ErrorJson(int code, string status) => $$$"""{"error":{"code":{{{code}}},"message":"made up","status":"{{{status}}}"}}""";

    private static SearchAnalyticsQueryRequest Sent(FakeGoogleTransport.Call call)
        => NewtonsoftJsonSerializer.Instance.Deserialize<SearchAnalyticsQueryRequest>(call.Body);

    /// <summary>The measures of a month and segment, made up and distinct for each, so a row in the result says where it came from.</summary>
    private static (double Clicks, double Impressions) Measures(Period month, string segment)
    {
        var clicks = (month.Year % 100 * 100) + (month.Month * 10) + segment switch { "brand" => 1, "non_brand" => 2, _ => 0 };
        return (clicks, clicks * 10);
    }

    private static IEnumerable<GscRow> ExpectedRows(IEnumerable<Period> months)
        => months.SelectMany(month => new[] { GscAgent.SegmentTotal, GscAgent.SegmentBrand, GscAgent.SegmentNonBrand }.Select(segment =>
        {
            var (clicks, impressions) = Measures(month, segment);
            return new GscRow(month.ToString(), segment, (long)clicks, (long)impressions, 0.05, 7.5);
        }));

    private static readonly ApiDataRow[] QueryRows = [DataRow(700, 5000, 0.14, 1.2, "acme"), DataRow(120, 9000, 0.0133, 6.55, "example widget")];

    private static readonly ApiDataRow[] PageRows = [DataRow(500, 8000, 0.0625, 2.1, "https://www.example.com/")];

    private static readonly ApiDataRow[] QueryPageRows = [DataRow(650, 4000, 0.1625, 1.1, "acme", "https://www.example.com/")];

    /// <summary>
    /// What a Search Console whose finalised data reaches <paramref name="dataThrough"/> answers: the probe a row per day up to
    /// there, a month and segment (told apart by the filter) one row of measures, and each list its rows.
    /// </summary>
    private static FakeGoogleTransport.Answer Synthetic(DateOnly dataThrough, FakeGoogleTransport.Call call)
    {
        var request = Sent(call);
        switch (string.Join(",", request.Dimensions ?? []))
        {
            case "date":
                var rows = new List<ApiDataRow>();
                var last = Date(request.EndDate) < dataThrough ? Date(request.EndDate) : dataThrough;
                for (var day = Date(request.StartDate); day <= last; day = day.AddDays(1)) rows.Add(DataRow(1, 1, 0.1, 1, Iso(day)));
                return Ok(new SearchAnalyticsQueryResponse { Rows = rows });
            case "query": return Ok(new SearchAnalyticsQueryResponse { Rows = QueryRows });
            case "page": return Ok(new SearchAnalyticsQueryResponse { Rows = PageRows });
            case "query,page": return Ok(new SearchAnalyticsQueryResponse { Rows = QueryPageRows });
            default:
                var segment = request.DimensionFilterGroups?[0].Filters[0].Operator__ switch
                {
                    "includingRegex" => GscAgent.SegmentBrand,
                    "excludingRegex" => GscAgent.SegmentNonBrand,
                    _ => GscAgent.SegmentTotal,
                };
                var (clicks, impressions) = Measures(Period.FromDate(Date(request.StartDate)), segment);
                return Ok(new SearchAnalyticsQueryResponse { Rows = [DataRow(clicks, impressions, 0.05, 7.5)] });
        }
    }

    private static FakeSearchConsole SearchConsoleThrough(DateOnly dataThrough)
        => new(new FakeGoogleTransport(call => Synthetic(dataThrough, call)));

    private static GscAgent Agent(IGoogleClients clients, DateOnly today, string site = Site) => new(site, BrandRegex, clients, today);

    private static IEnumerable<Period> Months(string from, string to)
    {
        for (var month = Period.Parse(from); month <= Period.Parse(to); month = month.AddMonths(1)) yield return month;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The calls of a fetch
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A complete report month: the probe first, then thirteen months (the report month and twelve before it) oldest first, each
    /// by total, brand and non-brand in that order, then the three lists of the report month. Every request is the one Nova
    /// builds (the report month's are compared with the goldens), each goes to the site's address and none carries the site in
    /// its body.
    /// </summary>
    [Fact]
    public async Task FetchAsync_ForACompleteMonth_AsksForTheProbeThenThirteenMonthsBySegmentThenTheThreeLists()
    {
        var fake = SearchConsoleThrough(new DateOnly(2026, 8, 12));

        var fetch = await Agent(fake, ProbeDay).FetchAsync(Period.Parse("2026-07"), CancellationToken.None);

        var months = Months("2025-07", "2026-07").ToList();
        Assert.Equal(13, months.Count);
        Assert.Equal(1 + (13 * 3) + 3, fake.Calls.Count);

        Assert.All(fake.Calls, call => Assert.Contains("/sites/sc-domain%3Aexample.com/searchAnalytics/query", call.Uri.AbsoluteUri));
        Assert.All(fake.Calls, call => Assert.DoesNotContain("example.com", call.Body)); // the site is in the address, not the body

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(fake.Calls[0].Body), Fixtures.Json("gsc-request-golden-probe.json")), fake.Calls[0].Body);

        var call = 1;
        foreach (var month in months)
        {
            foreach (var segment in new[] { GscAgent.SegmentTotal, GscAgent.SegmentBrand, GscAgent.SegmentNonBrand })
            {
                var expected = GscAgent.BuildPeriodRequest(new DateOnly(month.Year, month.Month, 1), segment, BrandRegex);
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(fake.Calls[call].Body), OnTheWire(expected)), $"{month} {segment}: {fake.Calls[call].Body}");
                call++;
            }
        }

        // July 2026 is the report month: its three period calls and the three lists are exactly Nova's requests.
        foreach (var (index, kind) in new[]
        {
            (37, "period-total"), (38, "period-brand"), (39, "period-non_brand"), (40, "queries"), (41, "pages"), (42, "query-pages"),
        })
        {
            var golden = Fixtures.Json($"gsc-request-golden-{kind}.json");
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(fake.Calls[index].Body), golden), $"call {index} ({kind}): {fake.Calls[index].Body}");
        }

        Assert.Equal("2026-08-12", fetch.DataThrough);
        Assert.Equal("2026-07", fetch.LatestCompletePeriod);
        Assert.Equal("2025-05", fetch.WindowStart);
        Assert.Empty(fetch.OutOfWindowPeriods);
        Assert.Equal(ExpectedRows(months), fetch.Rows);
        Assert.Equal(new[] { new GscQueryRow("acme", 700, 5000, 0.14, 1.2), new GscQueryRow("example widget", 120, 9000, 0.0133, 6.55) }, fetch.Queries);
        Assert.Equal(new[] { new GscPageRow("https://www.example.com/", 500, 8000, 0.0625, 2.1) }, fetch.Pages);
        Assert.Equal(new[] { new GscQueryPageRow("acme", "https://www.example.com/", 650, 4000, 0.1625, 1.1) }, fetch.QueryPages);
    }

    /// <summary>
    /// Asked for a month Search Console has not finished (the data stops on the 12th), the agent fetches nothing for it: half a
    /// month beside whole ones reads as a drop. The curve ends at the latest complete month and so do the three lists.
    /// </summary>
    [Fact]
    public async Task FetchAsync_ForAMonthThatIsNotCompleteStopsAtTheLatestCompleteMonth()
    {
        var fake = SearchConsoleThrough(new DateOnly(2026, 7, 12));

        var fetch = await Agent(fake, new DateOnly(2026, 7, 15)).FetchAsync(Period.Parse("2026-07"), CancellationToken.None);

        var months = Months("2025-07", "2026-06").ToList();
        Assert.Equal(1 + (12 * 3) + 3, fake.Calls.Count);
        Assert.DoesNotContain(fake.Calls.Skip(1), call => Sent(call).StartDate!.StartsWith("2026-07", StringComparison.Ordinal));

        var lists = fake.Calls.Skip(1 + (12 * 3)).Select(Sent).ToList();
        Assert.All(lists, request => Assert.Equal(("2026-06-01", "2026-06-30"), (request.StartDate, request.EndDate)));
        Assert.Equal(new[] { "query", "page", "query,page" }, lists.Select(request => string.Join(",", request.Dimensions)));

        Assert.Equal("2026-06", fetch.LatestCompletePeriod);
        Assert.Equal(ExpectedRows(months), fetch.Rows);
        Assert.NotEmpty(fetch.Queries!);
    }

    /// <summary>
    /// Months before the 16-month window are not asked for (an answer would be empty, which cannot be told from zero clicks)
    /// and are listed as out of the window. The report month is inside the window, so its lists are fetched.
    /// </summary>
    [Fact]
    public async Task FetchAsync_SkipsMonthsBeforeTheWindowAndListsThem()
    {
        var fake = SearchConsoleThrough(new DateOnly(2026, 7, 12));

        var fetch = await Agent(fake, new DateOnly(2026, 7, 15)).FetchAsync(Period.Parse("2025-12"), CancellationToken.None);

        var months = Months("2025-04", "2025-12").ToList();
        Assert.Equal(1 + (9 * 3) + 3, fake.Calls.Count);
        Assert.Equal("2025-04", fetch.WindowStart);
        Assert.Equal(new[] { "2024-12", "2025-01", "2025-02", "2025-03" }, fetch.OutOfWindowPeriods);
        Assert.Equal(ExpectedRows(months), fetch.Rows);
        Assert.Equal(("2025-12-01", "2025-12-31"), (Sent(fake.Calls[^1]).StartDate, Sent(fake.Calls[^1]).EndDate));
    }

    /// <summary>A report month before the window asks the probe and nothing else: no month, no list, and every month of the curve is out.</summary>
    [Fact]
    public async Task FetchAsync_ForAMonthBeforeTheWindowAsksOnlyTheProbe()
    {
        var fake = SearchConsoleThrough(new DateOnly(2026, 7, 12));

        var fetch = await Agent(fake, new DateOnly(2026, 7, 15)).FetchAsync(Period.Parse("2024-01"), CancellationToken.None);

        Assert.Single(fake.Calls);
        Assert.Equal(new[] { "date" }, Sent(fake.Calls[0]).Dimensions);
        Assert.Empty(fetch.Rows);
        Assert.Empty(fetch.Queries!);
        Assert.Empty(fetch.Pages!);
        Assert.Empty(fetch.QueryPages!);
        Assert.Equal(13, fetch.OutOfWindowPeriods.Count);
        Assert.Equal("2023-01", fetch.OutOfWindowPeriods[0]);
        Assert.Equal("2024-01", fetch.OutOfWindowPeriods[^1]);
    }

    /// <summary>
    /// When the probe comes back without a date, the documented lag of three days stands in for the measured one (today minus
    /// three is 2026-06-29, before the month end, so June is not complete), and no data-through date is claimed.
    /// </summary>
    [Fact]
    public async Task FetchAsync_WhenTheProbeHasNoDatesTheDocumentedLagStandsIn()
    {
        var fake = new FakeSearchConsole(new FakeGoogleTransport(call => Sent(call).Dimensions?.Contains("date") == true
            ? Ok(new SearchAnalyticsQueryResponse())
            : Synthetic(new DateOnly(2026, 7, 2), call)));

        var fetch = await Agent(fake, new DateOnly(2026, 7, 2)).FetchAsync(Period.Parse("2026-06"), CancellationToken.None);

        Assert.Null(fetch.DataThrough);
        Assert.Equal("2026-05", fetch.LatestCompletePeriod);
        Assert.Equal(Months("2025-06", "2026-05").Count() * 3, fetch.Rows.Count);
        Assert.Equal("2026-05", fetch.Rows[^1].Period);
        Assert.Equal(("2026-05-01", "2026-05-31"), (Sent(fake.Calls[^1]).StartDate, Sent(fake.Calls[^1]).EndDate)); // the lists use the latest complete month
    }

    /// <summary>
    /// A month with no data (Review Focus 2): every call answers with nothing (the REST API sends <c>{}</c>, no rows at all).
    /// The fetch ends without an error and holds no row, no list entry and no total, so nothing downstream can divide by it.
    /// </summary>
    [Fact]
    public async Task FetchAsync_WhenSearchConsoleHasNoDataReturnsEmptyListsAndDoesNotThrow()
    {
        var fake = new FakeSearchConsole(); // every answer is {}

        var fetch = await Agent(fake, new DateOnly(2026, 7, 15)).FetchAsync(Period.Parse("2026-06"), CancellationToken.None);

        Assert.Equal(1 + (13 * 3) + 3, fake.Calls.Count);
        Assert.Empty(fetch.Rows);
        Assert.Empty(fetch.Queries!);
        Assert.Empty(fetch.Pages!);
        Assert.Empty(fetch.QueryPages!);
        Assert.Null(fetch.DataThrough);
        Assert.Equal("2026-06", fetch.LatestCompletePeriod);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The site, and what is checked before any call
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The property goes in the address of the call and the REST client escapes it (a domain property has a colon, a URL prefix
    /// has slashes); passing it already escaped would escape it twice.
    /// </summary>
    [Theory]
    [InlineData("sc-domain:example.com", "/sites/sc-domain%3Aexample.com/searchAnalytics/query")]
    [InlineData("https://www.example.com/", "/sites/https%3A%2F%2Fwww.example.com%2F/searchAnalytics/query")]
    public async Task FetchAsync_PutsTheSiteInTheAddressEscapedOnce(string site, string expectedPath)
    {
        var fake = SearchConsoleThrough(new DateOnly(2026, 7, 12));

        await Agent(fake, new DateOnly(2026, 7, 15), site).FetchAsync(Period.Parse("2024-01"), CancellationToken.None);

        var call = Assert.Single(fake.Calls);
        Assert.Contains(expectedPath, call.Uri.AbsoluteUri);
        Assert.StartsWith("/webmasters/v3/sites/", call.Uri.AbsolutePath);
        Assert.DoesNotContain("example.com", call.Body);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sc-domain:XXXXXXXX")]
    public async Task FetchAsync_RefusesAMissingSiteBeforeAskingForAnything(string site)
    {
        var fake = new FakeSearchConsole();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Agent(fake, new DateOnly(2026, 7, 15), site).FetchAsync(Period.Parse("2026-06"), CancellationToken.None));

        Assert.Contains("Esatto:SeoReport:SearchConsoleSite", error.Message);
        Assert.Contains("sc-domain:example.com", error.Message);
        Assert.Equal(0, fake.ServiceRequests);
        Assert.Empty(fake.Calls);
    }

    [Fact]
    public async Task FetchAsync_RefusesAnUnusableBrandPatternBeforeAskingForAnything()
    {
        var fake = new FakeSearchConsole();
        var today = new DateOnly(2026, 7, 15);
        var period = Period.Parse("2026-06");

        await Assert.ThrowsAsync<ArgumentException>(() => new GscAgent(Site, "(", fake, today).FetchAsync(period, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => new GscAgent(Site, "a(?=b)", fake, today).FetchAsync(period, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new GscAgent(Site, "", fake, today).FetchAsync(period, CancellationToken.None));

        Assert.Equal(0, fake.ServiceRequests);
        Assert.Empty(fake.Calls);
    }

    [Fact]
    public async Task FetchAsync_TakesThePatternBrandPatternBuildsForTermsWithABackslashAndADigit()
    {
        // Search Console only sees the brand pattern; the person typed terms. A term like a\1 or c:\2024 is escaped by BrandPattern
        // and must never stop every fetch as if it were a backreference.
        var fake = new FakeSearchConsole();
        var pattern = BrandPattern.Build(["a\\1", "c:\\2024"])!;

        await new GscAgent(Site, pattern, fake, new DateOnly(2026, 7, 15)).FetchAsync(Period.Parse("2026-06"), CancellationToken.None);

        Assert.NotEmpty(fake.Calls);
    }

    [Fact]
    public async Task FetchAsync_ChecksTheSiteBeforeTheBrandPattern()
    {
        var fake = new FakeSearchConsole();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new GscAgent("", "(", fake, new DateOnly(2026, 7, 15)).FetchAsync(Period.Parse("2026-06"), CancellationToken.None));
    }

    [Fact]
    public async Task TheDefaultTodayIsTheUtcDate()
    {
        var fake = new FakeSearchConsole();

        await new GscAgent(Site, BrandRegex, fake).FetchAsync(Period.Parse("2000-01"), CancellationToken.None);

        var probe = Sent(Assert.Single(fake.Calls));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.InRange(Date(probe.EndDate).DayNumber, today.AddDays(-1).DayNumber, today.AddDays(1).DayNumber); // not across midnight UTC
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Cancellation
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task FetchAsync_StopsWhenCancelled()
    {
        var fake = new FakeSearchConsole();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Agent(fake, new DateOnly(2026, 7, 15)).FetchAsync(Period.Parse("2026-06"), cts.Token));

        Assert.Empty(fake.Calls);
    }

    /// <summary>The token reaches every call, not only the first: cancelling while the probe is answered stops the run before the next call goes out.</summary>
    [Fact]
    public async Task FetchAsync_PassesTheTokenToEveryCall()
    {
        using var cts = new CancellationTokenSource();
        var fake = new FakeSearchConsole(new FakeGoogleTransport(call =>
        {
            cts.Cancel();
            return Synthetic(new DateOnly(2026, 7, 12), call);
        }));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Agent(fake, new DateOnly(2026, 7, 15)).FetchAsync(Period.Parse("2026-06"), cts.Token));

        Assert.Single(fake.Calls);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Failures
    // ---------------------------------------------------------------------------------------------------------------

    private static GscAgent AgentWithDelays(IGoogleClients clients, List<TimeSpan> delays)
        => new(Site, BrandRegex, clients, new DateOnly(2026, 7, 15), (delay, ct) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });

    private static readonly Period BeforeTheWindow = Period.Parse("2024-01"); // one call only: the probe

    /// <summary>A rate limit or a server error is tried again after 2, 4 and 8 seconds (waited through the injected delay), as in Nova.</summary>
    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "RESOURCE_EXHAUSTED")]
    [InlineData(HttpStatusCode.InternalServerError, "INTERNAL")]
    [InlineData(HttpStatusCode.BadGateway, "UNAVAILABLE")]
    public async Task FetchAsync_TriesATransientFailureAgain(HttpStatusCode status, string reason)
    {
        var transport = new FakeGoogleTransport();
        transport.Enqueue(status, ErrorJson((int)status, reason));
        var fake = new FakeSearchConsole(transport);
        var delays = new List<TimeSpan>();

        var fetch = await AgentWithDelays(fake, delays).FetchAsync(BeforeTheWindow, CancellationToken.None);

        Assert.Equal(2, fake.Calls.Count);
        Assert.Equal(new[] { TimeSpan.FromSeconds(2) }, delays);
        Assert.Empty(fetch.Rows);
    }

    [Fact]
    public async Task FetchAsync_GivesUpAfterFourAttemptsAndThrowsTheGoogleException()
    {
        var transport = new FakeGoogleTransport();
        for (var i = 0; i < 6; i++) transport.Enqueue(HttpStatusCode.InternalServerError, ErrorJson(500, "INTERNAL"));
        var fake = new FakeSearchConsole(transport);
        var delays = new List<TimeSpan>();

        var error = await Assert.ThrowsAsync<GoogleApiException>(() => AgentWithDelays(fake, delays).FetchAsync(BeforeTheWindow, CancellationToken.None));

        Assert.Equal(HttpStatusCode.InternalServerError, error.HttpStatusCode);
        Assert.Equal(4, fake.Calls.Count);
        Assert.Equal(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8) }, delays);
    }

    /// <summary>
    /// A failure that waiting cannot cure (no access, API disabled, property not found, bad request) is not tried again and
    /// reaches the caller unchanged, so the caller can map it to a code. That holds for the probe too: unlike Nova, the agent
    /// does not swallow a failed probe and carry on with a guessed lag, because what it fetches is archived for good.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "PERMISSION_DENIED")]
    [InlineData(HttpStatusCode.NotFound, "NOT_FOUND")]
    [InlineData(HttpStatusCode.BadRequest, "INVALID_ARGUMENT")]
    [InlineData(HttpStatusCode.Unauthorized, "UNAUTHENTICATED")]
    public async Task FetchAsync_DoesNotRetryOrSwallowAFailureThatWaitingCannotCure(HttpStatusCode status, string reason)
    {
        var transport = new FakeGoogleTransport();
        transport.Enqueue(status, ErrorJson((int)status, reason));
        var fake = new FakeSearchConsole(transport);
        var delays = new List<TimeSpan>();

        var error = await Assert.ThrowsAsync<GoogleApiException>(() => AgentWithDelays(fake, delays).FetchAsync(BeforeTheWindow, CancellationToken.None));

        Assert.Equal(status, error.HttpStatusCode);
        Assert.Single(fake.Calls);
        Assert.Empty(delays);
    }

    /// <summary>A month that cannot be fetched stops the run: no partial result is returned for the caller to archive.</summary>
    [Fact]
    public async Task FetchAsync_StopsAtAMonthThatFails()
    {
        var fake = new FakeSearchConsole(new FakeGoogleTransport(call => Sent(call).Dimensions?.Contains("date") == true
            ? Synthetic(new DateOnly(2026, 7, 12), call)
            : new FakeGoogleTransport.Answer(HttpStatusCode.Forbidden, ErrorJson(403, "PERMISSION_DENIED"))));

        var error = await Assert.ThrowsAsync<GoogleApiException>(
            () => Agent(fake, new DateOnly(2026, 7, 15)).FetchAsync(Period.Parse("2026-06"), CancellationToken.None));

        Assert.Equal(HttpStatusCode.Forbidden, error.HttpStatusCode);
        Assert.Equal(2, fake.Calls.Count); // the probe and the first month's first call
    }

    /// <summary>The caller's token goes to the wait between attempts too, so cancelling does not sit out a delay.</summary>
    [Fact]
    public async Task FetchAsync_PassesTheTokenToTheWaitBetweenAttempts()
    {
        var transport = new FakeGoogleTransport();
        transport.Enqueue(HttpStatusCode.TooManyRequests, ErrorJson(429, "RESOURCE_EXHAUSTED"));
        var fake = new FakeSearchConsole(transport);
        using var cts = new CancellationTokenSource();
        var seen = default(CancellationToken);
        var agent = new GscAgent(Site, BrandRegex, fake, new DateOnly(2026, 7, 15), (_, ct) =>
        {
            seen = ct;
            cts.Cancel();
            return Task.FromCanceled(ct);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => agent.FetchAsync(BeforeTheWindow, cts.Token));

        Assert.Equal(cts.Token, seen);
        Assert.Single(fake.Calls);
    }

    /// <summary>The service is asked for once per fetch, so a key that changes takes effect on the next fetch and not in the middle of one.</summary>
    [Fact]
    public async Task FetchAsync_AsksForTheServiceOncePerFetch()
    {
        var fake = SearchConsoleThrough(new DateOnly(2026, 7, 12));

        await Agent(fake, new DateOnly(2026, 7, 15)).FetchAsync(Period.Parse("2026-06"), CancellationToken.None);

        Assert.Equal(1, fake.ServiceRequests);
        Assert.True(fake.Calls.Count > 1);
    }
}
