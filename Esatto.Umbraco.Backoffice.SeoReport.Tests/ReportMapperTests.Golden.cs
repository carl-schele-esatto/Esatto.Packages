using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The port against Nova's own TrafficMapper. <c>Fixtures/report-mapper-golden.json</c> was generated once by running Nova's
/// <c>TrafficMapper.Build</c> (outside both repositories) on the synthetic inputs it carries, then trimmed to data: no prose, and
/// every sentence that carried meaning turned into the port's base id plus every number, period and date it showed (its
/// "_about" says exactly how). Each scenario's input is run through <see cref="ReportMapper.Build"/> here, trimmed the same way,
/// and must equal Nova's, number for number.
/// </summary>
public partial class ReportMapperTests
{
    private const string GoldenFile = "report-mapper-golden.json";

    private sealed record GoldenInput(
        string ReportPeriod,
        List<Ga4Row> Rows,
        GscFetch? Gsc,
        string? GscProperty,
        string? Ga4Property,
        string? BrandRegex);

    private static IEnumerable<JsonObject> GoldenScenarios()
        => Fixtures.Json(GoldenFile)["scenarios"]!.AsArray().Select(s => s!.AsObject());

    private static IEnumerable<string> GoldenNameList() => GoldenScenarios().Select(s => s["name"]!.GetValue<string>());

    public static TheoryData<string> GoldenNames() => [.. GoldenNameList()];

    private static JsonObject Golden(string name) => GoldenScenarios().Single(s => s["name"]!.GetValue<string>() == name);

    /// <summary>The report <see cref="ReportMapper.Build"/> makes from the golden scenario <paramref name="name"/>'s input.</summary>
    internal static Report GoldenReport(string name) => BuildGolden(Golden(name));

    private static Report BuildGolden(JsonObject scenario)
    {
        var input = scenario["input"].Deserialize<GoldenInput>(JsonSerializerOptions.Web)!;
        return ReportMapper.Build(Period.Parse(input.ReportPeriod), input.Rows, input.Gsc, input.GscProperty, input.Ga4Property, input.BrandRegex);
    }

    [Theory]
    [MemberData(nameof(GoldenNames))]
    public void Every_number_equals_what_Novas_TrafficMapper_produces_for_the_same_input(string name)
    {
        var scenario = Golden(name);

        var expected = WithDeliberateDifferences(scenario["expected"]!.DeepClone().AsObject());
        var actual = Trimmed(BuildGolden(scenario));

        var difference = Difference(expected, actual, "$");
        Assert.True(difference is null, $"{name}: {difference}");
    }

    [Fact]
    public void The_golden_holds_no_Swedish_text_and_no_real_domain()
    {
        var text = Fixtures.Text(GoldenFile);

        Assert.DoesNotMatch("[åäöÅÄÖ]", text);
        Assert.All(Regex.Matches(text, @"https?://([^/""]+)").Select(m => m.Groups[1].Value),
            host => Assert.True(host == "example.com" || host.EndsWith(".example.com", StringComparison.Ordinal), host));
    }

    /// <summary>
    /// The golden scenarios between them reach every text the mapper can emit, so the comparison above covers each one and
    /// no registered key is dead. (The no-data note and the "no Search Console data" note are the port's own; the no-data cases and
    /// the sample with an empty Search Console answer supply them.)
    /// </summary>
    [Fact]
    public void The_scenarios_reach_every_text_the_mapper_registers()
    {
        var reports = GoldenScenarios().Select(BuildGolden).Append(NoDataReport("no rows, no Search Console")).Append(Sample("no Search Console data"));
        var emitted = reports.SelectMany(ReportTexts.Walk).Select(u => u.Key).ToHashSet(StringComparer.Ordinal);

        Assert.Empty(ReportKeys.ReportMapperKeys().SelectMany(keys => keys).Except(emitted));
    }

    // -----------------------------------------------------------------------------------------------------------------
    // The deliberate differences from Nova, applied to Nova's report so each one is explicit and nothing else may differ.
    // -----------------------------------------------------------------------------------------------------------------

    private static JsonObject WithDeliberateDifferences(JsonObject nova)
    {
        var traffic = nova["traffic"]!;

        // 1. R10: a year-chart series is keyed "year_<YYYY>", so its label resolves through the year-suffix rule; Nova keyed
        //    it by the bare year.
        foreach (var chart in new[] { traffic["organic"], traffic["total"] })
        {
            foreach (var series in chart!["series"]!.AsArray()) series!["key"] = "year_" + series["key"]!.GetValue<string>();
        }

        // 2. The brief and the spec make "no key events" a warning; Nova called it info.
        foreach (var annotation in traffic["organic"]!["annotations"]!.AsArray())
        {
            if (annotation!["text"]!["key"]!.GetValue<string>() == "noKeyEvents") annotation["severity"] = "warning";
        }

        // 3. R11: a report with no data at all says so; Nova always wrote "live" (it never ran without data).
        if (!HasData(nova))
        {
            nova["data_status"] = "noData";
            nova["data_status_note"] = new JsonObject { ["key"] = "noData", ["numbers"] = new JsonArray(), ["periods"] = new JsonArray(), ["dates"] = new JsonArray() };
        }

        return nova;
    }

