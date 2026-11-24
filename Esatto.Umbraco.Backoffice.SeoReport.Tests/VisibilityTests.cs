using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// Visibility: how many search queries rank in the top three, per month.
///
/// The question it answers is in no other figure the report shows: clicks and impressions can stand still while rankings
/// shift.
/// </summary>
public class VisibilityTests
{
    private static SearchQuery Q(double position) =>
        new($"query {position}", 0, 10, 0, position);

    private static Report ReportWith(string period, params SearchQuery[] queries) =>
        TestReports.With(period, queries: [.. queries]);

    [Fact]
    public void The_queries_in_the_top_three_are_counted_against_the_total()
    {
        var m = Assert.Single(Visibility.PerMonth([
            ReportWith("2026-07", Q(1), Q(2.5), Q(6), Q(9.9), Q(11), Q(20), Q(21), Q(90)),
        ]));

        Assert.Equal(2, m.Top3);
        Assert.Equal(8, m.Total);
    }

    [Fact]
    public void The_limit_is_inclusive_upwards()
    {
        // Position 3.0 is top three. 3.1 is not.
        var m = Assert.Single(Visibility.PerMonth([ReportWith("2026-07", Q(3.0), Q(3.1))]));

        Assert.Equal(1, m.Top3);
        Assert.Equal(2, m.Total);
    }

    [Fact]
    public void The_months_come_in_chronological_order()
    {
        // The archive delivers newest first. A development reads left to right.
        var months = Visibility.PerMonth([
            ReportWith("2026-07", Q(1)),
            ReportWith("2026-05", Q(1)),
            ReportWith("2026-06", Q(1)),
        ]);

        Assert.Equal(["2026-05", "2026-06", "2026-07"], months.Select(x => x.Period));
    }

    [Fact]
    public void Months_without_search_queries_are_left_out()
    {
        // A month archived before search queries were fetched knows nothing about the distribution. A row of zeros would read
        // as "no search terms at all", a claim we cannot make.
        var months = Visibility.PerMonth([
            ReportWith("2026-06"),
            ReportWith("2026-07", Q(4)),
        ]);

        Assert.Equal("2026-07", Assert.Single(months).Period);
    }

    [Fact]
    public void Position_zero_is_not_counted()
    {
        // Position 0 means "no value" in the Search Console mapping, not "best possible".
        var m = Assert.Single(Visibility.PerMonth([ReportWith("2026-07", Q(0), Q(5))]));

        Assert.Equal(1, m.Total);
        Assert.Equal(0, m.Top3);
    }

    // ---- No data -----------------------------------------------------------------------

    [Fact]
    public void An_empty_archive_gives_an_empty_list()
        => Assert.Empty(Visibility.PerMonth([]));

    [Fact]
    public void A_month_where_no_query_has_a_position_is_left_out()
        => Assert.Empty(Visibility.PerMonth([ReportWith("2026-07", Q(0), Q(0))]));

    [Fact]
    public void A_month_without_a_search_block_is_left_out()
        => Assert.Empty(Visibility.PerMonth([TestReports.With("2026-07")]));
}
