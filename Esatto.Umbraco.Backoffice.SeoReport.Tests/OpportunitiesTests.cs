using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The two quick wins that only became possible once search queries were fetched in depth and query-x-page came along: a query
/// just behind page one, and two of the site's own pages competing for one query.
///
/// The class deliberately does NOT redo the three findings that already exist ("seen but not clicked", "shown but ranking
/// deep", "non-brand: visibility without clicks"). Two implementations of one rule drift apart, and then the screen
/// contradicts itself about the same page.
/// </summary>
public class OpportunitiesTests
{
    private static SearchQuery Q(string query, long impressions, double position, long clicks = 0) =>
        new(query, clicks, impressions,
            impressions == 0 ? 0 : Math.Round(clicks / (double)impressions * 100.0, 2), position);

    private static SearchQueryPage QP(string query, string path, long impressions, double position = 10) =>
        new(query, $"https://example.com{path}", path, 0, impressions, 0, position);

    private static IReadOnlyList<Opportunity> Find(SearchQuery[]? queries = null, SearchQueryPage[]? pairs = null) =>
        Opportunities.Find(TestReports.With("2026-07",
            queries: queries is null ? null : [.. queries],
            queryPages: pairs is null ? null : [.. pairs]));

    // ---- Close to page one -------------------------------------------------------------

    [Fact]
    public void A_query_on_page_two_with_volume_is_a_near_breakthrough()
    {
        var found = Find([Q("blue widgets", 400, 12.5)]);

        var o = Assert.Single(found);
        Assert.Equal(Opportunities.KindNear, o.Kind);

        // The query is in the heading and the figures in the text, so the row reads on two levels: what it is about, then
        // why. The position is what makes the query worth naming. Numbers are sent raw; the client formats them.
        Assert.Equal(Opportunities.KindNear, o.Text.Key);
        Assert.Equal("blue widgets", o.Text.Arg<string>("query"));
        Assert.Equal(12.5, o.Text.Arg<double>("position"));
        Assert.Equal(400L, o.Text.Arg<long>("impressions"));
    }

    [Fact]
    public void The_position_in_the_text_is_rounded_to_one_decimal()
    {
        var o = Assert.Single(Find([Q("blue widgets", 400, 13.26)]));

        Assert.Equal(13.3, o.Text.Arg<double>("position"));
    }

    [Fact]
    public void A_query_already_on_page_one_is_no_breakthrough()
        => Assert.Empty(Find([Q("blue widgets", 400, 5.0)]));

    [Fact]
    public void A_query_ranking_deep_is_a_matter_of_ranking_not_of_one_step()
    {
        // Position 25 is content work, not "almost there". It belongs to the existing finding "shown but ranking deep".
        Assert.Empty(Find([Q("blue widgets", 400, 25.0)]));
    }

    [Fact]
    public void Too_few_impressions_are_not_mentioned()
    {
        // Without a noise floor the list fills with queries shown three times, where a change in position is chance and not
        // a result.
        Assert.Empty(Find([Q("obscure thing", 3, 12.0)]));
    }

    [Fact]
    public void The_noise_floor_is_fifty_impressions_inclusive()
    {
        Assert.Single(Find([Q("a", 50, 12.0)]));
        Assert.Empty(Find([Q("b", 49, 12.0)]));
    }

    [Fact]
    public void The_thresholds_are_the_same_as_in_the_rest_of_the_report()
    {
        // Exactly 10 is a "good position", exactly 20 is still page two.
        Assert.Empty(Find([Q("a", 400, 10.0)]));
        Assert.Single(Find([Q("b", 400, 20.0)]));
    }

    [Fact]
    public void The_most_shown_queries_come_first()
    {
        var found = Find([Q("small", 100, 15.0), Q("large", 900, 15.0)]);

        Assert.Equal("large", found[0].Text.Arg<string>("query"));
    }

    [Fact]
    public void At_most_five_near_breakthroughs_are_named()
    {
        var queries = Enumerable.Range(1, 8).Select(i => Q($"query {i}", 100 + i, 15.0)).ToArray();

        Assert.Equal(5, Find(queries).Count);
    }

    // ---- Cannibalization ---------------------------------------------------------------

