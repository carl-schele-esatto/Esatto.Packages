using System.Text.Json;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// Tests for who may see the findings.
///
/// THE FIXTURES ARE BUILT FROM JSON and not with constructor calls. A report has a dozen positional parameters of which only a
/// handful matter here, and, more importantly, that is exactly how reports reach the code in operation: the archive stores
/// JSON and deserializes it with default settings. A sparse report (an older schema, missing members) then gives the same nulls
/// in the test as in reality.
/// </summary>
public class FindingsFilterTests
{
    private static Report Parse(string json) => JsonSerializer.Deserialize<Report>(json)!;

    /// <summary>A report whose Search Console block carries these annotations and a warning on a chart as well.</summary>
    private static Report ReportWith(string annotations) => Parse($$"""
        {
          "schema_version": "1.0.0",
          "report_period": "2026-07",
          "generated_at": "2026-08-01T06:00:00Z",
          "sources": [{ "id": "gsc", "latest_period": "2026-07" }],
          "traffic": {
            "kpis": [{ "key": "gsc_clicks", "value": 120, "format": "integer" }],
            "organic": {
              "id": "traffic_organic", "type": "line", "palette": "ramp", "height": 280,
              "labels": [], "series": [], "insights": [],
              "annotations": [{ "severity": "warning", "text": { "key": "noKeyEvents" } }]
            },
            "search": {
              "id": "traffic_search",
              "segments": [],
              "annotations": [{{annotations}}],
              "insights": [{ "key": "someInsight" }],
              "queries": [{ "query": "blue widgets", "clicks": 1, "impressions": 10, "ctr_percent": 10, "position": 4 }]
            }
          }
        }
        """);

    // A finding (warning) and a note about the data (info).
    private const string OneFindingOneNote = """
        { "severity": "warning", "text": { "key": "pagesNotClicked", "args": { "count": 4 } } },
        { "severity": "info", "text": { "key": "anonymizedQueries" } }
        """;

    private static List<Annotation> NotesOf(Report? r) => r?.Traffic?.Search?.Annotations ?? [];

    // ---- IsAllowed ------------------------------------------------------------------------

    [Fact]
    public void An_empty_configuration_lets_everyone_through()
    {
        // The default. No restriction configured is the behaviour from before the feature existed.
        Assert.True(FindingsFilter.IsAllowed(["editor"], []));
        Assert.True(FindingsFilter.IsAllowed(["editor"], null));
        Assert.True(FindingsFilter.IsAllowed(null, null));
        Assert.True(FindingsFilter.IsAllowed([], []));
    }

    [Fact]
    public void Only_blank_aliases_in_the_configuration_count_as_empty()
    {
        // A setting of [""] in a secret is a mistake, not a rule that should hide the list of fixes from everyone.
        Assert.True(FindingsFilter.IsAllowed(["editor"], ["", "   "]));
        Assert.True(FindingsFilter.IsAllowed(["editor"], [null!, ""]));
    }

    [Fact]
    public void A_member_of_a_configured_group_sees_the_findings()
        => Assert.True(FindingsFilter.IsAllowed(["editor", "seo"], ["seo"]));

    [Fact]
    public void Outside_the_group_the_findings_are_hidden()
        => Assert.False(FindingsFilter.IsAllowed(["editor", "writer"], ["seo"]));

    [Fact]
    public void A_user_without_groups_does_not_see_the_findings()
    {
        Assert.False(FindingsFilter.IsAllowed([], ["seo"]));
        Assert.False(FindingsFilter.IsAllowed(null, ["seo"]));
    }

    [Fact]
    public void Blank_user_aliases_never_match()
    {
        Assert.False(FindingsFilter.IsAllowed(["", "  ", null!], ["seo"]));
    }

    [Fact]
    public void Aliases_are_compared_regardless_of_case_and_surrounding_whitespace()
    {
        // The alias is typed by hand in two places: in the user group form and in configuration. "SEO" against "seo" must not be
        // the difference between seeing and not seeing.
        Assert.True(FindingsFilter.IsAllowed(["SEO"], ["seo"]));
        Assert.True(FindingsFilter.IsAllowed(["seo"], [" seo "]));
        Assert.True(FindingsFilter.IsAllowed([" Seo "], ["sEO"]));
    }

    [Fact]
    public void One_matching_group_among_several_is_enough()
        => Assert.True(FindingsFilter.IsAllowed(["writer"], ["seo", "admin", "Writer"]));

    // ---- StripFindings --------------------------------------------------------------------

    [Fact]
    public void Findings_are_removed_but_the_data_notes_stay()
    {
        var stripped = FindingsFilter.StripFindings(ReportWith(OneFindingOneNote));

        var note = Assert.Single(NotesOf(stripped));
        Assert.Equal("info", note.Severity);
        Assert.Equal("anonymizedQueries", note.Text.Key);
    }

    [Fact]
    public void Without_findings_the_same_instance_is_returned()
    {
        // No needless copy of a whole report tree when there is nothing to remove.
        var report = ReportWith("""{ "severity": "info", "text": { "key": "anonymizedQueries" } }""");

        Assert.Same(report, FindingsFilter.StripFindings(report));
    }

