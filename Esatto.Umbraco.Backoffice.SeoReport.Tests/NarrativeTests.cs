using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// Tests for the month in words.
///
/// WHY IT EXISTS: the dashboard produced tables and a mechanical sentence per row, while a report written by a person does
/// something else: it JOINS sources that contradict each other, NAMES a cause and DRAWS a conclusion. The narrative does the
/// first and the third deterministically; the second cannot be known from any data source and comes from the editor's note.
///
/// EVERY STATEMENT MUST BE SUPPORTED BY THE FIGURES. The tests therefore check both that a statement appears when its basis
/// exists AND that it is absent when it does not: a summary that guesses is worse than none. The server sends a key and raw
/// figures, so the tests read the arguments with their exact types and never any wording (the wording is in en.ts).
/// </summary>
public partial class NarrativeTests
{
    private static Report Report(List<Kpi>? kpis = null, List<SearchSegment>? segments = null) =>
        TestReports.With("2026-07", kpis: kpis, segments: segments);

    private static Kpi K(string key, double? value, string? direction = null, double? changePercent = null) =>
        TestReports.Kpi(key, value, direction, changePercent);

    private static IReadOnlyList<string> Keys(IReadOnlyList<Statement> said) => [.. said.Select(x => x.Text.Key)];

    private static TextRef Only(IReadOnlyList<Statement> said, string key) =>
        Assert.Single(said, x => x.Text.Key == key).Text;

    // ---- The two sources against each other ---------------------------------------------

    [Fact]
    public void Sources_that_contradict_each_other_are_flagged_so_the_text_can_blame_the_measurement()
    {
        // The most important sentence: GA4 seems to fall while Search Console is up, and the cause is measurement, not traffic.
        var said = Narrative.Write(
            Report([K("gsc_clicks", 120, "up", 10.0), K("organic_sessions", 80, "down", -20.0)]),
            movement: null, visibility: []);

        var text = Only(said, Narrative.KeySources);

        Assert.Equal(120d, text.Arg<double>("clicks"));
        Assert.Equal(10d, text.Arg<double>("clicksChange"));
        Assert.Equal(80d, text.Arg<double>("sessions"));
        Assert.Equal(-20d, text.Arg<double>("sessionsChange"));
        Assert.False(text.Arg<bool>("agree"), "the conclusion is the whole point of the statement");
    }

    [Fact]
    public void Sources_that_point_the_same_way_do_not_claim_a_measurement_error()
    {
        var said = Narrative.Write(
            Report([K("gsc_clicks", 120, "up", 10.0), K("organic_sessions", 80, "up", 9.0)]),
            movement: null, visibility: []);

        Assert.True(Only(said, Narrative.KeySources).Arg<bool>("agree"));
    }

    [Fact]
    public void Two_falling_sources_agree_too()
    {
        var said = Narrative.Write(
            Report([K("gsc_clicks", 120, "down", -4.0), K("organic_sessions", 80, "down", -9.0)]),
            movement: null, visibility: []);

        Assert.True(Only(said, Narrative.KeySources).Arg<bool>("agree"));
    }

    [Fact]
    public void Without_both_sources_nothing_is_said_about_the_difference()
    {
        var said = Narrative.Write(Report([K("gsc_clicks", 120, "up", 10.0)]), movement: null, visibility: []);

        Assert.DoesNotContain(Narrative.KeySources, Keys(said));
    }

    [Fact]
    public void Without_trends_no_direction_is_claimed()
    {
        // Two figures without a trend say nothing about development, and then the statement must not suggest one.
        var said = Narrative.Write(
            Report([K("gsc_clicks", 120), K("organic_sessions", 80)]),
            movement: null, visibility: []);

        Assert.DoesNotContain(Narrative.KeySources, Keys(said));
    }

