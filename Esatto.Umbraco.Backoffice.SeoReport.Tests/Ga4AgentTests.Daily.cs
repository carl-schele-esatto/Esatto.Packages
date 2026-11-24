using System.Globalization;
using System.Text.Json.Nodes;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Google.Apis.AnalyticsData.v1beta.Data;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>The daily-traffic half of <see cref="Ga4AgentTests"/>: request shapes, window, mapping and the series maths, against Nova's.</summary>
public partial class Ga4AgentTests
{
    private static DateOnly Day(string text) => DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Fact]
    public void BuildSeriesRequest_MatchesNovaGolden()
    {
        var request = Ga4DailyAgent.BuildSeriesRequest(Property, new DateOnly(2026, 5, 20), new DateOnly(2026, 7, 18));

        var golden = Golden("ga4-request-golden-daily-series.json");
        var actual = OnTheWire(request);
        Assert.True(JsonNode.DeepEquals(actual, golden), $"expected {golden.ToJsonString()}{Environment.NewLine}but was  {actual.ToJsonString()}");
    }

    [Fact]
    public void BuildSourcesRequest_MatchesNovaGolden()
    {
        var request = Ga4DailyAgent.BuildSourcesRequest(Property, new DateOnly(2026, 7, 15));

        var golden = Golden("ga4-request-golden-daily-sources.json");
        var actual = OnTheWire(request);
        Assert.True(JsonNode.DeepEquals(actual, golden), $"expected {golden.ToJsonString()}{Environment.NewLine}but was  {actual.ToJsonString()}");
    }

    [Fact]
    public void The_daily_golden_comparison_fails_for_a_changed_request()
    {
        var series = Ga4DailyAgent.BuildSeriesRequest(Property, new DateOnly(2026, 5, 20), new DateOnly(2026, 7, 18));
        series.Dimensions[0].Name = "yearMonth";
        Assert.False(SameAsGolden(series, "ga4-request-golden-daily-series.json"));

        var sources = Ga4DailyAgent.BuildSourcesRequest(Property, new DateOnly(2026, 7, 15));
        sources.OrderBys[0].Desc = false;
        Assert.False(SameAsGolden(sources, "ga4-request-golden-daily-sources.json"));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The window
    // ---------------------------------------------------------------------------------------------------------------

    public static TheoryData<int> WindowCases()
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < Fixtures.Json("ga4-window-golden.json").AsArray().Count; i++) data.Add(i);
        return data;
    }

    /// <summary>
    /// Nova's window rules over the same inputs: rolling thirty days (also for blank or malformed months), a past month, the
    /// running month clipped at yesterday, a leap February, and the two edges where the end falls before the start. Only the
    /// dates and the month are compared: Nova's readable label (Swedish text) is not carried over, because the server sends
    /// no prose.
    /// </summary>
    [Theory]
    [MemberData(nameof(WindowCases))]
    public void WindowFor_MatchesNova(int index)
    {
        var golden = Fixtures.Json("ga4-window-golden.json").AsArray()[index]!;

        var window = Ga4DailyAgent.WindowFor(Day(golden["today"]!.GetValue<string>()), golden["monthInput"]?.GetValue<string>());

        Assert.Equal(golden["start"]!.GetValue<string>(), Iso(window.Start));
        Assert.Equal(golden["end"]!.GetValue<string>(), Iso(window.End));
        Assert.Equal(golden["previousStart"]!.GetValue<string>(), Iso(window.PreviousStart));
        Assert.Equal(golden["previousEnd"]!.GetValue<string>(), Iso(window.PreviousEnd));
        Assert.Equal(golden["month"]?.GetValue<string>(), window.Month);
    }

    [Fact]
    public void The_rolling_window_is_thirty_days_ending_yesterday_against_the_thirty_before()
    {
        var window = Ga4DailyAgent.WindowFor(new DateOnly(2026, 7, 20), null);

        Assert.Equal(30, Ga4DailyAgent.WindowDays);
        Assert.Equal(new DateOnly(2026, 6, 20), window.Start);
        Assert.Equal(new DateOnly(2026, 7, 19), window.End);
        Assert.Equal(window.Start.AddDays(-30), window.PreviousStart);
        Assert.Equal(window.Start.AddDays(-1), window.PreviousEnd);
        Assert.Null(window.Month);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Mapping and the series maths
    // ---------------------------------------------------------------------------------------------------------------

    private static IEnumerable<string> CaseNames(string file) => Fixtures.Json(file).AsArray().Select(n => n!["name"]!.GetValue<string>());

    private static JsonNode Case(string file, string name) => Fixtures.Json(file).AsArray().Single(n => n!["name"]!.GetValue<string>() == name)!;

    private static TheoryData<string> NamesIn(string file)
    {
        var data = new TheoryData<string>();
        foreach (var name in CaseNames(file)) data.Add(name);
        return data;
    }

    public static TheoryData<string> PointCases() => NamesIn("ga4-points-golden.json");

    public static TheoryData<string> SourceCases() => NamesIn("ga4-sources-golden.json");

    public static TheoryData<string> SplitCases() => NamesIn("ga4-split-golden.json");

    [Theory]
    [MemberData(nameof(PointCases))]
    public void MapPoints_MatchesNova(string name)
    {
        var golden = Case("ga4-points-golden.json", name);

        var points = Ga4DailyAgent.MapPoints(Parse(golden["response"]!));

        Assert.Equal(
            golden["points"]!.AsArray().Select(p => new Ga4DailyPoint(Day(p![0]!.GetValue<string>()), p[1]!.GetValue<long>())).ToList(),
            points);
    }

    [Fact]
    public void MapPoints_And_MapSources_ReadAResponseWithoutRows()
    {
        Assert.Empty(Ga4DailyAgent.MapPoints(new RunReportResponse()));
        Assert.Empty(Ga4DailyAgent.MapSources(new RunReportResponse()));
    }

    [Theory]
    [MemberData(nameof(SourceCases))]
    public void MapSources_MatchesNova(string name)
    {
        var golden = Case("ga4-sources-golden.json", name);

        var sources = Ga4DailyAgent.MapSources(Parse(golden["response"]!));

        Assert.Equal(
            golden["sources"]!.AsArray().Select(s => new Ga4DailySource(
                s!["channel"]!.GetValue<string>(), s["source"]!.GetValue<string>(), s["sessions"]!.GetValue<long>())).ToList(),
            sources);
    }

    [Theory]
    [MemberData(nameof(SplitCases))]
    public void Split_MatchesNova(string name)
    {
        var golden = Case("ga4-split-golden.json", name);
        var window = Ga4DailyAgent.WindowFor(Day(golden["today"]!.GetValue<string>()), golden["month"]?.GetValue<string>());
        var input = golden["input"]!.AsArray().Select(p => new Ga4DailyPoint(Day(p![0]!.GetValue<string>()), p[1]!.GetValue<long>()));

        var series = Ga4DailyAgent.Split(input, window);

        Assert.Equal(
            golden["points"]!.AsArray().Select(p => new Ga4DailyPoint(Day(p![0]!.GetValue<string>()), p[1]!.GetValue<long>())).ToList(),
            series.Points);
        Assert.Equal(golden["baseline"]!.GetValue<double>(), series.Baseline);
        Assert.Equal(golden["total"]!.GetValue<long>(), series.Total);
        Assert.Equal(golden["previousTotal"]!.GetValue<long>(), series.PreviousTotal);
        Assert.Equal(golden["changePercent"]?.GetValue<double>(), series.ChangePercent);
        var peak = golden["peak"];
        Assert.Equal(
            peak is null ? null : new Ga4DailyPoint(Day(peak[0]!.GetValue<string>()), peak[1]!.GetValue<long>()),
            series.Peak);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Fetching
    // ---------------------------------------------------------------------------------------------------------------

    private static string DailyResponseJson(params (string Date, string Sessions)[] rows)
        => new JsonObject
        {
            ["rows"] = new JsonArray(rows.Select(r => (JsonNode)new JsonObject
            {
                ["dimensionValues"] = new JsonArray(new JsonObject { ["value"] = r.Date }),
                ["metricValues"] = new JsonArray(new JsonObject { ["value"] = r.Sessions }),
            }).ToArray()),
        }.ToJsonString();

    /// <summary>One call covers the previous period and the current one as a single range; the series is then split by date.</summary>
    [Fact]
    public async Task Daily_FetchAsync_AsksOnceForBothPeriodsAndSplitsThem()
    {
        var fake = new FakeAnalyticsData(DailyResponseJson(("20260502", "10"), ("20260610", "30"), ("20260611", "20")));

        var (series, window) = await new Ga4DailyAgent(Property, fake).FetchAsync(new DateOnly(2026, 7, 20), "2026-06", CancellationToken.None);

        var call = Assert.Single(fake.Calls);
        Assert.Equal("/v1beta/properties/123456789:runReport", call.Uri.AbsolutePath);
        var expectedRequest = Ga4DailyAgent.BuildSeriesRequest(Property, new DateOnly(2026, 5, 1), new DateOnly(2026, 6, 30));
        expectedRequest.Property = null; // goes in the address, not the body
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(call.Body), OnTheWire(expectedRequest)), call.Body);
        Assert.Equal("/v1beta/properties/123456789:runReport", call.Uri.AbsolutePath);

        Assert.Equal("2026-06", window.Month);
        Assert.Equal(30, series.Points.Count);
        Assert.Equal(50, series.Total);
        Assert.Equal(10, series.PreviousTotal);
        Assert.Equal(new Ga4DailyPoint(new DateOnly(2026, 6, 10), 30), series.Peak);
        Assert.Equal(400.0, series.ChangePercent);
    }

    [Fact]
    public async Task Daily_FetchAsync_WithoutAMonthUsesTheRollingWindow()
    {
        var fake = new FakeAnalyticsData(DailyResponseJson());

        var (series, window) = await new Ga4DailyAgent(Property, fake).FetchAsync(new DateOnly(2026, 7, 20), null, CancellationToken.None);

        var call = Assert.Single(fake.Calls);
        var body = JsonNode.Parse(call.Body)!;
        Assert.Equal("2026-05-21", body["dateRanges"]![0]!["startDate"]!.GetValue<string>());
        Assert.Equal("2026-07-19", body["dateRanges"]![0]!["endDate"]!.GetValue<string>());
        Assert.Null(window.Month);
        Assert.Equal(30, series.Points.Count);
        Assert.Equal(0, series.Total);
        Assert.Null(series.Peak);
    }

    [Fact]
    public async Task Daily_FetchSourcesAsync_AsksForTheDayAndMapsTheSources()
    {
        var fake = new FakeAnalyticsData("""
            {"rows":[
              {"dimensionValues":[{"value":"Email"},{"value":"newsletter"}],"metricValues":[{"value":"400"}]},
              {"dimensionValues":[{"value":"Direct"},{"value":"(direct)"}],"metricValues":[{"value":"0"}]}
            ]}
            """);

        var sources = await new Ga4DailyAgent(Property, fake).FetchSourcesAsync(new DateOnly(2026, 7, 15), CancellationToken.None);

        var call = Assert.Single(fake.Calls);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(call.Body), WireGolden("ga4-request-golden-daily-sources.json")), call.Body);
        Assert.Equal(new[] { new Ga4DailySource("Email", "newsletter", 400) }, sources);
    }

    [Theory]
    [InlineData("")]
    [InlineData("properties/XXXXXXXXX")]
    public async Task Daily_RefusesAMissingPropertyBeforeAskingForAnything(string property)
    {
        var fake = new FakeAnalyticsData();
        var agent = new Ga4DailyAgent(property, fake);

        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.FetchAsync(new DateOnly(2026, 7, 20), null, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.FetchSourcesAsync(new DateOnly(2026, 7, 15), CancellationToken.None));

        Assert.Equal(0, fake.ServiceRequests);
        Assert.Empty(fake.Calls);
    }
}
