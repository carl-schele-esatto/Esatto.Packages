using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// Movement: which pages gained and lost the most impressions, against the previous month AND the same month a year earlier.
///
/// TWO COMPARISONS PER ROW, not one. The previous month answers "what happened since last time", the same month last year
/// answers "are we above or below last year", and they can disagree. One comparison that switched basis depending on what
/// the archive happened to hold was the real defect: the same heading meant different things in different months.
/// </summary>
public class MovementTests
{
    private static SearchLandingPage P(string path, long impressions, long clicks = 0, double position = 10) =>
        new($"https://example.com{path}", path, clicks, impressions,
            impressions == 0 ? 0 : Math.Round(clicks / (double)impressions * 100.0, 2), position);

    private static Report Month(string period, params SearchLandingPage[] pages) =>
        TestReports.With(period, pages: [.. pages]);

    /// <summary>July 2026 with both the previous month and the same month a year earlier in the archive.</summary>
    private static MovementResult Full(SearchLandingPage now, SearchLandingPage previous, SearchLandingPage yearAgo) =>
        Movement.Compare(
            [Month("2026-07", now), Month("2026-06", previous), Month("2025-07", yearAgo)],
            "2026-07")!;

    // ---- Both comparisons --------------------------------------------------------------

    [Fact]
    public void A_row_carries_both_the_previous_month_and_the_same_month_last_year()
    {
        var result = Full(P("/a", 100), P("/a", 500), P("/a", 900));

        Assert.Equal("2026-06", result.Previous!.Period);
        Assert.Equal("2025-07", result.YearAgo!.Period);

        var move = Assert.Single(result.Losses);
        Assert.Equal(-400, move.VsPrevious!.Delta);
        Assert.Equal(-800, move.VsYearAgo!.Delta);
    }

    [Fact]
    public void A_page_can_be_up_on_last_month_and_down_on_last_year()
    {
        // THE WHOLE REASON TO SHOW BOTH. The page has recovered since last month and so lands among the GAINS, but the
        // year row shows it is still 500 impressions below last year. Without that row the rise would read as "all is well".
        var move = Assert.Single(Full(P("/a", 400), P("/a", 100), P("/a", 900)).Gains);

        Assert.Equal(300, move.VsPrevious!.Delta);
        Assert.Equal(-500, move.VsYearAgo!.Delta);
    }

    [Fact]
    public void The_position_in_both_comparison_months_comes_along()
    {
        var move = Assert.Single(Full(
            P("/", 1000, position: 35.5), P("/", 1200, position: 20.5), P("/", 5000, position: 8.5)).Losses);

        Assert.Equal(35.5, move.Position);
        Assert.Equal(20.5, move.VsPrevious!.PositionBefore);
        Assert.Equal(8.5, move.VsYearAgo!.PositionBefore);
    }

    // ---- The ranking -------------------------------------------------------------------

    [Fact]
    public void It_is_ranked_on_the_previous_month_even_when_the_year_month_exists()
    {
        // CONSISTENCY ABOVE ALL. The order must mean the same thing every month, or the list cannot be compared between months
        // even though each single figure is true. "/new-loss" dropped the most SINCE LAST MONTH and so comes first, even
        // though "/old-loss" is further below last year.
        var result = Movement.Compare(
            [
                Month("2026-07", P("/new-loss", 600), P("/old-loss", 100)),
                Month("2026-06", P("/new-loss", 1000), P("/old-loss", 110)),
                Month("2025-07", P("/new-loss", 700), P("/old-loss", 5000)),
            ],
            "2026-07")!;

        Assert.Equal(Movement.RankPrevious, result.RankedBy);
        Assert.Equal("/new-loss", result.Losses[0].Path);

        // The year comparison stays on the row as a check, though it does not set the order.
        Assert.Equal(-100, result.Losses[0].VsYearAgo!.Delta);
    }

    [Fact]
    public void Without_a_year_month_only_the_month_comparison_is_there()
    {
        var result = Movement.Compare(
            [Month("2026-07", P("/a", 100)), Month("2026-06", P("/a", 500))],
            "2026-07")!;

        Assert.Equal(Movement.RankPrevious, result.RankedBy);
        Assert.Null(result.YearAgo);
        Assert.Null(Assert.Single(result.Losses).VsYearAgo);
    }

    [Fact]
    public void The_year_month_alone_is_enough_too()
    {
        // The oldest archived month has no previous one, and a month can have its year month but not the month before it
        // when the archive has a gap. Here the year month IS the nearest earlier month, which is one comparison and is
        // reported as the year comparison only.
        var result = Movement.Compare(
            [Month("2026-07", P("/a", 100)), Month("2025-07", P("/a", 900))],
            "2026-07")!;

        Assert.Null(result.Previous);
        Assert.Equal("2025-07", result.YearAgo!.Period);
        Assert.Equal(Movement.RankYearAgo, result.RankedBy);
        Assert.Null(Assert.Single(result.Losses).VsPrevious);
    }