    [Fact]
    public void A_trend_that_is_a_note_without_a_percentage_gives_no_direction()
    {
        // "no comparison" trends carry a text and no change.
        var said = Narrative.Write(
            Report([K("gsc_clicks", 120, "up", 10.0), K("organic_sessions", 80, "note")]),
            movement: null, visibility: []);

        Assert.DoesNotContain(Narrative.KeySources, Keys(said));
    }

    [Fact]
    public void A_source_without_a_value_gives_no_statement()
    {
        var said = Narrative.Write(
            Report([K("gsc_clicks", null, "up", 10.0), K("organic_sessions", 80, "down", -20.0)]),
            movement: null, visibility: []);

        Assert.DoesNotContain(Narrative.KeySources, Keys(said));
    }

    [Fact]
    public void The_figures_are_raw_numbers_not_formatted_text()
    {
        // The client formats them for the user's language, so the server must not (no thousands separator, no decimal comma).
        var text = Only(
            Narrative.Write(
                Report([K("gsc_clicks", 1234, "up", 7.6), K("organic_sessions", 900, "down", -15.3)]),
                movement: null, visibility: []),
            Narrative.KeySources);

        Assert.Equal(1234d, text.Arg<double>("clicks"));
        Assert.Equal(7.6, text.Arg<double>("clicksChange"));
        Assert.Equal(-15.3, text.Arg<double>("sessionsChange"));
    }

    // ---- Non-brand ----------------------------------------------------------------------

    [Fact]
    public void The_non_brand_share_carries_both_figures_and_the_percentage()
    {
        // 20 of 120 is 16.666..., one decimal.
        var said = Narrative.Write(
            Report([K("gsc_clicks", 120)], [TestReports.Segment("brand", 40), TestReports.Segment("non_brand", 20)]),
            movement: null, visibility: []);

        var text = Only(said, Narrative.KeyNonBrand);

        Assert.Equal(20d, text.Arg<double>("clicks"));
        Assert.Equal(120d, text.Arg<double>("total"));
        Assert.Equal(16.7, text.Arg<double>("percent"));
    }

    [Fact]
    public void Without_a_non_brand_segment_nothing_is_said_about_it()
    {
        var said = Narrative.Write(Report([K("gsc_clicks", 120)]), movement: null, visibility: []);

        Assert.DoesNotContain(Narrative.KeyNonBrand, Keys(said));
    }

    [Fact]
    public void A_non_brand_segment_without_a_clicks_measure_gives_no_statement()
    {
        var segment = new SearchSegment("non_brand", [K("non_brand_impressions", 900)]);

        var said = Narrative.Write(Report([K("gsc_clicks", 120)], [segment]), movement: null, visibility: []);

        Assert.DoesNotContain(Narrative.KeyNonBrand, Keys(said));
    }

    [Fact]
    public void Without_a_click_total_to_stand_against_the_share_is_not_stated()
    {
        // No total, or a total of zero: the share would divide by nothing, and the sentence would claim a share of nothing.
        foreach (var total in new[] { null, K("gsc_clicks", 0), K("gsc_clicks", null) })
        {
            var said = Narrative.Write(
                Report(total is null ? [] : [total], [TestReports.Segment("non_brand", 20)]),
                movement: null, visibility: []);

            Assert.DoesNotContain(Narrative.KeyNonBrand, Keys(said));
        }
    }

    [Fact]
    public void A_non_brand_segment_without_a_value_gives_no_statement()
    {
        var segment = new SearchSegment("non_brand", [K("non_brand_clicks", null)]);

        var said = Narrative.Write(Report([K("gsc_clicks", 120)], [segment]), movement: null, visibility: []);

        Assert.DoesNotContain(Narrative.KeyNonBrand, Keys(said));
    }

    // ---- The edges -----------------------------------------------------------------------

    [Fact]
    public void Without_a_basis_nothing_is_written()
    {
        Assert.Empty(Narrative.Write(null, null, []));
        Assert.Empty(Narrative.Write(Report(), null, []));
    }

    [Fact]
    public void A_missing_visibility_list_is_the_same_as_an_empty_one()
    {
        Assert.Empty(Narrative.Write(Report(), null, null));
        Assert.Empty(Narrative.Write(null, null, null));
    }