    [Fact]
    public void Two_pages_on_the_same_query_are_cannibalization()
    {
        var found = Find(
            queries: [Q("blue widgets", 500, 8.0)],
            pairs: [QP("blue widgets", "/products/widgets", 300, 8.0), QP("blue widgets", "/blog/blue-widgets", 200, 18.0)]);

        var o = Assert.Single(found, x => x.Kind == Opportunities.KindCannibal);

        Assert.Equal(Opportunities.KindCannibal, o.Text.Key);
        Assert.Equal("blue widgets", o.Text.Arg<string>("query"));
        Assert.Equal("/products/widgets", o.Text.Arg<string>("firstPath"));
        Assert.Equal(8.0, o.Text.Arg<double>("firstPosition"));
        Assert.Equal(300L, o.Text.Arg<long>("firstImpressions"));
        Assert.Equal("/blog/blue-widgets", o.Text.Arg<string>("secondPath"));
        Assert.Equal(18.0, o.Text.Arg<double>("secondPosition"));
        Assert.Equal(200L, o.Text.Arg<long>("secondImpressions"));
    }

    [Fact]
    public void A_dominant_page_with_a_marginal_side_page_is_not_cannibalization()
    {
        // Almost every query has a tail of incidental pages. Without a ratio requirement every query becomes a "finding",
        // and the list is worthless.
        var found = Find(
            queries: [Q("blue widgets", 500, 8.0)],
            pairs: [QP("blue widgets", "/products/widgets", 480, 8.0), QP("blue widgets", "/blog/other", 5, 40.0)]);

        Assert.DoesNotContain(found, x => x.Kind == Opportunities.KindCannibal);
    }

    [Fact]
    public void The_second_page_must_have_a_quarter_of_the_first_ones_impressions_inclusive()
    {
        Assert.Single(Find(pairs: [QP("a", "/one", 400), QP("a", "/two", 100)]));
        Assert.Empty(Find(pairs: [QP("a", "/one", 400), QP("a", "/two", 99)]));
    }

    [Fact]
    public void Rows_of_one_page_are_added_together_and_the_best_position_is_kept()
    {
        // One page can appear for a query several times (different URLs of the same path, say); it is still one page.
        var found = Find(pairs:
        [
            QP("a", "/one", 150, 9.0), QP("a", "/one", 150, 7.0), QP("a", "/two", 100, 18.0),
        ]);

        var o = Assert.Single(found);
        Assert.Equal(300L, o.Text.Arg<long>("firstImpressions"));
        Assert.Equal(7.0, o.Text.Arg<double>("firstPosition"));
    }

    [Fact]
    public void Without_a_page_link_there_is_no_cannibalization()
    {
        // Months archived before query-x-page was fetched have no pairs. The class must stay quiet, not crash.
        Assert.DoesNotContain(
            Find(queries: [Q("blue widgets", 500, 8.0)]), x => x.Kind == Opportunities.KindCannibal);
    }

    [Fact]
    public void Pages_without_any_impressions_are_not_cannibalization()
    {
        // Zero is not a quarter of zero: two pages that were never shown compete for nothing.
        Assert.Empty(Find(pairs: [QP("a", "/one", 0), QP("a", "/two", 0)]));
    }

    // ---- The order ---------------------------------------------------------------------

    [Fact]
    public void The_cheapest_action_comes_first()
    {
        // One step from page one is less work than merging two pages.
        var found = Find(
            queries: [Q("blue widgets", 500, 15.0)],
            pairs: [QP("blue widgets", "/a", 300, 15.0), QP("blue widgets", "/b", 200, 18.0)]);

        Assert.Equal([Opportunities.KindNear, Opportunities.KindCannibal], found.Select(x => x.Kind));
        Assert.Equal(found.Select(x => x.Effort).Order(), found.Select(x => x.Effort));
    }

    // ---- No data -----------------------------------------------------------------------

    [Fact]
    public void An_empty_report_gives_an_empty_list()
        => Assert.Empty(Find());

    [Fact]
    public void Empty_lists_give_an_empty_list()
        => Assert.Empty(Find(queries: [], pairs: []));

    [Fact]
    public void No_report_at_all_gives_an_empty_list()
        => Assert.Empty(Opportunities.Find(null));

    [Fact]
    public void A_query_without_text_is_not_a_competition()
        => Assert.Empty(Find(pairs: [QP("", "/one", 400), QP("", "/two", 400)]));
}
