using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Google.Apis.Json;
using Google.Apis.SearchConsole.v1.Data;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The Search Console agent against what Nova's agent produced. Search Console was already on Google's stable REST client in
/// Nova, so the request and response types are the same ones; what is compared is the behaviour. The expected values are either
/// golden files generated once by running Nova's own code on synthetic input (request shapes, date ranges, response mapping) or
/// literals pasted from one such run (the calendar rules), so "the port is faithful" is a comparison with the original, not with
/// what the port happens to do. Nothing in a fixture is real data. This half covers the pure parts; the half that talks to
/// Google's real request pipeline over a fake transport is in the other file of this class.
/// </summary>
public partial class GscAgentTests
{
    private const string Site = "sc-domain:example.com";

    // The pattern BrandPattern builds for the terms "acme" and "acme ab"; the golden requests were made with this literal.
    private const string BrandRegex = "(?i)(acme|acme ab)";

    private static readonly DateOnly July2026 = new(2026, 7, 1);

    private static DateOnly Date(string text) => DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------------------------------------------------------
    // The calendar rules: the 16-month window and the latest complete month. The literals below were produced once by
    // running Nova's GscAgent.EarliestAvailableMonth, DataThroughFrom and LatestCompleteMonth (golden-gen-gsc, outside both
    // repositories); nothing here is computed by the code under test.
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>A probe answer as Search Console gives it: one row per day, dates ascending in the first key.</summary>
    private static SearchAnalyticsQueryResponse ProbeThrough(DateOnly last)
    {
        var rows = new List<ApiDataRow>();
        for (var day = last.AddDays(-42); day <= last; day = day.AddDays(1))
        {
            rows.Add(DataRow(10, 100, 0.1, 8, Iso(day)));
        }

        return new SearchAnalyticsQueryResponse { Rows = rows };
    }

    /// <summary>
    /// The brief's five days (first of a month, mid-month, month end, 2026-03-01, 2026-12-31) plus the first of a year, a leap
    /// day and a day early in February. The probe for each trails by three days, as Search Console's final data does.
    /// </summary>
    [Theory]
    [InlineData("2026-07-01", "2025-04", "2026-06-28", "2026-05")]
    [InlineData("2026-07-15", "2025-04", "2026-07-12", "2026-06")]
    [InlineData("2026-07-31", "2025-04", "2026-07-28", "2026-06")]
    [InlineData("2026-03-01", "2024-12", "2026-02-26", "2026-01")]
    [InlineData("2026-12-31", "2025-09", "2026-12-28", "2026-11")]
    [InlineData("2026-01-01", "2024-10", "2025-12-29", "2025-11")]
    [InlineData("2028-02-29", "2026-11", "2028-02-26", "2028-01")]
    [InlineData("2026-02-03", "2024-11", "2026-01-31", "2026-01")]
    public void TheWindowAndTheLatestCompleteMonth_MatchNova(string today, string earliest, string dataThrough, string latestComplete)
    {
        var day = Date(today);

        Assert.Equal(Period.Parse(earliest), GscAgent.EarliestAvailableMonth(day));

        var through = GscAgent.DataThroughFrom(ProbeThrough(day.AddDays(-3)));
        Assert.Equal(Date(dataThrough), through);
        Assert.Equal(Period.Parse(latestComplete), GscAgent.LatestCompleteMonth(through!.Value));
    }

    /// <summary>A month is complete only when the data reaches its last day: the month end, leap day and year edges decide.</summary>
    [Theory]
    [InlineData("2026-06-30", "2026-06")]
    [InlineData("2026-07-01", "2026-06")]
    [InlineData("2026-07-31", "2026-07")]
    [InlineData("2026-06-29", "2026-05")]
    [InlineData("2028-02-28", "2028-01")]
    [InlineData("2028-02-29", "2028-02")]
    [InlineData("2026-02-28", "2026-02")]
    [InlineData("2026-12-31", "2026-12")]
    [InlineData("2026-01-01", "2025-12")]
    [InlineData("2026-01-31", "2026-01")]
    public void LatestCompleteMonth_MatchesNova(string dataThrough, string expected)
        => Assert.Equal(Period.Parse(expected), GscAgent.LatestCompleteMonth(Date(dataThrough)));

