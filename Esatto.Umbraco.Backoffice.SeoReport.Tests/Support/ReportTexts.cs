using System.Text.Json;
using System.Text.Json.Nodes;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>One text the report names: where it sits, the area and part that location gives it, and the arguments it carries.</summary>
internal sealed record TextUse(string Location, string Area, string BaseId, string Part, IReadOnlyCollection<string> ArgNames)
{
    /// <summary>The translation key, by the shared rule.</summary>
    public string Key => ReportKeys.Lookup(Area, BaseId, Part);
}

/// <summary>
/// Walks a SERIALIZED report (what the client receives) and lists every text it names, applying the location rules documented
/// in Report.cs: the client has nothing else to go on, so the tests read the report the same way.
/// </summary>
internal static class ReportTexts
{
    private static readonly string[] Charts = ["organic", "total", "search_comparison"];

    public static JsonObject Json(Report report) => JsonNode.Parse(JsonSerializer.Serialize(report))!.AsObject();

    public static List<TextUse> Walk(Report report) => Walk(Json(report));

    public static List<TextUse> Walk(JsonObject root) => Walk(root, out _);

    /// <param name="root">The serialized report.</param>
    /// <param name="visited">How many "key" and "id" properties the walk read, to compare with how many the JSON holds.</param>
    public static List<TextUse> Walk(JsonObject root, out int visited)
    {
        var uses = new List<TextUse>();
        var count = 0;

        void Add(string location, string area, string baseId, IReadOnlyCollection<string> args, params string[] parts)
        {
            foreach (var part in parts) uses.Add(new TextUse(location, area, baseId, part, args));
        }

        string Id(JsonNode node, string property)
        {
            count++;
            return node[property]!.GetValue<string>();
        }

        void Ref(JsonNode? textRef, string location, string area, params string[] parts)
        {
            if (textRef is null) return;
            var args = textRef["args"] is JsonObject a ? a.Select(p => p.Key).ToArray() : [];
            Add(location, area, Id(textRef, "key"), args, parts);
        }

        void Chart(JsonNode? chart, string location)
        {
            if (chart is null) return;
            Add(location, "chart", Id(chart, "id"), [], "Title", "Subtitle", "Description");
            foreach (var series in chart["series"]!.AsArray())
            {
                var key = Id(series!, "key");
                Add($"{location}.series", "series", key, ReportKeys.SeriesYear(key) is null ? [] : ["year"], "Label");
            }

            foreach (var annotation in chart["annotations"]!.AsArray()) Ref(annotation!["text"], $"{location}.annotations", "annotation", "Title", "Text");
            foreach (var insight in chart["insights"]!.AsArray()) Ref(insight, $"{location}.insights", "insight", "Text");
        }

        void Metric(JsonNode kpi, string location, params string[] parts)
        {
            Add(location, "kpi", Id(kpi, "key"), [], parts);
            Ref(kpi["trend"]?["text"], $"{location}.trend", "kpi", "Text");
        }

        void Segment(JsonNode? segment, string location)
        {
            if (segment is null) return;
            Add(location, "series", Id(segment, "key"), [], "Label");
            foreach (var metric in segment["metrics"]!.AsArray()) Metric(metric!, $"{location}.metrics", "Label");
        }

        Ref(root["data_status_note"], "data_status_note", "ui", "Text");

        foreach (var source in root["sources"]!.AsArray())
        {
            Add("sources", "ui", Id(source!, "id"), [], "Label");
            Ref(source!["note"], "sources.note", "ui", "Text");
        }

        var traffic = root["traffic"]!;
        foreach (var kpi in traffic["kpis"]!.AsArray()) Metric(kpi!, "traffic.kpis", "Label", "Description");
        foreach (var chart in Charts) Chart(traffic[chart], $"traffic.{chart}");

        if (traffic["search"] is JsonObject search)
        {
            Add("traffic.search", "chart", Id(search, "id"), [], "Title", "Subtitle");
            foreach (var segment in search["segments"]!.AsArray()) Segment(segment, "traffic.search.segments");
            Segment(search["total"], "traffic.search.total");
            Chart(search["clicks"], "traffic.search.clicks");
            Chart(search["impressions"], "traffic.search.impressions");
            foreach (var annotation in search["annotations"]!.AsArray()) Ref(annotation!["text"], "traffic.search.annotations", "annotation", "Title", "Text");
            foreach (var insight in search["insights"]!.AsArray()) Ref(insight, "traffic.search.insights", "insight", "Text");
        }

        visited = count;
        return uses;
    }

    /// <summary>Every property called "key" or "id" anywhere in the JSON, outside a text's arguments.</summary>
    public static int KeyAndIdProperties(JsonNode? node) => node switch
    {
        JsonObject o => o.Sum(p => (p.Key is "key" or "id" ? 1 : 0) + (p.Key == "args" ? 0 : KeyAndIdProperties(p.Value))),
        JsonArray a => a.Sum(KeyAndIdProperties),
        _ => 0,
    };

    /// <summary>Every text reference object in the JSON, wherever it sits: the properties that hold one by contract.</summary>
    public static IEnumerable<JsonObject> TextRefObjects(JsonObject root)
    {
        static IEnumerable<JsonObject> Refs(JsonNode? node, string? property)
        {
            switch (node)
            {
                case JsonObject o:
                    if (property is "text" or "note" or "data_status_note" or "insights") yield return o;
                    foreach (var (name, value) in o)
                    {
                        if (name == "args") continue;
                        foreach (var r in Refs(value, name)) yield return r;
                    }

                    break;
                case JsonArray a:
                    foreach (var item in a)
                    {
                        foreach (var r in Refs(item, property)) yield return r;
                    }

                    break;
            }
        }

        return Refs(root, null);
    }
}
