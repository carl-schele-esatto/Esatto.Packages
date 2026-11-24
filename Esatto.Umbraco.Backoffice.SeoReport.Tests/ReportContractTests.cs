using System.Text.Json;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The report contract is a trimmed port of Nova's: the server sends data plus translation keys, never prose, so the
/// presentation-only and agency-only members are gone. These tests pin what is left.
/// </summary>
public class ReportContractTests
{
    private static readonly string[] RemovedNames =
    [
        "keywords", "positions", "deliverables", "actions", "agency", "client", "client_name",
        "report_period_label", "label", "description",
    ];

    private static Report Minimal() => new(
        SchemaVersion: "1.0.0",
        ReportPeriod: "2026-10",
        GeneratedAt: "2026-11-01T06:00:00Z",
        DataStatus: null,
        DataStatusNote: null,
        Sources: [],
        Traffic: new TrafficSection(
            Kpis: [],
            Organic: EmptyChart("organic"),
            Total: EmptyChart("total"),
            SearchComparison: EmptyChart("search_comparison")));

    private static ChartBlock EmptyChart(string id)
        => new(id, "line", "ramp", 280, [], [], [], []);

    private static Kpi PopulatedKpi() => new(
        Key: "gsc_clicks",
        Value: 1234,
        Format: "integer",
        Source: "gsc",
        Period: "2026-09",
        Trend: new Trend("up", 12.5, new TextRef("vsSameMonthLastYear")));

    private static ChartBlock PopulatedChart(string id) => new(
        Id: id,
        Type: "line",
        Palette: "ramp",
        Height: 280,
        Labels: ["2026-09", "2026-10"],
        Series: [new Series("sessions", [10, null], Emphasis: true, Style: "solid")],
        Annotations: [new Annotation("2026-10", "warning", new TextRef("noKeyEvents"))],
        Insights: [new TextRef("nonBrandShare", new Dictionary<string, object?> { ["percent"] = 62, ["term"] = "seo" })]);

    private static SearchSegment PopulatedSegment(string key) => new(key, [PopulatedKpi()]);

    private static Report Populated() => Minimal() with
    {
        DataStatus = "live",
        DataStatusNote = new TextRef("sampleDataNote"),
        Sources = [new SourceStatus("gsc", "2026-09", new TextRef("gscLag", new Dictionary<string, object?> { ["days"] = 3 }))],
        Traffic = new TrafficSection(
            Kpis: [PopulatedKpi()],
            Organic: PopulatedChart("organic"),
            Total: PopulatedChart("total"),
            SearchComparison: PopulatedChart("search_comparison"),
            Search: new SearchBlock(
                Id: "search",
                Segments: [PopulatedSegment("brand"), PopulatedSegment("non_brand")],
                Clicks: PopulatedChart("clicks"),
                Impressions: PopulatedChart("impressions"),
                Annotations: [new Annotation(null, "info", new TextRef("anonymizedQueries"))],
                Insights: [new TextRef("brandShare")],
                Queries: [new SearchQuery("umbraco seo", 10, 200, 5, 3.2)],
                Pages: [new SearchLandingPage("https://example.com/a", "/a", 10, 200, 5, 3.2)],
                Total: PopulatedSegment("total"),
                QueryPages: [new SearchQueryPage("umbraco seo", "https://example.com/a", "/a", 10, 200, 5, 3.2)])),
        Measured = new Measured("sc-domain:example.com", "properties/1", "esatto"),
    };

    /// <summary>
    /// Every property name in the JSON, at any depth, except inside a TextRef's "args": those keys are the
    /// placeholders of one text (data, not contract) and differ per text.
    /// </summary>
    private static HashSet<string> PropertyNames(Report report)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(report));
        var names = new HashSet<string>(StringComparer.Ordinal);
        Collect(json.RootElement, names);
        return names;

        static void Collect(JsonElement element, HashSet<string> names)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        names.Add(property.Name);
                        if (property.Name != "args") Collect(property.Value, names);
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray()) Collect(item, names);
                    break;
            }
        }
    }

    [Fact]
    public void A_minimal_report_carries_none_of_the_removed_properties()
        => Assert.Empty(PropertyNames(Minimal()).Intersect(RemovedNames));

    [Fact]
    public void A_fully_populated_report_carries_none_of_the_removed_properties_at_any_depth()
        => Assert.Empty(PropertyNames(Populated()).Intersect(RemovedNames));

    [Fact]
    public void A_populated_report_serializes_exactly_the_trimmed_contract()
    {
        string[] expected =
        [
            // Report, Measured, SourceStatus
            "schema_version", "report_period", "generated_at", "data_status", "data_status_note", "sources", "traffic",
            "measured", "gsc_property", "ga4_property", "brand_regex", "id", "latest_period", "note",
            // TextRef
            "key", "args",
            // Kpi, Trend
            "kpis", "value", "format", "source", "period", "trend", "direction", "change_percent", "text",
            // ChartBlock, Series, Annotation
            "organic", "total", "search_comparison", "type", "palette", "height", "labels", "series", "values",
            "emphasis", "style", "annotations", "insights", "severity",
            // SearchBlock, SearchSegment, queries, pages
            "search", "segments", "metrics", "clicks", "impressions", "queries", "pages", "query_pages",
            "query", "url", "path", "ctr_percent", "position",
        ];

        Assert.Equal(expected.Order(), PropertyNames(Populated()).Order());
    }

    [Fact]
    public void A_text_reference_without_arguments_omits_args()
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new TextRef("noKeyEvents")));

        Assert.Equal("noKeyEvents", json.RootElement.GetProperty("key").GetString());
        Assert.False(json.RootElement.TryGetProperty("args", out _));
    }

    [Fact]
    public void A_report_survives_a_serialize_deserialize_round_trip()
    {
        var original = JsonSerializer.Serialize(Populated());

        var roundTripped = JsonSerializer.Deserialize<Report>(original);

        Assert.NotNull(roundTripped);
        Assert.Equal(original, JsonSerializer.Serialize(roundTripped));
    }
}
