using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>The biggest movement and the season caveat of <see cref="NarrativeTests"/>.</summary>
public partial class NarrativeTests
{
    // ---- The biggest movement ------------------------------------------------------------

    private static Move Loss(string path = "/example/page", double? position = 35.5, MoveDelta? vsPrevious = null, MoveDelta? vsYearAgo = null) =>
        new(path, $"https://example.com{path}", 1000, position, vsPrevious, vsYearAgo);

    private static MovementResult Result(Comparison? previous, Comparison? yearAgo, string rankedBy, params Move[] losses) =>
        new("2026-07", previous, yearAgo, rankedBy, losses, Gains: []);

    [Fact]
    public void The_biggest_drop_names_the_page_both_comparisons_and_the_position()
    {
        var movement = Result(
            new Comparison("2026-06"), new Comparison("2025-07"), Movement.RankYearAgo,
            Loss(
                vsPrevious: new MoveDelta(1400, -400, -28.6, 20.5),
                vsYearAgo: new MoveDelta(7000, -6000, -85.7, 8.5)));

        var text = Only(Narrative.Write(Report(), movement, []), Narrative.KeyMovement);

        Assert.Equal("/example/page", text.Arg<string>("path"));
        Assert.Equal("2025-07", text.Arg<string>("yearAgoPeriod"));
        Assert.Equal(-6000L, text.Arg<long>("yearAgoDelta"));
        Assert.Equal("2026-06", text.Arg<string>("previousPeriod"));
        Assert.Equal(-400L, text.Arg<long>("previousDelta"));

        // The position before and after is what decides what to DO: the same drop means different things at position 12 (the
        // demand fell) and at 35 (the ranking was lost). It is set against the month the list is sorted on: here the year
        // comparison (8.5), not last month (20.5).
        Assert.Equal(35.5, text.Arg<double>("position"));
        Assert.Equal(8.5, text.Arg<double>("positionBefore"));
    }

    [Fact]
    public void The_position_is_set_against_the_previous_month_when_the_list_is_sorted_on_it()
    {
        var movement = Result(
            new Comparison("2026-06"), new Comparison("2025-07"), Movement.RankPrevious,
            Loss(
                vsPrevious: new MoveDelta(1400, -400, -28.6, 20.5),
                vsYearAgo: new MoveDelta(7000, -6000, -85.7, 8.5)));

        var text = Only(Narrative.Write(Report(), movement, []), Narrative.KeyMovement);

        Assert.Equal(20.5, text.Arg<double>("positionBefore"));
    }

    [Fact]
    public void The_direction_of_each_comparison_is_its_sign_so_a_recovery_since_last_month_is_not_a_drop()
    {
        // "Fewer than last year and MORE than last month": both numbers matter, and each keeps its own sign.
        var movement = Result(
            new Comparison("2026-06"), new Comparison("2025-07"), Movement.RankYearAgo,
            Loss(
                vsPrevious: new MoveDelta(700, 300, 42.9, 15),
                vsYearAgo: new MoveDelta(1500, -500, -33.3, 8)));

        var text = Only(Narrative.Write(Report(), movement, []), Narrative.KeyMovement);

        Assert.Equal(300L, text.Arg<long>("previousDelta"));
        Assert.Equal(-500L, text.Arg<long>("yearAgoDelta"));
    }

    [Fact]
    public void A_comparison_the_page_has_no_figure_for_is_left_out_instead_of_guessed()
    {
        var movement = Result(
            new Comparison("2026-06"), yearAgo: null, Movement.RankPrevious,
            Loss(vsPrevious: new MoveDelta(1400, -400, -28.6, 20.5), vsYearAgo: null));

        var text = Only(Narrative.Write(Report(), movement, []), Narrative.KeyMovement);

        text.AssertNullArg("yearAgoPeriod");
        text.AssertNullArg("yearAgoDelta");
        Assert.Equal(-400L, text.Arg<long>("previousDelta"));
    }

    [Fact]
    public void A_comparison_month_whose_delta_is_missing_for_the_page_is_left_out()
    {
        // The month exists in the archive, but this page has no delta against it.
        var movement = Result(
            new Comparison("2026-06"), new Comparison("2025-07"), Movement.RankYearAgo,
            Loss(vsPrevious: null, vsYearAgo: new MoveDelta(7000, -6000, -85.7, 8.5)));

        var text = Only(Narrative.Write(Report(), movement, []), Narrative.KeyMovement);

        text.AssertNullArg("previousPeriod");
        text.AssertNullArg("previousDelta");
        Assert.Equal(-6000L, text.Arg<long>("yearAgoDelta"));
    }

