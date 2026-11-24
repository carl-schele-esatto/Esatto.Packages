using System.Text.Json;
using System.Text.RegularExpressions;
using Esatto.Umbraco.Backoffice.SeoReport.Api;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;
using static Esatto.Umbraco.Backoffice.SeoReport.Tests.Support.ApiResults;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The JSON of <c>GET analysis</c> as the browser receives it (the Management API's real options, see <see cref="ManagementJson"/>):
/// the member names the client reads, every month as a <c>YYYY-MM</c> string and never as an object, and the nulls inside a
/// statement's arguments, which are information ("there is no comparison month") and not an absence.
/// </summary>
public class AnalysisJsonTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static async Task<JsonDocument> AnalysisJsonAsync()
    {
        var rig = await ApiRig.CreateAsync();
        foreach (var report in AnalysisArchive.Reports()) rig.Reports.ArchiveReport(report);
        await rig.Targets.SaveAsync(AnalysisArchive.Terms);
        await rig.Notes.SaveAsync(Period.Parse(AnalysisArchive.Newest), "A note.");

        return JsonDocument.Parse(ManagementJson.Body(await rig.Controller.GetAnalysis(null, Ct)));
    }

    private static string[] Names(JsonElement element) => element.EnumerateObject().Select(p => p.Name).ToArray();

    [Fact]
    public async Task The_analysis_has_exactly_the_members_the_client_reads()
    {
        using var json = await AnalysisJsonAsync();
        var root = json.RootElement;

        Assert.Equal(["hasAnalysis", "period", "summary", "note", "targetHistory", "movement", "opportunities", "targets"], Names(root));

        var movement = root.GetProperty("movement");
        Assert.Equal(["period", "previous", "yearAgo", "rankedBy", "losses", "gains"], Names(movement));
        Assert.Equal(["period"], Names(movement.GetProperty("previous")));
        var move = movement.GetProperty("losses")[0];
        Assert.Equal(["path", "url", "impressions", "position", "vsPrevious", "vsYearAgo"], Names(move));
        Assert.Equal(["before", "delta", "changePercent", "positionBefore"], Names(move.GetProperty("vsPrevious")));

        Assert.Equal(["text"], Names(root.GetProperty("summary")[0]));
        Assert.Equal(["key", "args"], Names(root.GetProperty("summary")[0].GetProperty("text")));
        Assert.Equal(["kind", "text", "effort"], Names(root.GetProperty("opportunities")[0]));

        var targets = root.GetProperty("targets");
        Assert.Equal(["rowsFetched", "rows"], Names(targets));
        var row = targets.GetProperty("rows")[0];
        Assert.Equal(["term", "status", "impressions", "clicks", "ctrPercent", "position", "matches", "example", "advice", "page"], Names(row));
        Assert.Equal(["key", "args"], Names(row.GetProperty("advice")));
        Assert.Equal(["path", "position", "ctrPercent", "impressions", "clicks", "queries"], Names(row.GetProperty("page")));

        var history = root.GetProperty("targetHistory");
        Assert.Equal(["months", "rows"], Names(history));
        Assert.Equal(["period"], Names(history.GetProperty("months")[0]));
        Assert.Equal(["term", "positions"], Names(history.GetProperty("rows")[0]));
    }

    [Fact]
    public async Task Every_month_is_a_YYYY_MM_string_and_never_an_object()
    {
        using var json = await AnalysisJsonAsync();
        var members = JsonWalk.Members(json.RootElement).ToList();

        // The members that name a month: "period", and every "...Period" (the arguments of the statements: comparePeriod, bestPeriod...).
        var months = members.Where(m => m.Name == "period" || m.Name.EndsWith("Period", StringComparison.Ordinal)).ToList();
        Assert.True(months.Count >= 10, $"only {months.Count} month members were found, so the walk is not looking where the months are");

        foreach (var month in months)
        {
            Assert.True(
                month.Value.ValueKind == JsonValueKind.Null
                    || (month.Value.ValueKind == JsonValueKind.String && Regex.IsMatch(month.Value.GetString()!, @"^\d{4}-\d{2}$")),
                $"{month.Path} is {month.Value.ValueKind}: {month.Value.GetRawText()}");
        }

        // A range is two days or two months as strings, never an object; and the struct form of a month has a year member.
        Assert.All(members.Where(m => m.Name is "from" or "to"), m => Assert.NotEqual(JsonValueKind.Object, m.Value.ValueKind));
        Assert.DoesNotContain(members, m => m.Name == "year");
        Assert.DoesNotContain(members, m => m.Name == "month");
    }

    [Fact]
    public async Task A_null_inside_a_statements_arguments_is_written_and_not_left_out()
    {
        using var json = await AnalysisJsonAsync();

        var visibility = json.RootElement.GetProperty("summary").EnumerateArray()
            .Select(s => s.GetProperty("text"))
            .Single(t => t.GetProperty("key").GetString() == "visibility");
        var args = visibility.GetProperty("args");

        // The newest month is also the best one, so the statement names no third month: three arguments that are there and null.
        foreach (var name in new[] { "bestPeriod", "bestTop3", "bestPercent" })
        {
            Assert.True(args.TryGetProperty(name, out var value), $"'{name}' is missing, and a text that is told \"no best month\" is not the same as one that is told nothing");
            Assert.Equal(JsonValueKind.Null, value.ValueKind);
        }

        Assert.Equal("2025-07", args.GetProperty("comparePeriod").GetString());
        Assert.True(args.GetProperty("yearOnYear").GetBoolean());
    }

    [Fact]
    public async Task A_new_page_has_a_null_change_and_it_is_written()
    {
        using var json = await AnalysisJsonAsync();

        var gains = json.RootElement.GetProperty("movement").GetProperty("gains").EnumerateArray().ToList();
        var added = gains.Single(g => g.GetProperty("path").GetString() == "/new");

        Assert.Equal(JsonValueKind.Null, added.GetProperty("vsPrevious").GetProperty("changePercent").ValueKind);
        Assert.Equal(JsonValueKind.Null, added.GetProperty("vsPrevious").GetProperty("positionBefore").ValueKind);
    }

    [Fact]
    public async Task The_advice_of_a_target_row_carries_its_figures_as_arguments()
    {
        using var json = await AnalysisJsonAsync();

        var rows = json.RootElement.GetProperty("targets").GetProperty("rows").EnumerateArray().ToList();
        var missing = rows.Single(r => r.GetProperty("term").GetString() == "garden gnome");

        Assert.Equal("missing", missing.GetProperty("advice").GetProperty("key").GetString());
        Assert.Equal(4, missing.GetProperty("advice").GetProperty("args").GetProperty("rowsFetched").GetInt32());
        Assert.Equal(JsonValueKind.Null, missing.GetProperty("position").ValueKind);
        Assert.Equal(JsonValueKind.Null, missing.GetProperty("page").ValueKind);
    }

    [Fact]
    public void An_empty_analysis_is_hasAnalysis_false_as_the_only_member()
        => Assert.Equal("{\"hasAnalysis\":false}", ManagementJson.Serialize(new NoAnalysisResponse()));
}
