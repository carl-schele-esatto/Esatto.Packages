using System.Text.Json;
using System.Text.Json.Nodes;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The server never sends prose: every text in the report is a key the client can resolve by where it sits. These tests read
/// the SERIALIZED report, which is all the client gets, for every golden scenario plus the no-data and no-brand cases.
/// </summary>
public partial class ReportMapperTests
{
    private static IEnumerable<string> SampleNameList() => [.. GoldenNameList(), "no data", "no brand terms", "no data-through date", "no Search Console data"];

    public static TheoryData<string> SampleNames() => [.. SampleNameList()];

    /// <summary>Every sample report: the golden scenarios, no data, no brand terms. EnglishTextsTests reads them too.</summary>
    internal static IEnumerable<Report> AllSamples() => SampleNameList().Select(Sample);

    private static Report Sample(string name) => name switch
    {
        "no data" => NoDataReport("no rows, no Search Console"),
        "no brand terms" => ReportMapper.Build(October2026, History(), Search(), "sc-domain:example.com", brandRegex: null),

        // The probe held no date, so the Search Console lag note goes out with no arguments at all.
        "no data-through date" => ReportMapper.Build(October2026, History(), Search() with { DataThrough = null }, brandRegex: BrandRegex),

        // Visits, and a Search Console that answered with no rows at all.
        "no Search Console data" => ReportMapper.Build(October2026, History(), Search() with { Rows = [] }, brandRegex: BrandRegex),
        _ => BuildGolden(Golden(name)),
    };

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void Every_text_the_report_names_resolves_to_a_registered_key(string name)
    {
        var uses = ReportTexts.Walk(Sample(name));

        Assert.NotEmpty(uses);
        Assert.All(uses, use => Assert.True(ReportKeys.All.Contains(use.Key), $"{use.Location}: {use.Key} ({use.Area}/{use.BaseId}/{use.Part}) is not registered"));
    }

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void The_walk_reads_every_key_and_id_in_the_report(string name)
    {
        // A text location the walk does not know would go unchecked above; this fails first.
        var json = ReportTexts.Json(Sample(name));

        ReportTexts.Walk(json, out var visited);

        Assert.Equal(ReportTexts.KeyAndIdProperties(json), visited);
    }

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void No_string_in_the_report_contains_a_Swedish_letter(string name)
    {
        var strings = new List<string>();
        Collect(ReportTexts.Json(Sample(name)), strings);

        Assert.NotEmpty(strings);
        Assert.All(strings, s => Assert.DoesNotMatch("[åäöÅÄÖ]", s));

        static void Collect(JsonNode? node, List<string> into)
        {
            switch (node)
            {
                case JsonObject o:
                    foreach (var (property, value) in o)
                    {
                        into.Add(property);
                        Collect(value, into);
                    }

                    break;
                case JsonArray a:
                    foreach (var item in a) Collect(item, into);
                    break;
                case JsonValue v when v.GetValueKind() == JsonValueKind.String:
                    into.Add(v.GetValue<string>());
                    break;
            }
        }
    }

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void Every_text_argument_is_a_plain_value_the_client_reads_unambiguously(string name)
    {
        var refs = ReportTexts.TextRefObjects(ReportTexts.Json(Sample(name))).ToList();

        Assert.All(refs, r => Assert.Equal(["key"], r.Select(p => p.Key).Where(k => k != "args")));
        Assert.All(refs.SelectMany(r => r["args"] is JsonObject args ? args : []), arg => Assert.True(
            arg.Value is null || arg.Value.GetValueKind() is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False,
            $"{arg.Key} = {arg.Value?.ToJsonString()}"));
    }

    [Theory]
    [MemberData(nameof(SampleNames))]
    public void Every_period_the_report_carries_is_a_raw_YYYY_MM(string name)
    {
        var json = ReportTexts.Json(Sample(name));
        var periods = new List<string>();
        Collect(json, null, periods);

        Assert.NotEmpty(periods);
        Assert.All(periods, p => Assert.True(Period.TryParse(p, out _), p));

        static void Collect(JsonNode? node, string? property, List<string> into)
        {
            switch (node)
            {
                case JsonObject o:
                    foreach (var (name, value) in o) Collect(value, name, into);
                    break;
                case JsonArray a:
                    foreach (var item in a) Collect(item, property, into);
                    break;
                case JsonValue v when v.GetValueKind() == JsonValueKind.String
                                      && property is "period" or "report_period" or "latest_period" or "from" or "to" or "windowStart":
                    into.Add(v.GetValue<string>());
                    break;
            }
        }
    }
}