    [Fact]
    public void Without_a_position_in_both_months_none_is_stated()
    {
        // A position "before" without a position "now" (or the other way round) is half a sentence. Both or neither.
        var noPositionNow = Loss(position: null, vsYearAgo: new MoveDelta(7000, -6000, -85.7, 8.5));
        var noPositionBefore = Loss(vsYearAgo: new MoveDelta(7000, -6000, -85.7, null));

        foreach (var worst in new[] { noPositionNow, noPositionBefore })
        {
            var movement = Result(null, new Comparison("2025-07"), Movement.RankYearAgo, worst);

            var text = Only(Narrative.Write(Report(), movement, []), Narrative.KeyMovement);

            text.AssertNullArg("position");
            text.AssertNullArg("positionBefore");
            Assert.Equal(-6000L, text.Arg<long>("yearAgoDelta"));
        }
    }

    [Fact]
    public void The_home_page_path_is_sent_as_it_is_and_the_client_names_it()
    {
        var movement = Result(
            new Comparison("2026-06"), yearAgo: null, Movement.RankPrevious,
            Loss(path: "/", vsPrevious: new MoveDelta(500, -400, -80, 12)));

        Assert.Equal("/", Only(Narrative.Write(Report(), movement, []), Narrative.KeyMovement).Arg<string>("path"));
    }

    [Fact]
    public void A_drop_with_no_comparison_figure_at_all_gives_no_statement()
    {
        // Nothing to say about HOW MUCH the page lost, and a sentence that only names the page would be vague.
        var movement = Result(
            new Comparison("2026-06"), new Comparison("2025-07"), Movement.RankPrevious,
            Loss(vsPrevious: null, vsYearAgo: null));

        Assert.DoesNotContain(Narrative.KeyMovement, Keys(Narrative.Write(Report(), movement, [])));
    }

    [Fact]
    public void Gains_alone_make_no_statement_about_a_drop()
    {
        var movement = new MovementResult(
            "2026-07", new Comparison("2026-06"), new Comparison("2025-07"), Movement.RankPrevious,
            Losses: [], Gains: [Loss(vsPrevious: new MoveDelta(100, 400, 400, 20))]);

        Assert.Empty(Narrative.Write(Report(), movement, []));
    }

    // ---- The season caveat ---------------------------------------------------------------

    [Fact]
    public void Month_against_month_gets_a_season_caveat_naming_the_month()
    {
        var movement = Result(
            new Comparison("2026-06"), yearAgo: null, Movement.RankPrevious,
            Loss("/a", vsPrevious: new MoveDelta(500, -400, -80, 12)));

        var text = Only(Narrative.Write(Report(), movement, []), Narrative.KeySeason);

        Assert.Equal("2026-06", text.Arg<string>("period"));
    }

    [Fact]
    public void A_year_comparison_gets_no_season_caveat()
    {
        var movement = Result(
            previous: null, new Comparison("2025-07"), Movement.RankYearAgo,
            Loss("/a", vsYearAgo: new MoveDelta(500, -400, -80, 12)));

        Assert.DoesNotContain(Narrative.KeySeason, Keys(Narrative.Write(Report(), movement, [])));
    }

    [Fact]
    public void Both_comparisons_present_gets_no_season_caveat()
    {
        var movement = Result(
            new Comparison("2026-06"), new Comparison("2025-07"), Movement.RankPrevious,
            Loss("/a", vsPrevious: new MoveDelta(500, -400, -80, 12), vsYearAgo: new MoveDelta(500, -400, -80, 12)));

        Assert.DoesNotContain(Narrative.KeySeason, Keys(Narrative.Write(Report(), movement, [])));
    }

    [Fact]
    public void The_season_caveat_does_not_depend_on_there_being_a_drop()
    {
        // The comparison basis is true however the pages moved, and a reader of the gains needs it as much.
        var movement = new MovementResult(
            "2026-07", new Comparison("2026-06"), YearAgo: null, Movement.RankPrevious, Losses: [], Gains: []);

        Assert.Equal([Narrative.KeySeason], Keys(Narrative.Write(Report(), movement, [])));
    }
}
