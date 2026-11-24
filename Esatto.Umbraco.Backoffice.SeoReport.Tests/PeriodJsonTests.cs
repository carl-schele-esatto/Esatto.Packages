using System.Text.Json;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// A <see cref="Period"/> is a struct, and with Umbraco's options it would be written as <c>{ "year": 2026, "month": 7 }</c>. The
/// type carries a converter instead, so that wherever one ends up in a response it reaches the browser as <c>YYYY-MM</c>, and a
/// month that comes back in a request is read from that form and nothing else. Tested through the Management API's real options.
/// </summary>
public class PeriodJsonTests
{
    private sealed record Holder(Period Month, Period? Maybe, Dictionary<Period, int>? ByMonth = null);

    private static readonly JsonSerializerOptions Options = ManagementJson.Options;

    [Fact]
    public void A_period_is_written_as_its_YYYY_MM_string_and_not_as_an_object()
    {
        Assert.Equal("\"2026-07\"", JsonSerializer.Serialize(new Period(2026, 7), Options));

        using var json = ManagementJson.Parse(new Holder(new Period(2026, 7), new Period(2025, 12), new Dictionary<Period, int> { [new Period(2026, 1)] = 3 }));
        var root = json.RootElement;

        Assert.Equal("2026-07", root.GetProperty("month").GetString());
        Assert.Equal("2025-12", root.GetProperty("maybe").GetString());
        Assert.Equal("2026-01", Assert.Single(root.GetProperty("byMonth").EnumerateObject()).Name); // a month as a dictionary key too
    }

    [Fact]
    public void A_missing_nullable_period_is_null()
    {
        using var json = ManagementJson.Parse(new Holder(new Period(2026, 7), null));

        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("maybe").ValueKind);
    }

    [Fact]
    public void A_period_survives_the_round_trip()
    {
        var sent = new Holder(new Period(1, 1), new Period(9999, 12), new Dictionary<Period, int> { [new Period(2026, 7)] = 1, [new Period(2026, 8)] = 2 });

        var back = JsonSerializer.Deserialize<Holder>(ManagementJson.Serialize(sent), Options)!;

        Assert.Equal(sent.Month, back.Month);
        Assert.Equal(sent.Maybe, back.Maybe);
        Assert.Equal(sent.ByMonth, back.ByMonth);
        Assert.Equal(new Period(2026, 7), JsonSerializer.Deserialize<Period>("\"2026-07\"", Options));
        Assert.Null(JsonSerializer.Deserialize<Holder>("{\"month\":\"2026-07\",\"maybe\":null}", Options)!.Maybe);
    }

    [Theory]
    [InlineData("\"2026-13\"")]
    [InlineData("\"2026-00\"")]
    [InlineData("\"2026-7\"")]
    [InlineData("\" 2026-07\"")]
    [InlineData("\"2026-07 \"")]
    [InlineData("\"2026-07-01\"")]
    [InlineData("\"0000-01\"")]
    [InlineData("\"juli\"")]
    [InlineData("\"\"")]
    [InlineData("null")]
    [InlineData("202607")]
    [InlineData("true")]
    [InlineData("{\"year\":2026,\"month\":7}")]
    [InlineData("[\"2026-07\"]")]
    public void Anything_but_a_YYYY_MM_string_is_refused(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Period>(json, Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Holder>($"{{\"month\":{json}}}", Options));
    }

    [Fact]
    public void A_refused_value_is_not_repeated_in_the_error()
    {
        const string Hostile = "\"SECRET-9f3a-not-a-month\"";

        var error = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Period>(Hostile, Options));

        Assert.DoesNotContain("SECRET-9f3a", error.Message);
        Assert.DoesNotContain("SECRET-9f3a", error.ToString());
    }

    [Fact]
    public void A_month_that_is_not_a_YYYY_MM_string_is_refused_as_a_dictionary_key_too()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Dictionary<Period, int>>("{\"2026-7\":1}", Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Dictionary<Period, int>>("{\"SECRET-9f3a\":1}", Options));
    }

    [Fact]
    public void The_default_period_is_not_a_month_and_is_never_written_as_one()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(default(Period), Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(new Dictionary<Period, int> { [default] = 1 }, Options));
    }
}
