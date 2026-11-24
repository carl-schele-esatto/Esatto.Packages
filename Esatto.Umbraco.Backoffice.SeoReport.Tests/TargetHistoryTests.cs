using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The position grid: one row per target query, one column per archived month.
///
/// THE FIXED BASKET. A visibility table built on each month's most shown queries means different words in different months,
/// and so mixes "our words moved" with "the basket changed". Here the basket is chosen by a person and changes only when
/// that person changes it, which is the only way to read a position development without mixing the two.
///
/// THE WEIGHTING IS NOT TESTED HERE. The grid calls TargetAnalysis.PositionOf, the very code the target rows use, so a test
/// here would prove that a equals a. The formula has its edge cases tested in TargetAnalysisTests.
/// </summary>
public class TargetHistoryTests
{
    private static SearchQuery Q(string query, long impressions, double position) =>
        new(query, 0, impressions, 0, position);

    private static Report Month(string period, params SearchQuery[] queries) =>
        TestReports.With(period, queries: [.. queries]);

    [Fact]
    public void One_row_per_term_and_one_column_per_month()
    {
        var grid = TargetHistory.Grid(
            ["widgets", "gadgets"],
            [Month("2026-06", Q("blue widgets", 100, 18)), Month("2026-07", Q("blue widgets", 100, 14))]);

        Assert.NotNull(grid);
        Assert.Equal(["2026-06", "2026-07"], grid.Months.Select(x => x.Period));
        Assert.Equal(["widgets", "gadgets"], grid.Rows.Select(x => x.Term));
        Assert.Equal([18.0, 14.0], grid.Rows[0].Positions);
    }

    [Fact]
    public void A_term_without_a_match_gets_null_in_the_cell()
    {
        // Null is the information: "not among the most shown that month". A zero would read as position zero, that is,
        // as the best possible.
        var grid = TargetHistory.Grid(
            ["gadgets"], [Month("2026-07", Q("blue widgets", 100, 8))]);

        Assert.NotNull(grid);
        Assert.Equal(new double?[] { null }, Assert.Single(grid.Rows).Positions);
    }

    [Fact]
    public void The_months_are_chronological()
    {
        var grid = TargetHistory.Grid(
            ["widgets"],
            [Month("2026-07", Q("blue widgets", 10, 5)), Month("2026-05", Q("blue widgets", 10, 9)), Month("2026-06", Q("blue widgets", 10, 7))]);

        Assert.NotNull(grid);
        Assert.Equal(["2026-05", "2026-06", "2026-07"], grid.Months.Select(x => x.Period));
        Assert.Equal([9.0, 7.0, 5.0], grid.Rows[0].Positions);
    }

    [Fact]
    public void Months_without_search_queries_are_left_out_entirely()
    {
        // A column where every cell is empty says nothing about the terms, only about the archive.
        var grid = TargetHistory.Grid(
            ["widgets"], [TestReports.With("2026-06"), Month("2026-07", Q("blue widgets", 10, 5))]);

        Assert.NotNull(grid);
        Assert.Equal("2026-07", Assert.Single(grid.Months).Period);
    }

    [Fact]
    public void Without_terms_or_months_there_is_no_grid()
    {
        Assert.Null(TargetHistory.Grid([], [Month("2026-07", Q("blue widgets", 10, 5))]));
        Assert.Null(TargetHistory.Grid(["widgets"], []));
    }

    // ---- No data -----------------------------------------------------------------------

    [Fact]
    public void No_terms_and_no_archive_give_no_grid_and_do_not_throw()
        => Assert.Null(TargetHistory.Grid([], []));

    [Fact]
    public void Months_whose_query_lists_are_empty_give_no_grid()
        => Assert.Null(TargetHistory.Grid(
            ["widgets"], [TestReports.With("2026-06", queries: []), TestReports.With("2026-07")]));
}
