using System.Text.Json;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The JSON of <c>GET daily</c> and <c>GET daily/sources</c> as the browser receives it (the Management API's real options): the names
/// the client reads, days as <c>YYYY-MM-DD</c> and months as <c>YYYY-MM</c> strings, and the arguments of the chart's texts with their
/// nulls, which say "no month" and "nothing provisional" and are not the same as an argument that is missing.
/// </summary>
public class DailyJsonTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static async Task<JsonDocument> DailyJsonAsync(string? month, DateTimeOffset? now = null)
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Now: now));
        DailyCurves.Serve(rig);

        return JsonDocument.Parse(ManagementJson.Body(await rig.Controller.GetDaily(month, Ct)));
    }

    private static string[] Names(JsonElement element) => element.EnumerateObject().Select(p => p.Name).ToArray();

    [Fact]
    public async Task The_daily_answer_has_exactly_the_members_the_client_reads()
    {
        using var json = await DailyJsonAsync(null);
        var root = json.RootElement;

        Assert.Equal(["chart", "textArgs", "months", "window", "total", "changePercent", "peak", "days", "windowDays"], Names(root));
        Assert.Equal(["id", "type", "palette", "height", "labels", "series", "annotations", "insights"], Names(root.GetProperty("chart")));
        Assert.Equal(["start", "end", "month"], Names(root.GetProperty("window")));
        Assert.Equal(["date", "sessions"], Names(root.GetProperty("peak")));
        Assert.Equal(["date", "sessions"], Names(root.GetProperty("days")[0]));
        Assert.Equal(["period", "provisionalDays", "windowDays"], Names(root.GetProperty("textArgs")).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task The_rolling_window_has_no_month_and_nothing_to_hide_about_the_margin()
    {
        using var json = await DailyJsonAsync(null);
        var root = json.RootElement;

        Assert.Equal(JsonValueKind.Null, root.GetProperty("window").GetProperty("month").ValueKind);
        Assert.Equal("", root.GetProperty("months")[0].GetString()); // the empty string is a value, and it is written
        Assert.Equal(13, root.GetProperty("months").GetArrayLength());

        var args = root.GetProperty("textArgs");
        Assert.Equal(JsonValueKind.Null, args.GetProperty("period").ValueKind); // there, and null
        Assert.Equal(30, args.GetProperty("windowDays").GetInt32());
        Assert.Equal(2, args.GetProperty("provisionalDays").GetInt32());
    }

    [Fact]
    public async Task A_finished_month_sends_provisionalDays_as_a_null_that_is_there()
    {
        using var json = await DailyJsonAsync("2026-06");
        var args = json.RootElement.GetProperty("textArgs");

        Assert.Equal("2026-06", args.GetProperty("period").GetString());
        Assert.True(args.TryGetProperty("provisionalDays", out var provisional), "a text that is told 'nothing is provisional' is not told nothing");
        Assert.Equal(JsonValueKind.Null, provisional.ValueKind);
    }

    [Fact]
    public async Task Every_day_is_YYYY_MM_DD_and_every_month_YYYY_MM_as_a_string()
    {
        using var json = await DailyJsonAsync("2026-06");
        var members = JsonWalk.Members(json.RootElement).ToList();

        var days = members.Where(m => m.Name is "start" or "end" or "date").ToList();
        Assert.True(days.Count > 10);
        Assert.All(days, d => Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", d.Value.GetString()!));

        var window = json.RootElement.GetProperty("window");
        Assert.Matches(@"^\d{4}-\d{2}$", window.GetProperty("month").GetString()!);
        Assert.All(json.RootElement.GetProperty("months").EnumerateArray().Skip(1), m => Assert.Matches(@"^\d{4}-\d{2}$", m.GetString()!));
        Assert.DoesNotContain(members, m => m.Value.ValueKind == JsonValueKind.Object && m.Name is "start" or "end" or "date" or "month" or "period");
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", json.RootElement.GetProperty("chart").GetProperty("labels")[0].GetString()!); // the chart's labels are raw dates too
    }

    [Fact]
    public async Task The_sources_of_a_day_have_the_members_the_client_reads()
    {
        var rig = await ApiRig.CreateAsync();
        DailyCurves.Serve(rig);

        using var json = JsonDocument.Parse(ManagementJson.Body(await rig.Controller.GetDailySources("2026-08-05", Ct)));

        Assert.Equal(["date", "sources"], Names(json.RootElement));
        Assert.Equal(["channel", "source", "sessions"], Names(json.RootElement.GetProperty("sources")[0]));
        Assert.Equal("2026-08-05", json.RootElement.GetProperty("date").GetString());
    }
}
