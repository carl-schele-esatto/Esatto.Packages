using System.Text.Json.Nodes;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Google.Apis.AnalyticsData.v1beta.Data;
using Google.Apis.Json;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The GA4 agents on Google's stable REST client against what Nova's gRPC agents produced. The expected values are golden
/// files generated once by running Nova's own code on synthetic input (never real data), so "the port is faithful" is a
/// comparison with the original, not with what the port happens to do. This part covers the monthly agent and the shared
/// helpers; the daily agent is in the other half of this class.
/// </summary>
public partial class Ga4AgentTests
{
    private const string Property = "properties/123456789";

    private static readonly DateOnly July2026 = new(2026, 7, 1);

    // ---------------------------------------------------------------------------------------------------------------
    // Comparing a REST request with Nova's protobuf-JSON golden
    // ---------------------------------------------------------------------------------------------------------------

    /// <summary>The request as Google's client puts it on the wire: its own serializer, not ours.</summary>
    private static JsonObject OnTheWire(RunReportRequest request)
        => JsonNode.Parse(NewtonsoftJsonSerializer.Instance.Serialize(request))!.AsObject();

    /// <summary>
    /// Nova's golden request, brought to the REST client's spelling. Exactly one rule, because that is the only difference the
    /// two serializers have for these requests (protobuf JSON versus Newtonsoft over the REST types):
    /// <list type="number">
    /// <item><c>limit</c> is a 64-bit integer: protobuf JSON writes it as a string (<c>"200"</c>), the REST client as a number
    /// (<c>200</c>). The value is the same; the REST API accepts a number.</item>
    /// </list>
    /// Everything else already agrees: names are camelCase in both, the enum <c>matchType</c> is the string <c>EXACT</c> in
    /// both, both leave defaults and unset fields out, and <see cref="JsonNode.DeepEquals"/> ignores property order in objects
    /// (but not the order of arrays, which matters for dimensions, metrics, filters and ordering).
    /// </summary>
    private static JsonObject Golden(string file)
    {
        var golden = Fixtures.Json(file).AsObject();
        if (golden["limit"] is JsonValue limit && limit.TryGetValue<string>(out var text)) golden["limit"] = long.Parse(text);
        return golden;
    }

    /// <summary>
    /// The golden as it goes out on the wire: without <c>property</c>. Google documents the property as given in the address of the
    /// call and not in the body, so the agent moves it to the address; the golden of the built request keeps it, this one does not.
    /// </summary>
    private static JsonObject WireGolden(string file)
    {
        var golden = Golden(file);
        golden.Remove("property");
        return golden;
    }

    private static bool SameAsGolden(RunReportRequest request, string file) => JsonNode.DeepEquals(OnTheWire(request), Golden(file));

    public static TheoryData<string> Segments() => new()
    {
        Ga4Agent.SegmentOrganic,
        Ga4Agent.SegmentTotal,
        Ga4Agent.SegmentOrganicGoogle,
    };

    [Theory]
    [MemberData(nameof(Segments))]
    public void BuildRequest_MatchesNovaGolden(string segment)
    {
        var request = Ga4Agent.BuildRequest(Property, July2026, segment);

        var golden = Golden($"ga4-request-golden-{segment}.json");
        var actual = OnTheWire(request);
        Assert.True(JsonNode.DeepEquals(actual, golden), $"expected {golden.ToJsonString()}{Environment.NewLine}but was  {actual.ToJsonString()}");
    }