    [Fact]
    public void The_previous_month_is_the_NEAREST_earlier_archived_one()
    {
        var result = Movement.Compare(
            [Month("2026-07", P("/a", 100)), Month("2026-06", P("/a", 500)), Month("2026-03", P("/a", 900))],
            "2026-07")!;

        Assert.Equal("2026-06", result.Previous!.Period);
    }

    // ---- The edges ---------------------------------------------------------------------

    [Fact]
    public void Without_any_comparison_period_there_is_no_answer()
        => Assert.Null(Movement.Compare([Month("2026-07", P("/a", 100))], "2026-07"));

    [Fact]
    public void An_unknown_period_gives_null()
        => Assert.Null(Movement.Compare([Month("2026-07", P("/a", 100))], "2026-01"));

    [Fact]
    public void Months_without_a_page_list_do_not_count_as_a_comparison()
    {
        // A month archived before the pages were fetched cannot be compared against: everything would look like a pure gain.
        var result = Movement.Compare(
            [Month("2026-07", P("/a", 100)), TestReports.With("2026-06"), Month("2026-05", P("/a", 500))],
            "2026-07")!;

        Assert.Equal("2026-05", result.Previous!.Period);
    }

    [Fact]
    public void A_page_that_disappeared_entirely_is_the_clearest_loss()
    {
        var result = Movement.Compare(
            [Month("2026-07", P("/kept", 100)), Month("2025-07", P("/kept", 100), P("/gone", 1200))],
            "2026-07")!;

        var loss = Assert.Single(result.Losses);
        Assert.Equal("/gone", loss.Path);
        Assert.Equal(0, loss.Impressions);
        Assert.Equal(1200, loss.VsYearAgo!.Before);
        Assert.Equal(-100, loss.VsYearAgo.ChangePercent);
    }

    [Fact]
    public void A_new_page_has_no_percentage_change()
    {
        var gain = Assert.Single(Movement.Compare(
            [Month("2026-07", P("/new", 300)), Month("2025-07", P("/old", 300))], "2026-07")!.Gains);

        Assert.Equal("/new", gain.Path);

        // Division by zero gives no percentage: "new" is the information, and infinity is not a figure to show.
        Assert.Null(gain.VsYearAgo!.ChangePercent);
    }

    [Fact]
    public void Unchanged_pages_are_not_mentioned()
    {
        var result = Full(P("/same", 500), P("/same", 500), P("/same", 500));

        Assert.Empty(result.Losses);
        Assert.Empty(result.Gains);
    }

    [Fact]
    public void The_lists_are_cut_at_take()
    {
        var now = Enumerable.Range(1, 10).Select(i => P($"/page{i}", 10)).ToArray();
        var before = Enumerable.Range(1, 10).Select(i => P($"/page{i}", 1000)).ToArray();

        var result = Movement.Compare(
            [Month("2026-07", now), Month("2025-07", before)], "2026-07", take: 3)!;

        Assert.Equal(3, result.Losses.Length);
    }

    [Fact]
    public void The_percentage_change_is_rounded_to_one_decimal()
    {
        // 300 -> 301 is 0.3 %, not 0.33333.
        var move = Assert.Single(Movement.Compare(
            [Month("2026-07", P("/a", 301)), Month("2026-06", P("/a", 300))], "2026-07")!.Gains);

        Assert.Equal(0.3, move.VsPrevious!.ChangePercent);
    }

    [Fact]
    public void A_page_with_no_impressions_in_every_month_is_not_a_movement_and_gives_no_NaN()
    {
        var result = Full(P("/quiet", 0), P("/quiet", 0), P("/quiet", 0));

        Assert.Empty(result.Losses);
        Assert.Empty(result.Gains);
    }

    // ---- No data -----------------------------------------------------------------------

    [Fact]
    public void An_empty_archive_gives_null_and_does_not_throw()
        => Assert.Null(Movement.Compare([], "2026-07"));

    [Fact]
    public void Months_whose_page_lists_are_empty_give_null()
        => Assert.Null(Movement.Compare(
            [TestReports.With("2026-07", pages: []), TestReports.With("2025-07", pages: [])], "2026-07"));

    [Theory]
    [InlineData("")]
    [InlineData("2026")]
    [InlineData("2026-7")]
    [InlineData("not-a-month")]
    public void A_period_that_is_not_a_month_gives_null_and_does_not_throw(string period)
        => Assert.Null(Movement.Compare([Month("2026-07", P("/a", 100)), Month("2025-07", P("/a", 900))], period));
}