    [Theory]
    [InlineData("2026-07-15", "2025-04")]
    [InlineData("2026-01-31", "2024-10")]
    [InlineData("2024-02-29", "2022-11")]
    [InlineData("2026-12-01", "2025-09")]
    [InlineData("2026-05-31", "2025-02")]
    public void EarliestAvailableMonth_MatchesNova(string today, string expected)
        => Assert.Equal(Period.Parse(expected), GscAgent.EarliestAvailableMonth(Date(today)));

    [Fact]
    public void TheArchiveNeverReachesFartherBackThanSearchConsoleKeepsData()
    {
        Assert.Equal(16, GscAgent.RetentionMonths);
        Assert.Equal(12, GscAgent.WindowMonths);

        // The curve is the report month and twelve before it: thirteen months, the archive depth.
        Assert.Equal(13, GscAgent.WindowMonths + 1);
        Assert.True(GscAgent.WindowMonths + 1 <= GscAgent.RetentionMonths);
        Assert.Equal(3, GscAgent.FallbackLagDays);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The probe's answer: the last date
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void DataThroughFrom_IsTheLastParsableDateWhateverTheOrder()
    {
        var probe = new SearchAnalyticsQueryResponse
        {
            Rows =
            [
                DataRow(1, 1, 0.1, 1, "2026-06-30"),
                DataRow(1, 1, 0.1, 1, "junk"),
                DataRow(1, 1, 0.1, 1, "2026-06-12"),
                DataRow(1, 1, 0.1, 1, ""),
                new ApiDataRow { Clicks = 1 },
                DataRow(1, 1, 0.1, 1, "2026-06-29"),
            ],
        };

        Assert.Equal(new DateOnly(2026, 6, 30), GscAgent.DataThroughFrom(probe));
    }

    [Fact]
    public void DataThroughFrom_IsNullWhenThereIsNothingToRead()
    {
        Assert.Null(GscAgent.DataThroughFrom(null));
        Assert.Null(GscAgent.DataThroughFrom(new SearchAnalyticsQueryResponse())); // the REST client leaves rows null
        Assert.Null(GscAgent.DataThroughFrom(new SearchAnalyticsQueryResponse { Rows = [] }));
        Assert.Null(GscAgent.DataThroughFrom(new SearchAnalyticsQueryResponse { Rows = [DataRow(1, 1, 0.1, 1, "not a date")] }));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Comparing a request with Nova's golden
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The request as Google's client puts it on the wire: its own serializer, not ours. Nova's golden was written the same
    /// way, from Nova's request, which is the very same type, so the comparison needs NO canonicalization rule: both sides are
    /// parsed to <see cref="JsonNode"/> and compared with <see cref="JsonNode.DeepEquals"/>, which ignores the order of
    /// properties in an object (Newtonsoft writes them alphabetically) but not the order of arrays, which matters for
    /// dimensions, filter groups and filters. Numbers and strings are compared as they are.
    /// </summary>
    private static JsonNode OnTheWire(SearchAnalyticsQueryRequest request)
        => JsonNode.Parse(NewtonsoftJsonSerializer.Instance.Serialize(request))!;

    private static bool SameAsGolden(SearchAnalyticsQueryRequest request, string file) => JsonNode.DeepEquals(OnTheWire(request), Fixtures.Json(file));

    public static TheoryData<string> RequestKinds() => new()
    {
        "queries",
        "query-pages",
        "pages",
        "probe",
        "period-total",
        "period-brand",
        "period-non_brand",
    };

    /// <summary>The probe golden was made for this day, so a fetch on it asks for the same probe.</summary>
    private static readonly DateOnly ProbeDay = new(2026, 8, 15);

    private static SearchAnalyticsQueryRequest Build(string kind) => kind switch
    {
        "queries" => GscAgent.BuildQueriesRequest(July2026),
        "query-pages" => GscAgent.BuildQueryPagesRequest(July2026),
        "pages" => GscAgent.BuildPagesRequest(July2026),
        "probe" => GscAgent.BuildProbeRequest(ProbeDay),
        "period-total" => GscAgent.BuildPeriodRequest(July2026, GscAgent.SegmentTotal, BrandRegex),
        "period-brand" => GscAgent.BuildPeriodRequest(July2026, GscAgent.SegmentBrand, BrandRegex),
        "period-non_brand" => GscAgent.BuildPeriodRequest(July2026, GscAgent.SegmentNonBrand, BrandRegex),
        _ => throw new InvalidOperationException(kind),
    };

    [Theory]
    [MemberData(nameof(RequestKinds))]
    public void TheRequests_MatchNovaGolden(string kind)
    {
        var golden = Fixtures.Json($"gsc-request-golden-{kind}.json");
        var actual = OnTheWire(Build(kind));

        Assert.True(JsonNode.DeepEquals(actual, golden), $"expected {golden.ToJsonString()}{Environment.NewLine}but was  {actual.ToJsonString()}");
    }

    /// <summary>
    /// The comparison above has to be able to fail. Each change here is one a careless edit could make (a dimension, a date, a
    /// row limit, the data state, the regex, a filter operator, the filter itself), and none may still match Nova's request.
    /// </summary>
    [Theory]
    [InlineData("a dimension is renamed", "queries")]
    [InlineData("the range starts a day late", "queries")]
    [InlineData("the range ends a day early", "queries")]
    [InlineData("the row limit changes", "queries")]
    [InlineData("the data state is all", "queries")]
    [InlineData("the dimensions swap places", "query-pages")]
    [InlineData("a dimension is dropped", "query-pages")]
    [InlineData("the row limit changes", "pages")]
    [InlineData("the probe window is one day shorter", "probe")]
    [InlineData("the row limit changes", "probe")]
    [InlineData("a dimension is renamed", "probe")]
    [InlineData("the regex changes", "period-brand")]
    [InlineData("the filter operator is swapped", "period-brand")]
    [InlineData("the filter operator is swapped", "period-non_brand")]
    [InlineData("the filter is dropped", "period-brand")]
    [InlineData("the filter dimension changes", "period-non_brand")]
    [InlineData("the group type changes", "period-brand")]
    [InlineData("the row limit changes", "period-brand")]
    [InlineData("a dimension appears", "period-total")]
    [InlineData("a filter appears", "period-total")]
    public void TheGoldenComparison_FailsForAChangedRequest(string change, string kind)
    {
        var request = Build(kind);
        var file = $"gsc-request-golden-{kind}.json";
        Assert.True(SameAsGolden(request, file), "the unchanged request must match, or this test proves nothing");

        switch (change)
        {
            case "a dimension is renamed": request.Dimensions[0] = "page"; break;
            case "the range starts a day late": request.StartDate = "2026-07-02"; break;
            case "the range ends a day early": request.EndDate = "2026-07-30"; break;
            case "the row limit changes": request.RowLimit += 1; break;
            case "the data state is all": request.DataState = "all"; break;
            case "the dimensions swap places": (request.Dimensions[0], request.Dimensions[1]) = (request.Dimensions[1], request.Dimensions[0]); break;
            case "a dimension is dropped": request.Dimensions.RemoveAt(1); break;
            case "the probe window is one day shorter": request.StartDate = Iso(Date(request.StartDate).AddDays(1)); break;
            case "the regex changes": request.DimensionFilterGroups[0].Filters[0].Expression = "(?i)(acme)"; break;
            case "the filter operator is swapped":
                var filter = request.DimensionFilterGroups[0].Filters[0];
                filter.Operator__ = filter.Operator__ == "includingRegex" ? "excludingRegex" : "includingRegex";
                break;
            case "the filter is dropped": request.DimensionFilterGroups = null; break;
            case "the filter dimension changes": request.DimensionFilterGroups[0].Filters[0].Dimension = "page"; break;
            case "the group type changes": request.DimensionFilterGroups[0].GroupType = "or"; break;
            case "a dimension appears": request.Dimensions = ["query"]; break;
            case "a filter appears":
                request.DimensionFilterGroups =
                [
                    new ApiDimensionFilterGroup
                    {
                        GroupType = "and",
                        Filters = [new ApiDimensionFilter { Dimension = "query", Operator__ = "includingRegex", Expression = BrandRegex }],
                    },
                ];
                break;
            default: throw new InvalidOperationException(change);
        }

        Assert.False(SameAsGolden(request, file), $"a change ({change}) was not noticed");
    }

    /// <summary>
    /// The brand split asks Search Console for the <c>query</c> dimension filtered with a regular expression:
    /// <c>includingRegex</c> for brand, <c>excludingRegex</c> for non-brand, the <c>(?i)(...)</c> pattern passed on as it is. (The
    /// brief calls this "queryRegex"; Search Console has no dimension of that name, the REST filter is dimension
    /// <c>query</c> plus one of these two operators, and Nova's golden says the same.) The total has no filter at all.
    /// </summary>
    [Fact]
    public void TheBrandSplit_FiltersTheQueryDimensionWithThePatternBrandPatternBuilds()
    {
        var pattern = BrandPattern.Build(["acme", "acme ab"]);
        Assert.Equal(BrandRegex, pattern);

        var brand = GscAgent.BuildPeriodRequest(July2026, GscAgent.SegmentBrand, pattern!);
        var nonBrand = GscAgent.BuildPeriodRequest(July2026, GscAgent.SegmentNonBrand, pattern!);
        var total = GscAgent.BuildPeriodRequest(July2026, GscAgent.SegmentTotal, pattern!);

        var brandFilter = Assert.Single(Assert.Single(brand.DimensionFilterGroups).Filters);
        Assert.Equal(("query", "includingRegex", pattern), (brandFilter.Dimension, brandFilter.Operator__, brandFilter.Expression));

        var nonBrandFilter = Assert.Single(Assert.Single(nonBrand.DimensionFilterGroups).Filters);
        Assert.Equal(("query", "excludingRegex", pattern), (nonBrandFilter.Dimension, nonBrandFilter.Operator__, nonBrandFilter.Expression));

        Assert.Null(total.DimensionFilterGroups);
    }

    [Fact]
    public void AnUnknownSegment_IsAnUnfilteredRequestLikeTheTotal()
    {
        var unknown = GscAgent.BuildPeriodRequest(July2026, "nonsense", BrandRegex);

        Assert.True(SameAsGolden(unknown, "gsc-request-golden-period-total.json"));
    }

    public static TheoryData<string> RangeMonths()
    {
        var data = new TheoryData<string>();
        foreach (var (month, _) in Fixtures.Json("gsc-daterange-golden.json")["months"]!.AsObject()) data.Add(month);
        return data;
    }

    /// <summary>
    /// The first and the last day of the month over the months of Nova's run: July 2026, a leap February, an ordinary February,
    /// a December and a January. Every monthly request shares the range, whichever dimensions it asks for.
    /// </summary>
    [Theory]
    [MemberData(nameof(RangeMonths))]
    public void TheMonthlyRequests_CoverTheWholeMonthLikeNova(string month)
    {
        var golden = Fixtures.Json("gsc-daterange-golden.json")["months"]![month]!;
        var first = new DateOnly(int.Parse(month[..4]), int.Parse(month[5..]), 1);

        foreach (var request in new[]
        {
            GscAgent.BuildQueriesRequest(first),
            GscAgent.BuildQueryPagesRequest(first),
            GscAgent.BuildPagesRequest(first),
            GscAgent.BuildPeriodRequest(first, GscAgent.SegmentTotal, BrandRegex),
            GscAgent.BuildPeriodRequest(first, GscAgent.SegmentBrand, BrandRegex),
        })
        {
            Assert.Equal((golden["start"]!.GetValue<string>(), golden["end"]!.GetValue<string>()), (request.StartDate, request.EndDate));
        }
    }

    [Fact]
    public void TheMonthlyRequests_AcceptAnyDayOfTheMonth()
    {
        var request = GscAgent.BuildQueriesRequest(new DateOnly(2026, 7, 19));

        Assert.Equal(("2026-07-01", "2026-07-31"), (request.StartDate, request.EndDate));
    }

    public static TheoryData<string> ProbeDays()
    {
        var data = new TheoryData<string>();
        foreach (var (today, _) in Fixtures.Json("gsc-daterange-golden.json")["probes"]!.AsObject()) data.Add(today);
        return data;
    }

    /// <summary>The probe looks 45 days back from today, a window that crosses month and year boundaries.</summary>
    [Theory]
    [MemberData(nameof(ProbeDays))]
    public void TheProbe_Covers45DaysUpToTodayLikeNova(string today)
    {
        var golden = Fixtures.Json("gsc-daterange-golden.json")["probes"]![today]!;

        var request = GscAgent.BuildProbeRequest(Date(today));

        Assert.Equal((golden["start"]!.GetValue<string>(), golden["end"]!.GetValue<string>()), (request.StartDate, request.EndDate));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The brand pattern preflight
    // ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("(?i)(acme|acme ab)")]
    [InlineData("acme")]
    [InlineData("(?i)(c\\+\\+|a\\.b)")]
    [InlineData("^acme$")]
    public void ValidateBrandRegex_AcceptsAUsablePattern(string pattern) => GscAgent.ValidateBrandRegex(pattern);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateBrandRegex_RefusesAMissingPattern(string? pattern)
    {
        var error = Assert.Throws<InvalidOperationException>(() => GscAgent.ValidateBrandRegex(pattern));

        Assert.Contains("Esatto:SeoReport:BrandTerms", error.Message);
    }

    [Fact]
    public void ValidateBrandRegex_RefusesAnOpenGroupWithAFixedSentence()
    {
        var error = Assert.Throws<ArgumentException>(() => GscAgent.ValidateBrandRegex("("));

        Assert.Equal("brandRegex", error.ParamName);
        Assert.Contains("not a valid regular expression", error.Message);
        Assert.Null(error.InnerException); // the parser's own error repeats the pattern ("Invalid pattern '(' at offset 1")
        Assert.DoesNotContain("offset", error.Message);
        Assert.DoesNotContain("Invalid pattern", error.Message);
    }

    /// <summary>
    /// Whatever is wrong with a pattern, the message is a fixed sentence: it never carries any of the pattern's text, so a
    /// brand term that somebody typed cannot travel into a log or a response through the exception.
    /// </summary>
    [Theory]
    [InlineData("LEAK-MARKER-7c1d(")]
    [InlineData("LEAK-MARKER-7c1d)")]
    [InlineData("LEAK-MARKER-7c1d[a")]
    [InlineData("LEAK-MARKER-7c1d{2,1}")]
    [InlineData("*LEAK-MARKER-7c1d")]
    [InlineData("LEAK-MARKER-7c1d(?=x)")]
    [InlineData("LEAK-MARKER-7c1d(?!x)")]
    [InlineData("(?<=x)LEAK-MARKER-7c1d")]
    [InlineData("(?<!x)LEAK-MARKER-7c1d")]
    [InlineData("(LEAK-MARKER-7c1d)\\1")]
    public void ValidateBrandRegex_NeverRepeatsThePatternInItsMessage(string pattern)
    {
        var error = Assert.Throws<ArgumentException>(() => GscAgent.ValidateBrandRegex(pattern));

        Assert.DoesNotContain("LEAK-MARKER", error.Message);
        Assert.DoesNotContain("LEAK-MARKER", error.ToString());
        Assert.Null(error.InnerException);
    }

    /// <summary>Search Console's RE2 has no lookaround and no backreferences; each gets its own sentence, so the person knows what to remove.</summary>
    [Theory]
    [InlineData("a(?=b)", "lookahead")]
    [InlineData("a(?!b)", "lookahead")]
    [InlineData("(?<=a)b", "lookahead")]
    [InlineData("(?<!a)b", "lookahead")]
    [InlineData("(a)\\1", "backreference")]
    [InlineData("(a)(b)\\2", "backreference")]
    [InlineData("(a)\\\\\\1", "backreference")]
    public void ValidateBrandRegex_NamesWhatRE2CannotDo(string pattern, string word)
    {
        var error = Assert.Throws<ArgumentException>(() => GscAgent.ValidateBrandRegex(pattern));

        Assert.Contains(word, error.Message);
    }

    public static TheoryData<string[]> TermLists() => new()
    {
        new[] { "acme" },
        new[] { "acme", "acme ab" },
        new[] { "a+b", "c#", "x (y)", "[z]", "q.r", "50%", "a|b", "a-b", "$$", "^up", "{curly}", "what?", "star*" },
        new[] { "ångström", "Åre Fjäll", "Öresund" },
        new[] { "tab\there", "back\\slash", "quote's \"double\"" },
        new[] { "a\\1", "c:\\2024", "x\\\\9" },
    };

    /// <summary>A backslash followed by a digit is a backreference only when the backslash is not itself escaped.</summary>
    [Theory]
    [InlineData("a\\\\1")]
    [InlineData("(?i)(c:\\\\2024)")]
    [InlineData("a\\\\\\\\1")]
    public void ValidateBrandRegex_AcceptsAnEscapedBackslashBeforeADigit(string pattern) => GscAgent.ValidateBrandRegex(pattern);

    /// <summary>
    /// Every pattern <see cref="BrandPattern"/> builds, whatever characters the terms hold, passes the preflight: the two are
    /// meant to be used together, and a term that makes the preflight refuse would stop a fetch for no good reason.
    /// </summary>
    [Theory]
    [MemberData(nameof(TermLists))]
    public void ValidateBrandRegex_AcceptsEveryPatternBrandPatternBuilds(string[] terms)
    {
        var pattern = BrandPattern.Build(terms);

        Assert.NotNull(pattern);
        GscAgent.ValidateBrandRegex(pattern);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Mapping responses
    // ---------------------------------------------------------------------------------------------------------------

    private static readonly JsonSerializerOptions Camel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static ApiDataRow DataRow(double clicks, double impressions, double ctr, double position, params string[] keys)
        => new() { Keys = keys.Length == 0 ? null : keys, Clicks = clicks, Impressions = impressions, Ctr = ctr, Position = position };

    private static SearchAnalyticsQueryResponse Rows(params ApiDataRow[] rows) => new() { Rows = rows };

    private static SearchAnalyticsQueryResponse? ParseResponse(JsonNode? response)
        => response is null ? null : NewtonsoftJsonSerializer.Instance.Deserialize<SearchAnalyticsQueryResponse>(response.ToJsonString());

    public static TheoryData<string> MappingCases()
    {
        var data = new TheoryData<string>();
        foreach (var node in Fixtures.Json("gsc-mapping-golden.json").AsArray()) data.Add(node!["name"]!.GetValue<string>());
        return data;
    }

    /// <summary>The fetch as JSON, the way the golden was written from Nova's own records.</summary>
    private static JsonNode AsJson(GscFetch fetch) => JsonNode.Parse(JsonSerializer.Serialize(fetch, Camel))!;

    /// <summary>
    /// The same synthetic responses Nova's <c>MapRaw</c> was run on, written the way Google's REST API answers and read by the
    /// REST client's own deserializer (so a missing <c>rows</c> or <c>keys</c> is a null list, as it is for real). Covers what
    /// the original does in odd cases: rounding half to even, missing measures count as zero, an empty answer is no row (never
    /// a row of zeros), rows without a usable key are skipped, the API's order is kept, the documented lag stands in when the
    /// probe says nothing, and the months before the 16-month window are listed.
    /// </summary>
    [Theory]
    [MemberData(nameof(MappingCases))]
    public void MapResponse_MatchesNova(string name)
    {
        var golden = Fixtures.Json("gsc-mapping-golden.json").AsArray().Single(n => n!["name"]!.GetValue<string>() == name)!;

        var periods = golden["periods"]!.AsArray().Select(p => new GscPeriodResponse(
            Period.Parse(p!["period"]!.GetValue<string>()),
            p["segment"]!.GetValue<string>(),
            ParseResponse(p["response"]))).ToList();
        var responses = new GscResponses(
            ParseResponse(golden["probe"]),
            periods,
            ParseResponse(golden["queries"]),
            ParseResponse(golden["pages"]),
            ParseResponse(golden["queryPages"]));

        var fetch = GscAgent.MapResponse(Date(golden["today"]!.GetValue<string>()), Period.Parse(golden["reportPeriod"]!.GetValue<string>()), responses);

        var expected = golden["expected"]!;
        var actual = AsJson(fetch);
        Assert.True(JsonNode.DeepEquals(actual, expected), $"expected {expected.ToJsonString()}{Environment.NewLine}but was  {actual.ToJsonString()}");
    }

    private static void AssertFetch(GscFetch expected, GscFetch actual)
    {
        Assert.Equal(expected.Rows, actual.Rows);
        Assert.Equal(expected.DataThrough, actual.DataThrough);
        Assert.Equal(expected.LatestCompletePeriod, actual.LatestCompletePeriod);
        Assert.Equal(expected.WindowStart, actual.WindowStart);
        Assert.Equal(expected.OutOfWindowPeriods, actual.OutOfWindowPeriods);
        Assert.Equal(expected.Queries, actual.Queries);
        Assert.Equal(expected.Pages, actual.Pages);
        Assert.Equal(expected.QueryPages, actual.QueryPages);
    }

    /// <summary>
    /// Responses built by hand, and the expected <see cref="GscFetch"/> written out in full, so a wrong column, rounding,
    /// period, window or ordering shows up here without reading a golden file.
    /// </summary>
    [Fact]
    public void MapResponse_FromHandBuiltResponses_YieldsTheExactFetch()
    {
        var responses = new GscResponses(
            Probe: Rows(DataRow(1, 1, 0.1, 1, "2026-07-10"), DataRow(1, 1, 0.1, 1, "2026-07-12"), DataRow(1, 1, 0.1, 1, "2026-07-11")),
            Periods:
            [
                new GscPeriodResponse(Period.Parse("2026-05"), "total", Rows(DataRow(1200.4, 52000.6, 0.0231, 14.37))),
                new GscPeriodResponse(Period.Parse("2026-05"), "brand", Rows(DataRow(800, 9000, 0.0889, 1.9))),
                new GscPeriodResponse(Period.Parse("2026-05"), "non_brand", new SearchAnalyticsQueryResponse()),
                new GscPeriodResponse(Period.Parse("2026-06"), "total", Rows(DataRow(2.5, 3.5, 0.5, 1.5))),
                new GscPeriodResponse(Period.Parse("2026-06"), "brand", Rows(DataRow(0, 0, 0, 0))),
                new GscPeriodResponse(Period.Parse("2026-06"), "non_brand", null),
            ],
            Queries: Rows(DataRow(700, 5000, 0.14, 1.2, "acme"), DataRow(120.6, 9000, 0.0133, 6.55, "example widget")),
            Pages: Rows(DataRow(500, 8000, 0.0625, 2.1, "https://www.example.com/"), DataRow(250, 30000, 0.0083, 9.4, "https://www.example.com/widgets/")),
            QueryPages: Rows(DataRow(650, 4000, 0.1625, 1.1, "acme", "https://www.example.com/"), DataRow(100, 7000, 0.0143, 6.2, "example widget", "https://www.example.com/widgets/")));

        var fetch = GscAgent.MapResponse(new DateOnly(2026, 7, 15), Period.Parse("2026-06"), responses);

        AssertFetch(
            new GscFetch(
                Rows:
                [
                    new GscRow("2026-05", "total", 1200, 52001, 0.0231, 14.37),
                    new GscRow("2026-05", "brand", 800, 9000, 0.0889, 1.9),

                    // no row for 2026-05 non_brand (an empty answer is not a measured zero), and none for the null answer
                    new GscRow("2026-06", "total", 2, 4, 0.5, 1.5),         // half to even: 2.5 -> 2, 3.5 -> 4
                    new GscRow("2026-06", "brand", 0, 0, 0, 0),             // a measured zero stays a row
                ],
                DataThrough: "2026-07-12",
                LatestCompletePeriod: "2026-06",
                WindowStart: "2025-04",
                OutOfWindowPeriods: [],
                Queries: [new GscQueryRow("acme", 700, 5000, 0.14, 1.2), new GscQueryRow("example widget", 121, 9000, 0.0133, 6.55)],
                Pages: [new GscPageRow("https://www.example.com/", 500, 8000, 0.0625, 2.1), new GscPageRow("https://www.example.com/widgets/", 250, 30000, 0.0083, 9.4)],
                QueryPages:
                [
                    new GscQueryPageRow("acme", "https://www.example.com/", 650, 4000, 0.1625, 1.1),
                    new GscQueryPageRow("example widget", "https://www.example.com/widgets/", 100, 7000, 0.0143, 6.2),
                ]),
            fetch);
    }

    /// <summary>
    /// A month with no data (Review Focus 2). Google's REST API leaves <c>rows</c> out of an empty answer, so the list is null and
    /// not empty. Every part of the fetch must read that as "nothing": empty lists and no totals, never an exception and never a
    /// row of zeros.
    /// </summary>
    [Fact]
    public void MapResponse_ForAMonthWithNoDataThrowsNothingAndMapsToEmptyLists()
    {
        Assert.Null(new SearchAnalyticsQueryResponse().Rows);

        var nothing = new SearchAnalyticsQueryResponse();
        var fetch = GscAgent.MapResponse(
            new DateOnly(2026, 7, 15),
            Period.Parse("2026-06"),
            new GscResponses(
                nothing,
                [
                    new GscPeriodResponse(Period.Parse("2026-06"), "total", nothing),
                    new GscPeriodResponse(Period.Parse("2026-06"), "brand", new SearchAnalyticsQueryResponse { Rows = [] }),
                    new GscPeriodResponse(Period.Parse("2026-06"), "non_brand", null),
                ],
                nothing,
                new SearchAnalyticsQueryResponse { Rows = [] },
                nothing));

        Assert.Empty(fetch.Rows);
        Assert.Empty(fetch.Queries!);
        Assert.Empty(fetch.Pages!);
        Assert.Empty(fetch.QueryPages!);
        Assert.Null(fetch.DataThrough);
        Assert.Equal("2026-06", fetch.LatestCompletePeriod); // the documented lag stands in: 2026-07-12 is not the month end
        Assert.Empty(fetch.OutOfWindowPeriods);
    }

    [Fact]
    public void MapResponse_WhenNoCallWasMadeAtAllReturnsEmptyListsAndNotNulls()
    {
        var fetch = GscAgent.MapResponse(new DateOnly(2026, 7, 15), Period.Parse("2024-01"), new GscResponses(null, []));

        Assert.Empty(fetch.Rows);
        Assert.NotNull(fetch.Queries);
        Assert.NotNull(fetch.Pages);
        Assert.NotNull(fetch.QueryPages);
        Assert.Empty(fetch.Queries);
        Assert.Equal(13, fetch.OutOfWindowPeriods.Count); // the whole curve is before the window
    }

    /// <summary>
    /// A row whose key list is null, empty, too short or blank is skipped, like Nova does, and never reaches an index: the
    /// dimension value is the row's identity, so a row without one cannot be a query, a page or a pair.
    /// </summary>
    [Fact]
    public void MapResponse_SkipsRowsWithoutTheKeysTheRequestAskedFor()
    {
        var withoutKeys = new ApiDataRow { Clicks = 1, Impressions = 1, Ctr = 0.1, Position = 1 }; // Keys is null

        var fetch = GscAgent.MapResponse(
            new DateOnly(2026, 7, 15),
            Period.Parse("2026-06"),
            new GscResponses(
                null,
                [],
                Queries: Rows(withoutKeys, new ApiDataRow { Keys = [], Clicks = 2 }, DataRow(3, 3, 0.1, 3, ""), DataRow(4, 4, 0.1, 4, "  "), DataRow(5, 5, 0.1, 5, "kept")),
                Pages: Rows(withoutKeys, DataRow(3, 3, 0.1, 3, ""), DataRow(6, 6, 0.1, 6, "https://www.example.com/kept")),
                QueryPages: Rows(
                    withoutKeys,
                    DataRow(2, 2, 0.1, 2, "only a query"),
                    DataRow(3, 3, 0.1, 3, "", "https://www.example.com/x"),
                    DataRow(4, 4, 0.1, 4, "a query", ""),
                    DataRow(7, 7, 0.1, 7, "kept", "https://www.example.com/k"))));

        Assert.Equal(new[] { new GscQueryRow("kept", 5, 5, 0.1, 5) }, fetch.Queries);
        Assert.Equal(new[] { new GscPageRow("https://www.example.com/kept", 6, 6, 0.1, 6) }, fetch.Pages);
        Assert.Equal(new[] { new GscQueryPageRow("kept", "https://www.example.com/k", 7, 7, 0.1, 7) }, fetch.QueryPages);
    }

    [Fact]
    public void MapResponse_UsesTheFirstRowOfAMonthAndSegment()
    {
        var response = Rows(DataRow(5, 50, 0.1, 4), DataRow(99, 999, 0.9, 9));

        var fetch = GscAgent.MapResponse(
            new DateOnly(2026, 7, 15),
            Period.Parse("2026-06"),
            new GscResponses(null, [new GscPeriodResponse(Period.Parse("2026-06"), "total", response)]));

        Assert.Equal(new[] { new GscRow("2026-06", "total", 5, 50, 0.1, 4) }, fetch.Rows);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The records carry no prose
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The server never sends prose, so no property of any of the records may be free text. Nova's records had none either, and
    /// this pins it: the only strings are periods, segment codes, dates, queries and addresses that came from Search Console.
    /// </summary>
    [Fact]
    public void TheRecordsHoldNoTextOfTheirOwn()
    {
        var names = new[] { typeof(GscRow), typeof(GscFetch), typeof(GscQueryRow), typeof(GscQueryPageRow), typeof(GscPageRow) }
            .SelectMany(t => t.GetProperties().Where(p => p.PropertyType == typeof(string)).Select(p => $"{t.Name}.{p.Name}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "GscFetch.DataThrough",
                "GscFetch.LatestCompletePeriod",
                "GscFetch.WindowStart",
                "GscPageRow.Url",
                "GscQueryPageRow.Query",
                "GscQueryPageRow.Url",
                "GscQueryRow.Query",
                "GscRow.Period",
                "GscRow.Segment",
            ],
            names);
    }
}