    /// <summary>
    /// The comparison above has to be able to fail. Each change here is one a careless edit could make, and none may still
    /// match Nova's request: a dimension, a metric (changed or dropped), the date range, a filter value (changed or dropped),
    /// the limit, the ordering, the property.
    /// </summary>
    [Theory]
    [InlineData("a dimension is renamed", Ga4Agent.SegmentOrganicGoogle)]
    [InlineData("a metric is renamed", Ga4Agent.SegmentOrganicGoogle)]
    [InlineData("a metric is dropped", Ga4Agent.SegmentOrganicGoogle)]
    [InlineData("the metrics swap places", Ga4Agent.SegmentOrganicGoogle)]
    [InlineData("the date range starts a day late", Ga4Agent.SegmentOrganicGoogle)]
    [InlineData("the date range ends a day early", Ga4Agent.SegmentOrganicGoogle)]
    [InlineData("the channel group value changes", Ga4Agent.SegmentOrganicGoogle)]
    [InlineData("the source value changes", Ga4Agent.SegmentOrganicGoogle)]
    [InlineData("the match type changes", Ga4Agent.SegmentOrganic)]
    [InlineData("the filter is dropped", Ga4Agent.SegmentOrganic)]
    [InlineData("a filter appears on the total", Ga4Agent.SegmentTotal)]
    [InlineData("the limit changes", Ga4Agent.SegmentTotal)]
    [InlineData("the ordering is dropped", Ga4Agent.SegmentTotal)]
    [InlineData("the property changes", Ga4Agent.SegmentTotal)]
    public void The_golden_comparison_fails_for_a_changed_request(string change, string segment)
    {
        var request = Ga4Agent.BuildRequest(Property, July2026, segment);
        Assert.True(SameAsGolden(request, $"ga4-request-golden-{segment}.json"), "the unchanged request must match, or this test proves nothing");

        switch (change)
        {
            case "a dimension is renamed": request.Dimensions[0].Name = "date"; break;
            case "a metric is renamed": request.Metrics[1].Name = "engagedSessions"; break;
            case "a metric is dropped": request.Metrics.RemoveAt(3); break;
            case "the metrics swap places": (request.Metrics[0], request.Metrics[1]) = (request.Metrics[1], request.Metrics[0]); break;
            case "the date range starts a day late": request.DateRanges[0].StartDate = "2025-01-02"; break;
            case "the date range ends a day early": request.DateRanges[0].EndDate = "2026-07-30"; break;
            case "the channel group value changes": request.DimensionFilter.AndGroup.Expressions[0].Filter.StringFilter.Value = "Paid Search"; break;
            case "the source value changes": request.DimensionFilter.AndGroup.Expressions[1].Filter.StringFilter.Value = "bing"; break;
            case "the match type changes": request.DimensionFilter.Filter.StringFilter.MatchType = "CONTAINS"; break;
            case "the filter is dropped": request.DimensionFilter = null; break;
            case "a filter appears on the total":
                request.DimensionFilter = new FilterExpression { Filter = new Filter { FieldName = "sessionSource", StringFilter = new StringFilter { MatchType = "EXACT", Value = "google" } } };
                break;
            case "the limit changes": request.Limit = 201; break;
            case "the ordering is dropped": request.OrderBys = null; break;
            case "the property changes": request.Property = "properties/1"; break;
            default: throw new InvalidOperationException(change);
        }

        Assert.False(SameAsGolden(request, $"ga4-request-golden-{segment}.json"), $"a change ({change}) was not noticed");
    }