    /// <summary>Whether any value of any chart is above zero: the trimmed report's own evidence that there was data.</summary>
    private static bool HasData(JsonObject report)
        => report["traffic"]!.AsObject()
            .Where(p => p.Value is JsonObject)
            .SelectMany(p => p.Key == "search" ? [p.Value!["clicks"], p.Value["impressions"]] : new[] { p.Value })
            .SelectMany(chart => chart!["series"]!.AsArray())
            .SelectMany(series => series!["values"]!.AsArray())
            .Any(v => v is not null && v.GetValue<double>() > 0)
        || (report["traffic"]!["search"]?["total"]?["metrics"]?.AsArray().Any(m => m!["value"] is { } v && v.GetValue<double>() > 0) ?? false);

    // -----------------------------------------------------------------------------------------------------------------
    // Trimming the port's report the way the generator trimmed Nova's
    // -----------------------------------------------------------------------------------------------------------------

    private static readonly Regex PeriodPattern = new(@"^\d{4}-\d{2}\z");
    private static readonly Regex DatePattern = new(@"^\d{4}-\d{2}-\d{2}\z");

    /// <summary>The report as JSON without the members Nova's golden lacks, every text reference reduced to key and values.</summary>
    private static JsonObject Trimmed(Report report)
    {
        var json = ReportTexts.Json(report);
        json.Remove("schema_version");
        json.Remove("generated_at");
        Reduce(json);
        return json;
    }

    private static void Reduce(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject o:
                if (o.ContainsKey("severity") || o.ContainsKey("direction")) o["text"] = Values(o["text"]);
                if (o.ContainsKey("latest_period")) o["note"] = Values(o["note"]);
                if (o.ContainsKey("data_status")) o["data_status_note"] = Values(o["data_status_note"]);
                if (o["insights"] is JsonArray insights) o["insights"] = new JsonArray([.. insights.Select(Values)]);
                foreach (var (_, child) in o.ToList()) Reduce(child);
                break;
            case JsonArray a:
                foreach (var item in a) Reduce(item);
                break;
        }
    }

    /// <summary>A text reference as the golden holds one: its key, and its argument values sorted into numbers, periods and dates.</summary>
    private static JsonObject? Values(JsonNode? textRef)
    {
        if (textRef is null) return null;

        var values = textRef["args"] is JsonObject args ? args.Select(a => a.Value).ToList() : [];
        var strings = values.Where(v => v?.GetValueKind() == JsonValueKind.String).Select(v => v!.GetValue<string>()).ToList();

        return new JsonObject
        {
            ["key"] = textRef["key"]!.GetValue<string>(),
            ["numbers"] = new JsonArray([.. values.Where(v => v?.GetValueKind() == JsonValueKind.Number).Select(v => v!.GetValue<double>()).Order().Select(n => (JsonNode?)n)]),
            ["periods"] = new JsonArray([.. strings.Where(s => PeriodPattern.IsMatch(s)).Order(StringComparer.Ordinal).Select(p => (JsonNode?)p)]),
            ["dates"] = new JsonArray([.. strings.Where(s => DatePattern.IsMatch(s)).Order(StringComparer.Ordinal).Select(d => (JsonNode?)d)]),
        };
    }

    /// <summary>The first difference between two JSON trees, as a path; numbers compare by value, objects ignore order.</summary>
    private static string? Difference(JsonNode? expected, JsonNode? actual, string path)
    {
        switch (expected, actual)
        {
            case (null, null):
                return null;

            case (JsonObject e, JsonObject a):
                foreach (var name in e.Select(p => p.Key).Union(a.Select(p => p.Key), StringComparer.Ordinal))
                {
                    if (!a.ContainsKey(name)) return $"{path}.{name} is missing";
                    if (!e.ContainsKey(name)) return $"{path}.{name} is not in Nova's report";
                    if (Difference(e[name], a[name], $"{path}.{name}") is { } inner) return inner;
                }

                return null;

            case (JsonArray e, JsonArray a):
                if (e.Count != a.Count) return $"{path}: Nova has {e.Count} items, the port {a.Count} ({e.ToJsonString()} vs {a.ToJsonString()})";
                for (var i = 0; i < e.Count; i++)
                {
                    if (Difference(e[i], a[i], $"{path}[{i}]") is { } inner) return inner;
                }

                return null;

            case (JsonValue e, JsonValue a) when e.GetValueKind() == JsonValueKind.Number && a.GetValueKind() == JsonValueKind.Number:
                return e.GetValue<double>() == a.GetValue<double>() ? null : $"{path}: Nova {e.ToJsonString()}, the port {a.ToJsonString()}";

            default:
                return JsonNode.DeepEquals(expected, actual)
                    ? null
                    : $"{path}: Nova {expected?.ToJsonString() ?? "null"}, the port {actual?.ToJsonString() ?? "null"}";
        }
    }
}
