using System.Text.Json;
using System.Text.RegularExpressions;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The server sends translation keys, the client turns them into text, so both sides must build a key from an id
/// identically. <c>Client/src/lang/key-vectors.json</c> is the shared truth: this class checks the C# rule against it,
/// and the client's localize tests check the TypeScript rule against the same file.
/// </summary>
public class ReportKeysTests
{
    // Year: for a series row, the year the trailing "_YYYY" rule takes off the id (null when the rule does not apply).
    private sealed record Vector(string Area, string BaseId, string Part, string Expected, int? Year = null);

    private static List<Vector> ReadVectors()
    {
        var path = Path.Combine(ClientFiles.ClientDirectory(), "src", "lang", "key-vectors.json");
        return JsonSerializer.Deserialize<List<Vector>>(File.ReadAllText(path), JsonSerializerOptions.Web)!;
    }

    public static TheoryData<string, string, string, string> Vectors()
    {
        var data = new TheoryData<string, string, string, string>();
        foreach (var v in ReadVectors()) data.Add(v.Area, v.BaseId, v.Part, v.Expected);
        return data;
    }

    [Fact]
    public void Lookup_joins_area_PascalCase_id_and_part()
        => Assert.Equal("kpiTrafficOrganicLabel", ReportKeys.Lookup("kpi", "traffic_organic", "Label"));

    [Fact]
    public void The_vector_file_has_enough_complete_rows()
    {
        var vectors = ReadVectors();

        Assert.True(vectors.Count >= 5, $"key-vectors.json has {vectors.Count} rows");
        Assert.All(vectors, v =>
        {
            Assert.False(string.IsNullOrEmpty(v.Area));
            Assert.False(string.IsNullOrEmpty(v.BaseId));
            Assert.False(string.IsNullOrEmpty(v.Part));
            Assert.False(string.IsNullOrEmpty(v.Expected));
        });
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Lookup_matches_every_shared_vector(string area, string baseId, string part, string expected)
        => Assert.Equal(expected, ReportKeys.Lookup(area, baseId, part));

    // Each (area, id, year) once: two vectors that differ only in the part (a title and a text of the same id) are the same case here, and
    // xUnit would otherwise skip the second as a duplicate and say so on every run.
    public static TheoryData<string, string, int?> YearVectors()
    {
        var data = new TheoryData<string, string, int?>();
        foreach (var (area, baseId, year) in ReadVectors().Select(v => (v.Area, v.BaseId, v.Year)).Distinct()) data.Add(area, baseId, year);
        return data;
    }

    [Theory]
    [MemberData(nameof(YearVectors))]
    public void The_year_a_series_key_carries_matches_every_shared_vector(string area, string baseId, int? year)
        => Assert.Equal(year, area == "series" ? ReportKeys.SeriesYear(baseId) : null);

    [Fact]
    public void The_vector_file_covers_the_year_suffix_rule_both_ways()
    {
        var vectors = ReadVectors();

        Assert.Contains(vectors, v => v is { Area: "series", Year: not null });
        Assert.Contains(vectors, v => v is { Area: "series", Year: null } && Regex.IsMatch(v.BaseId, @"_\d+$"));
        Assert.Contains(vectors, v => v.Area != "series" && Regex.IsMatch(v.BaseId, @"_\d{4}$"));
    }

    [Theory]
    [InlineData("sessions_2025", "seriesSessionsLabel", 2025)]
    [InlineData("year_2026", "seriesYearLabel", 2026)]
    [InlineData("non_brand", "seriesNonBrandLabel", null)]
    [InlineData("a_12345", "seriesA12345Label", null)]
    [InlineData("a_202", "seriesA202Label", null)]
    public void A_series_key_ending_in_a_four_digit_year_looks_up_without_it_and_carries_the_year(string key, string expected, int? year)
    {
        Assert.Equal(expected, ReportKeys.Lookup("series", key, "Label"));
        Assert.Equal(year, ReportKeys.SeriesYear(key));
    }

    [Fact]
    public void The_year_suffix_rule_is_only_for_series()
        => Assert.Equal("kpiX2025Label", ReportKeys.Lookup("kpi", "x_2025", "Label"));

    [Theory]
    [InlineData("kpis", "x", "Label")]
    [InlineData("Kpi", "x", "Label")]
    [InlineData("", "x", "Label")]
    [InlineData("kpi", "x", "label")]
    [InlineData("kpi", "x", "Heading")]
    [InlineData("kpi", "x", "")]
    public void Lookup_rejects_an_unknown_area_or_part(string area, string baseId, string part)
        => Assert.Throws<ArgumentException>(() => ReportKeys.Lookup(area, baseId, part));

    [Theory]
    [InlineData("")]
    [InlineData("_a")]
    [InlineData("a_")]
    [InlineData("a__b")]
    [InlineData("a b")]
    [InlineData("a\n")]
    [InlineData("a-b")]
    [InlineData("../x")]
    [InlineData("traffic.organic")]
    [InlineData("ägg")]
    public void Lookup_rejects_an_id_that_has_no_single_PascalCase_form(string baseId)
        => Assert.Throws<ArgumentException>(() => ReportKeys.Lookup("kpi", baseId, "Label"));

    [Fact]
    public void Keys_expands_every_id_with_every_part()
        => Assert.Equal(
            ["kpiTrafficOrganicLabel", "kpiTrafficOrganicDescription", "kpiGscClicksLabel", "kpiGscClicksDescription"],
            ReportKeys.Keys("kpi", ["Label", "Description"], "traffic_organic", "gsc_clicks"));

    [Fact]
    public void A_key_registered_twice_is_reported_by_name_instead_of_silently_kept_once()
    {
        var error = Assert.Throws<InvalidOperationException>(() => ReportKeys.Build(
        [
            ReportKeys.Keys("kpi", ["Label"], "gsc_clicks", "total_sessions"),
            ReportKeys.Keys("kpi", ["Label", "Description"], "gsc_clicks"),
        ]));

        Assert.Contains("kpiGscClicksLabel", error.Message);
        Assert.DoesNotContain("kpiTotalSessionsLabel", error.Message);
    }

    [Fact]
    public void A_malformed_registration_fails_when_the_registry_is_built_not_when_a_key_is_looked_up()
    {
        // Keys(...) is lazy, so a typo surfaces from Build (that is, from the first use of All), and Lookup never builds the
        // registry: it keeps working whatever is registered.
        var registration = ReportKeys.Keys("kpis", ["Label"], "gsc_clicks");

        Assert.Throws<ArgumentException>(() => ReportKeys.Build([registration]));
        Assert.Equal("kpiGscClicksLabel", ReportKeys.Lookup("kpi", "gsc_clicks", "Label"));
    }

    [Fact]
    public void Build_keeps_every_key_once_in_registration_order()
        => Assert.Equal(
            ["kpiALabel", "kpiBLabel", "chartATitle"],
            ReportKeys.Build([ReportKeys.Keys("kpi", ["Label"], "a", "b"), ReportKeys.Keys("chart", ["Title"], "a")]));

    [Fact]
    public void The_registry_holds_only_well_formed_unique_keys()
    {
        var all = ReportKeys.All;

        Assert.NotEmpty(all);
        Assert.Equal(all.Count, all.Distinct(StringComparer.Ordinal).Count());
        Assert.All(all, key => Assert.Matches(
            new Regex("^(kpi|chart|series|annotation|insight|narrative|opportunity|target|error|ui)[A-Z0-9][A-Za-z0-9]*(Label|Description|Title|Subtitle|Text)$"),
            key));
    }
}