    [Fact]
    public void The_rest_of_the_report_is_untouched()
    {
        var report = ReportWith(OneFindingOneNote);
        var stripped = FindingsFilter.StripFindings(report)!;

        Assert.Equal("2026-07", stripped.ReportPeriod);
        Assert.Equal("2026-08-01T06:00:00Z", stripped.GeneratedAt);
        Assert.Equal(report.Sources, stripped.Sources);
        Assert.Equal(report.Traffic.Kpis, stripped.Traffic.Kpis);
        Assert.Equal(report.Traffic.Search!.Queries, stripped.Traffic.Search!.Queries);
    }

    [Fact]
    public void Only_the_search_block_annotations_are_findings_so_chart_notes_and_insights_stay()
    {
        // The charts' own annotations describe curves and are not findings; insights interpret and carry no severity.
        var report = ReportWith(OneFindingOneNote);
        var stripped = FindingsFilter.StripFindings(report)!;

        var chartNote = Assert.Single(stripped.Traffic.Organic.Annotations);
        Assert.Equal("warning", chartNote.Severity);
        Assert.Equal("noKeyEvents", chartNote.Text.Key);
        Assert.Equal(report.Traffic.Search!.Insights, stripped.Traffic.Search!.Insights);
    }

    [Fact]
    public void Only_the_value_warning_is_a_finding()
    {
        var stripped = FindingsFilter.StripFindings(ReportWith("""
            { "severity": "warning", "text": { "key": "a" } },
            { "severity": "info", "text": { "key": "b" } },
            { "severity": "notice", "text": { "key": "c" } }
            """));

        Assert.Equal(["b", "c"], NotesOf(stripped).Select(x => x.Text.Key));
    }

    [Fact]
    public void A_report_of_only_findings_ends_up_with_an_empty_list_not_a_missing_one()
    {
        var stripped = FindingsFilter.StripFindings(ReportWith("""{ "severity": "warning", "text": { "key": "a" } }"""));

        Assert.NotNull(stripped!.Traffic.Search!.Annotations);
        Assert.Empty(stripped.Traffic.Search.Annotations);
    }

    [Fact]
    public void The_original_is_not_mutated()
    {
        // The report comes from the archive's cache and is shared between calls. Were the list mutated, the findings would
        // disappear for the person who MAY see them as well, one call later.
        var report = ReportWith(OneFindingOneNote);

        FindingsFilter.StripFindings(report);

        Assert.Equal(2, NotesOf(report).Count);
    }

    [Fact]
    public void A_report_without_a_search_block_or_null_does_not_throw()
    {
        Assert.Null(FindingsFilter.StripFindings(null));

        var sparse = Parse("""
            {
              "schema_version": "1.0.0",
              "report_period": "2026-07",
              "generated_at": "2026-08-01T06:00:00Z",
              "traffic": { "kpis": [] }
            }
            """);

        Assert.Same(sparse, FindingsFilter.StripFindings(sparse));
    }

    [Fact]
    public void A_report_without_traffic_does_not_throw()
    {
        var bare = Parse("""{ "schema_version": "1.0.0", "report_period": "2026-07", "generated_at": "2026-08-01T06:00:00Z" }""");

        Assert.Same(bare, FindingsFilter.StripFindings(bare));
    }

    [Fact]
    public void A_missing_or_empty_annotation_list_returns_the_same_instance()
    {
        var withoutList = Parse("""
            {
              "schema_version": "1.0.0", "report_period": "2026-07", "generated_at": "2026-08-01T06:00:00Z",
              "traffic": { "kpis": [], "search": { "id": "traffic_search", "segments": [] } }
            }
            """);
        var nullList = Parse("""
            {
              "schema_version": "1.0.0", "report_period": "2026-07", "generated_at": "2026-08-01T06:00:00Z",
              "traffic": { "kpis": [], "search": { "id": "traffic_search", "segments": [], "annotations": null } }
            }
            """);
        var emptyList = ReportWith("");

        Assert.Same(withoutList, FindingsFilter.StripFindings(withoutList));
        Assert.Same(nullList, FindingsFilter.StripFindings(nullList));
        Assert.Same(emptyList, FindingsFilter.StripFindings(emptyList));
    }

    [Fact]
    public void A_null_entry_in_the_list_is_kept_and_does_not_throw()
    {
        // Garbage in an archived report must not take the dashboard down, and the filter removes findings, nothing else.
        var stripped = FindingsFilter.StripFindings(ReportWith("""
            null,
            { "severity": "warning", "text": { "key": "a" } }
            """));

        Assert.Null(Assert.Single(NotesOf(stripped)));
    }

    [Fact]
    public void An_annotation_without_a_severity_is_kept()
    {
        var stripped = FindingsFilter.StripFindings(ReportWith("""
            { "text": { "key": "a" } },
            { "severity": "warning", "text": { "key": "b" } }
            """));

        Assert.Equal("a", Assert.Single(NotesOf(stripped)).Text.Key);
    }
}