    [Fact]
    public void A_report_with_no_kpis_or_search_block_is_read_without_error()
    {
        var bare = TestReports.With("2026-07");

        Assert.Empty(Narrative.Write(bare, null, []));
    }

    [Fact]
    public void Null_entries_and_missing_lists_in_a_sparse_report_are_skipped_without_error()
    {
        // An archived report is JSON read with default settings: a missing member is a null and a damaged list can hold a null.
        var said = Narrative.Write(
            Report([null!, K("gsc_clicks", 120)], [null!, new SearchSegment("brand", null!), TestReports.Segment("non_brand", 20)]),
            movement: null,
            visibility: [null!, B("2026-07", 20)]);

        Assert.Equal([Narrative.KeyVisibility, Narrative.KeyNonBrand], Keys(said));

        var noMeasures = Narrative.Write(
            Report([K("gsc_clicks", 120)], [new SearchSegment("non_brand", null!)]), movement: null, visibility: []);

        Assert.Empty(noMeasures);
    }

    [Fact]
    public void A_movement_without_lists_or_months_says_nothing_and_never_throws()
    {
        var hollow = new MovementResult("2026-07", Previous: null, YearAgo: null, Movement.RankPrevious, Losses: null!, Gains: null!);

        Assert.Empty(Narrative.Write(Report(), hollow, []));
    }

    [Fact]
    public void Figures_that_are_not_finite_count_as_missing_and_never_throw()
    {
        // Math.Sign throws on NaN, and a share of infinity is not a figure: both are treated as "no data".
        var said = Narrative.Write(
            Report(
                [
                    K("gsc_clicks", double.NaN, "up", 5.0),
                    K("organic_sessions", 10, "up", double.PositiveInfinity),
                    K("organic_key_events", double.NaN),
                ],
                [TestReports.Segment("non_brand", double.NaN)]),
            movement: null, visibility: []);

        Assert.Empty(said);
    }

    [Fact]
    public void Statements_come_in_a_fixed_order()
    {
        var movement = Result(
            new Comparison("2026-06"), yearAgo: null, Movement.RankPrevious,
            Loss(vsPrevious: new MoveDelta(500, -400, -80, 12)));

        var said = Narrative.Write(
            Report(
                [K("gsc_clicks", 120, "up", 10.0), K("organic_sessions", 80, "down", -20.0), K("organic_key_events", 2)],
                [TestReports.Segment("non_brand", 20)]),
            movement,
            [B("2026-07", 20)]);

        Assert.Equal(
            [Narrative.KeySources, Narrative.KeyVisibility, Narrative.KeyNonBrand, Narrative.KeyConversion, Narrative.KeyMovement, Narrative.KeySeason],
            Keys(said));
        Assert.All(said, s => s.Text.AssertFinite());
    }

    // ---- Which statements exist -------------------------------------------------------------

    [Fact]
    public void The_statement_keys_are_the_registered_ids_and_each_has_a_translation_key()
    {
        Assert.Equal(
            ["conversion", "movement", "nonBrand", "season", "sources", "visibility"],
            Narrative.Kinds.Order(StringComparer.Ordinal));
        Assert.All(Narrative.Kinds, id => Assert.Contains(ReportKeys.Lookup("narrative", id, "Text"), ReportKeys.All));
    }

    /// <summary>Compares the argument dictionaries of two text references by name and value.</summary>
    private sealed class ArgsComparer : IEqualityComparer<IReadOnlyDictionary<string, object?>?>
    {
        public bool Equals(IReadOnlyDictionary<string, object?>? x, IReadOnlyDictionary<string, object?>? y) =>
            x is not null && y is not null && x.Count == y.Count && x.All(p => y.TryGetValue(p.Key, out var v) && Equals(p.Value, v));

        public int GetHashCode(IReadOnlyDictionary<string, object?>? obj) => obj?.Count ?? 0;
    }
}