    [Fact]
    public void An_unknown_segment_is_an_unfiltered_request_like_the_total()
    {
        var unknown = Ga4Agent.BuildRequest(Property, July2026, "nonsense");

        Assert.True(SameAsGolden(unknown, $"ga4-request-golden-{Ga4Agent.SegmentTotal}.json"));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Date ranges
    // ---------------------------------------------------------------------------------------------------------------

    public static TheoryData<string> RangeMonths()
    {
        var data = new TheoryData<string>();
        foreach (var (month, _) in Fixtures.Json("ga4-daterange-golden.json").AsObject()) data.Add(month);
        return data;
    }

    /// <summary>
    /// <c>DateRangeFor</c> over the same months as Nova's: July 2026 (the brief's example), a leap February, a December and a
    /// January. Both overloads, the <see cref="Period"/> one the package uses and the <see cref="DateOnly"/> one Nova had.
    /// </summary>
    [Theory]
    [MemberData(nameof(RangeMonths))]
    public void DateRangeFor_MatchesNova(string month)
    {
        var golden = Fixtures.Json("ga4-daterange-golden.json")[month]!;
        var expected = (DateOnly.ParseExact(golden["start"]!.GetValue<string>(), "yyyy-MM-dd"), DateOnly.ParseExact(golden["end"]!.GetValue<string>(), "yyyy-MM-dd"));

        Assert.Equal(expected, Ga4Agent.DateRangeFor(Period.Parse(month)));
        Assert.Equal(expected, Ga4Agent.DateRangeFor(new DateOnly(int.Parse(month[..4]), int.Parse(month[5..]), 1)));
    }

    [Fact]
    public void DateRangeFor_July2026_IsTheFullPreviousYearAndTheMonthToItsLastDay()
    {
        Assert.Equal((new DateOnly(2025, 1, 1), new DateOnly(2026, 7, 31)), Ga4Agent.DateRangeFor(Period.Parse("2026-07")));
        Assert.Equal(2, Ga4Agent.HistoryYears);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Mapping a response
    // ---------------------------------------------------------------------------------------------------------------

    private static RunReportResponse Parse(JsonNode response) => NewtonsoftJsonSerializer.Instance.Deserialize<RunReportResponse>(response.ToJsonString());

    private static Row MonthRow(string yearMonth, params string[] metrics) => new()
    {
        DimensionValues = [new DimensionValue { Value = yearMonth }],
        MetricValues = [.. metrics.Select(m => new MetricValue { Value = m })],
    };

    /// <summary>
    /// A response built by hand: two periods for each of the three segments. The expected rows are written out in full, so a
    /// wrong column, period format, segment or rounding shows up here without reading a golden file.
    /// </summary>
    [Fact]
    public void MapResponse_FromSyntheticResponse()
    {
        var organic = new RunReportResponse { Rows = [MonthRow("202606", "120", "0.5", "61.25", "3"), MonthRow("202607", "95", "0.6", "74.5", "2.5")] };
        var total = new RunReportResponse { Rows = [MonthRow("202606", "410", "0.55", "88.125", "7"), MonthRow("202607", "388", "0.58", "91.9", "3.5")] };
        var google = new RunReportResponse { Rows = [MonthRow("202606", "99", "0.61", "70", "1"), MonthRow("202607", "90", "0.67", "75.5", "0.4")] };

        Assert.Equal(
            new Ga4Row[]
            {
                new Ga4Row("2026-06", "organic", 120, 0.5, 61.25, 3),
                new Ga4Row("2026-07", "organic", 95, 0.6, 74.5, 2),
            },
            Ga4Agent.MapResponse(organic, Ga4Agent.SegmentOrganic));
        Assert.Equal(
            new Ga4Row[]
            {
                new Ga4Row("2026-06", "total", 410, 0.55, 88.125, 7),
                new Ga4Row("2026-07", "total", 388, 0.58, 91.9, 4),
            },
            Ga4Agent.MapResponse(total, Ga4Agent.SegmentTotal));
        Assert.Equal(
            new Ga4Row[]
            {
                new Ga4Row("2026-06", "organic_google", 99, 0.61, 70, 1),
                new Ga4Row("2026-07", "organic_google", 90, 0.67, 75.5, 0),
            },
            Ga4Agent.MapResponse(google, Ga4Agent.SegmentOrganicGoogle));
    }

    public static TheoryData<string> MappingCases()
    {
        var data = new TheoryData<string>();
        foreach (var node in Fixtures.Json("ga4-mapping-golden.json").AsArray()) data.Add(node!["name"]!.GetValue<string>());
        return data;
    }

    /// <summary>
    /// The same synthetic responses Nova's <c>MapRaw</c> was run on, written the way Google's REST API answers and read by the
    /// REST client's own deserializer. Covers what the original does in odd cases: ratios that do not parse become zero (also a
    /// decimal comma, under the invariant culture), key events round half to even, a missing <c>rows</c> is an empty month, rows
    /// without dimensions or metrics are skipped, and sessions that are not an integer fail the whole mapping.
    /// </summary>
    [Theory]
    [MemberData(nameof(MappingCases))]
    public void MapResponse_MatchesNova(string name)
    {
        var golden = Fixtures.Json("ga4-mapping-golden.json").AsArray().Single(n => n!["name"]!.GetValue<string>() == name)!;
        var response = Parse(golden["response"]!);
        var segment = golden["segment"]!.GetValue<string>();

        if (golden["throws"] is not null)
        {
            Assert.Throws<InvalidOperationException>(() => Ga4Agent.MapResponse(response, segment));
            return;
        }

        var expected = golden["rows"]!.AsArray().Select(r => new Ga4Row(
            r!["period"]!.GetValue<string>(),
            r["segment"]!.GetValue<string>(),
            r["sessions"]!.GetValue<long>(),
            r["engagementRate"]!.GetValue<double>(),
            r["avgSessionSeconds"]!.GetValue<double>(),
            r["keyEvents"]!.GetValue<long>())).ToList();

        Assert.Equal(expected, Ga4Agent.MapResponse(response, segment));
    }

    [Fact]
    public void MapResponse_ReadsAResponseThatHasNoRowsAtAll()
    {
        // Google's REST API leaves "rows" out for an empty report, so the list is null and not empty.
        Assert.Null(new RunReportResponse().Rows);
        Assert.Empty(Ga4Agent.MapResponse(new RunReportResponse(), Ga4Agent.SegmentTotal));
    }

    [Fact]
    public void MapResponse_NamesTheMonthAndTheValueWhenSessionsAreNotAnInteger()
    {
        var response = new RunReportResponse { Rows = [MonthRow("202607", "12.5")] };

        var error = Assert.Throws<InvalidOperationException>(() => Ga4Agent.MapResponse(response, Ga4Agent.SegmentOrganic));

        Assert.Contains("'12.5'", error.Message);
        Assert.Contains("202607", error.Message);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Fetching, through Google's real request pipeline over a fake transport
    // ---------------------------------------------------------------------------------------------------------------

    private static string ResponseJson(params (string YearMonth, string[] Metrics)[] rows)
        => new JsonObject
        {
            ["rows"] = new JsonArray(rows.Select(r => (JsonNode)new JsonObject
            {
                ["dimensionValues"] = new JsonArray(new JsonObject { ["value"] = r.YearMonth }),
                ["metricValues"] = new JsonArray(r.Metrics.Select(m => (JsonNode)new JsonObject { ["value"] = m }).ToArray()),
            }).ToArray()),
            ["rowCount"] = rows.Length,
            ["kind"] = "analyticsData#runReport",
        }.ToJsonString();

    /// <summary>
    /// One month means three calls, in Nova's order (organic, total, organic from Google), each to the property's
    /// <c>runReport</c> address with the request Nova builds, and the rows come back in that same order.
    /// </summary>
    [Fact]
    public async Task FetchAsync_CallsThePropertyOncePerSegmentInOrderAndMapsTheRows()
    {
        var fake = new FakeAnalyticsData(
            ResponseJson(("202606", ["120", "0.5", "61.25", "3"]), ("202607", ["95", "0.6", "74.5", "2.5"])),
            ResponseJson(("202606", ["410", "0.55", "88.125", "7"]), ("202607", ["388", "0.58", "91.9", "4"])),
            ResponseJson(("202606", ["99", "0.61", "70", "1"]), ("202607", ["90", "0.67", "75.5", "1"])));

        var rows = await new Ga4Agent(Property, fake).FetchAsync(Period.Parse("2026-07"), CancellationToken.None);

        Assert.Equal(3, fake.Calls.Count);
        Assert.All(fake.Calls, call => Assert.Equal("/v1beta/properties/123456789:runReport", call.Uri.AbsolutePath));
        Assert.All(fake.Calls, call => Assert.Null(JsonNode.Parse(call.Body)!["property"])); // the address carries it, not the body
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(fake.Calls[0].Body), WireGolden("ga4-request-golden-organic.json")), fake.Calls[0].Body);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(fake.Calls[1].Body), WireGolden("ga4-request-golden-total.json")), fake.Calls[1].Body);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(fake.Calls[2].Body), WireGolden("ga4-request-golden-organic_google.json")), fake.Calls[2].Body);

        Assert.Equal(
            new Ga4Row[]
            {
                new Ga4Row("2026-06", "organic", 120, 0.5, 61.25, 3),
                new Ga4Row("2026-07", "organic", 95, 0.6, 74.5, 2),
                new Ga4Row("2026-06", "total", 410, 0.55, 88.125, 7),
                new Ga4Row("2026-07", "total", 388, 0.58, 91.9, 4),
                new Ga4Row("2026-06", "organic_google", 99, 0.61, 70, 1),
                new Ga4Row("2026-07", "organic_google", 90, 0.67, 75.5, 1),
            },
            rows);
    }

    [Fact]
    public async Task FetchAsync_ForAMonthWithNoDataReturnsNoRows()
    {
        var fake = new FakeAnalyticsData("{}", "{}", "{}");

        var rows = await new Ga4Agent(Property, fake).FetchAsync(Period.Parse("2026-07"), CancellationToken.None);

        Assert.Equal(3, fake.Calls.Count);
        Assert.Empty(rows);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("properties/XXXXXXXXX")]
    public async Task FetchAsync_RefusesAMissingPropertyBeforeAskingForAnything(string property)
    {
        var fake = new FakeAnalyticsData();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new Ga4Agent(property, fake).FetchAsync(Period.Parse("2026-07"), CancellationToken.None));

        Assert.Contains("properties/123456789", error.Message);
        Assert.Equal(0, fake.ServiceRequests);
        Assert.Empty(fake.Calls);
    }

    [Fact]
    public async Task FetchAsync_StopsWhenCancelled()
    {
        var fake = new FakeAnalyticsData();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new Ga4Agent(Property, fake).FetchAsync(Period.Parse("2026-07"), cts.Token));

        Assert.Empty(fake.Calls);
    }
}
