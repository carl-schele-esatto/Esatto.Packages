using System.Globalization;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The target-query analysis.
///
/// The analysis is a PURE PROJECTION: (target queries, the report's search queries) gives rows with a status and a piece of
/// advice. No Google, no Umbraco context, no storage, so all of the logic can be tested from here, which is also why it lives
/// in a class of its own and not in the controller.
///
/// The thresholds come from ReportMapper (FindingGoodPosition 10, FindingDeepPosition 20, FindingLowCtr 1 %). The tests name
/// the figures anyway: reading a test, you should see which value it claims to hold without opening another file.
///
/// The advice is a text reference (a key and its arguments), never prose: what the client shows is in en.ts, and the tests
/// check the key and the raw figures it carries.
/// </summary>
public class TargetAnalysisTests
{
    private const int Rows = 250;

    /// <summary>A search query with its CTR worked out from clicks and impressions, as Search Console does.</summary>
    private static SearchQuery Q(string query, long clicks, long impressions, double position) =>
        new(query, clicks, impressions,
            impressions == 0 ? 0 : Math.Round(clicks / (double)impressions * 100.0, 2), position);

    /// <summary>Without a page link, like a month archived before query-x-page was fetched.</summary>
    private static TargetRow Row(string term, params SearchQuery[] queries) =>
        Assert.Single(TargetAnalysis.Build([term], queries, null, Rows));

    /// <summary>A query-page pair from Search Console.</summary>
    private static SearchQueryPage QP(string query, string path, long clicks, long impressions, double position) =>
        new(query, $"https://example.com{path}", path, clicks, impressions,
            impressions == 0 ? 0 : Math.Round(clicks / (double)impressions * 100.0, 2), position);

    private static TargetRow RowWith(string term, SearchQuery[] queries, SearchQueryPage[] pages) =>
        Assert.Single(TargetAnalysis.Build([term], queries, pages, Rows));

    // ---- The statuses ------------------------------------------------------------------

    [Fact]
    public void The_status_codes_are_the_ones_the_client_maps_to_labels()
        => Assert.Equal(["missing", "deep", "near", "title", "performing"], TargetAnalysis.Statuses);

    [Fact]
    public void A_term_without_matches_has_the_status_missing()
    {
        var row = Row("gadgets", Q("blue widgets", 5, 100, 8));

        Assert.Equal(TargetAnalysis.StatusMissing, row.Status);
        Assert.Equal(0, row.Matches);
        Assert.Equal(0, row.Impressions);
        Assert.Equal(0, row.Clicks);
        Assert.Null(row.Position);   // with no matches there is no position to show
        Assert.Null(row.Example);
    }

    [Fact]
    public void A_deep_position_has_the_status_deep()
        => Assert.Equal(TargetAnalysis.StatusDeep, Row("widgets", Q("blue widgets", 2, 400, 24.5)).Status);

    [Fact]
    public void A_position_between_ten_and_twenty_has_the_status_near()
        => Assert.Equal(TargetAnalysis.StatusNear, Row("widgets", Q("blue widgets", 2, 400, 14.5)).Status);

    [Fact]
    public void A_good_position_with_a_low_CTR_has_the_status_title()
    {
        // Position 6 and 0.2 % CTR: the ranking is won, the click is not.
        Assert.Equal(TargetAnalysis.StatusTitle, Row("widgets", Q("blue widgets", 2, 1000, 6.0)).Status);
    }

    [Fact]
    public void A_good_position_with_a_good_CTR_has_the_status_performing()
        => Assert.Equal(TargetAnalysis.StatusPerforming, Row("widgets", Q("blue widgets", 50, 1000, 6.0)).Status);

    // ---- The limits --------------------------------------------------------------------

    [Fact]
    public void Position_exactly_twenty_is_near_and_not_deep()
    {
        // "Deeper than 20" is strict: exactly 20.0 is still on page two.
        Assert.Equal(TargetAnalysis.StatusNear, Row("widgets", Q("blue widgets", 2, 400, 20.0)).Status);
    }

    [Fact]
    public void Position_exactly_ten_is_not_near()
    {
        // Ten is a "good position", so it is about the title and not about ranking.
        Assert.Equal(TargetAnalysis.StatusTitle, Row("widgets", Q("blue widgets", 2, 1000, 10.0)).Status);
    }

    [Fact]
    public void A_CTR_of_exactly_one_percent_is_not_title()
    {
        // The threshold is "under 1 %". Exactly 1 % is not low.
        Assert.Equal(TargetAnalysis.StatusPerforming, Row("widgets", Q("blue widgets", 10, 1000, 5.0)).Status);
    }

    // ---- The matching ------------------------------------------------------------------

    [Fact]
    public void The_term_matches_as_a_substring_whatever_the_case()
    {
        // A target query is a TOPIC. Search Console delivers whole phrases, so the bare word hardly ever occurs; it is the
        // phrases that contain it that exist.
        var row = Row("widgets",
            Q("Blue widgets", 5, 100, 8),
            Q("hosting WIDGETS", 1, 50, 12),
            Q("green sprockets", 9, 300, 4));

        Assert.Equal(2, row.Matches);
        Assert.Equal(150, row.Impressions);   // the third query does not contain the term
    }

    [Fact]
    public void Several_matches_are_added_up_and_the_CTR_is_worked_out_again_from_the_figures()
    {
        // An AVERAGE of the ratios would give about 5.1 %. The right answer is 12/1000 = 1.2 %.
        var row = Row("widgets",
            Q("blue widgets", 10, 100, 5.0),
            Q("widgets error 500", 2, 900, 30.0));

        Assert.Equal(1000, row.Impressions);
        Assert.Equal(12, row.Clicks);
        Assert.Equal(1.2, row.CtrPercent);
    }

    [Fact]
    public void The_position_is_weighted_by_impressions()
    {
        // (5 x 100 + 30 x 900) / 1000 = 27.5. A plain average would give 17.5, and the term would look "near page one" while
        // nearly all the impressions are at position 30.
        var row = Row("widgets",
            Q("blue widgets", 10, 100, 5.0),
            Q("widgets error 500", 2, 900, 30.0));

        Assert.Equal(27.5, row.Position);
        Assert.Equal(TargetAnalysis.StatusDeep, row.Status);
    }

    [Fact]
    public void The_example_is_the_query_with_the_most_impressions()
    {
        // The example is there so the aggregation can be checked: the term "widgets" also catches "widgets error 500", and
        // without the example it is not visible what was added up.
        Assert.Equal("widgets error 500", Row("widgets",
                Q("blue widgets", 10, 100, 5.0),
                Q("widgets error 500", 2, 900, 30.0))
            .Example);
    }

    // ---- The edges ---------------------------------------------------------------------

    [Fact]
    public void An_empty_term_list_gives_an_empty_analysis()
        => Assert.Empty(TargetAnalysis.Build([], [Q("widgets", 5, 100, 8)], null, Rows));

    [Fact]
    public void Without_search_queries_every_term_is_missing()
    {
        // A month archived before search queries were fetched has no list. The answer is "we do not know", and the nearest
        // thing to the truth is that nothing shows.
        var rows = TargetAnalysis.Build(["gadgets", "widgets"], null, null, Rows);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, x => Assert.Equal(TargetAnalysis.StatusMissing, x.Status));
    }

    [Fact]
    public void The_order_of_the_rows_follows_the_term_list()
    {
        // The editor sorts the list by what matters most. That order is information and the analysis must not rewrite it.
        var rows = TargetAnalysis.Build(
            ["widgets", "gadgets"], [Q("blue widgets", 5, 100, 8)], null, Rows);

        Assert.Equal("widgets", rows[0].Term);
        Assert.Equal("gadgets", rows[1].Term);
    }

    [Fact]
    public void Matches_without_impressions_do_not_divide_by_zero()
    {
        // Search Console can return a row with impressions = 0. The weighting divides by the impressions, so without a guard
        // the position becomes NaN, which serializes as null and looks like "no data": a silent failure instead of a visible one.
        var row = Row("widgets", Q("blue widgets", 0, 0, 12.0));

        Assert.Equal(12.0, row.Position);
        Assert.Equal(0, row.CtrPercent);
        Assert.Equal(TargetAnalysis.StatusNear, row.Status);
    }

    [Fact]
    public void Matches_without_impressions_keep_a_page_position_too()
    {
        var row = RowWith("widgets",
            [Q("blue widgets", 0, 0, 12.0)],
            [QP("blue widgets", "/products/widgets", 0, 0, 12.0)]);

        Assert.Equal(12.0, row.Page!.Position);
        Assert.Equal(0, row.Page.CtrPercent);
    }

    [Fact]
    public void No_figure_the_analysis_sends_is_NaN_or_infinite_for_empty_input()
    {
        var rows = TargetAnalysis.Build(["widgets"], [Q("blue widgets", 0, 0, 0)], [QP("blue widgets", "/a", 0, 0, 0)], 0);

        var row = Assert.Single(rows);
        Assert.True(double.IsFinite(row.CtrPercent));
        Assert.True(double.IsFinite(row.Position!.Value));
        Assert.True(double.IsFinite(row.Page!.Position));
        Assert.True(double.IsFinite(row.Page.CtrPercent));
    }

    // ---- Advice without a page ---------------------------------------------------------

    [Fact]
    public void Without_a_base_for_the_figures_the_advice_says_so_instead_of_claiming_zero_were_examined()
    {
        // A month archived before search queries were fetched has zero rows. "Not among the 0 most shown queries" is
        // meaningless and a claim we cannot make: we know nothing about that month. The advice must say the data is missing.
        var row = Assert.Single(TargetAnalysis.Build(["gadgets"], null, null, 0));

        Assert.Equal(TargetAnalysis.AdviceMissingNoQueries, row.Advice.Key);
        Assert.Empty(row.Advice.ArgNames());
    }

    [Fact]
    public void The_advice_for_a_missing_term_names_how_many_queries_were_examined()
    {
        // "Not visible" means "not among the N most shown", never "zero impressions". Search Console anonymizes the long
        // tail, so an absolute zero cannot be proven.
        var row = Row("gadgets", Q("blue widgets", 5, 100, 8));

        Assert.Equal(TargetAnalysis.AdviceMissing, row.Advice.Key);
        Assert.Equal(["rowsFetched"], row.Advice.ArgNames());
        Assert.Equal(Rows, row.Advice.Arg<int>("rowsFetched"));
    }

    [Fact]
    public void Without_a_page_link_the_advice_falls_back_to_the_general_one()
    {
        // A month archived before query-x-page was fetched has no pairs. The row must still work, with the general advice,
        // not with an empty sentence or a claim about a page.
        var row = Row("widgets", Q("blue widgets", 12, 600, 14.5));

        Assert.Null(row.Page);
        Assert.Equal(TargetAnalysis.AdviceNear, row.Advice.Key);
        Assert.Empty(row.Advice.ArgNames());
    }

    // ---- The page link -----------------------------------------------------------------

    [Fact]
    public void The_page_that_ranks_is_pointed_out()
    {
        // Without this the row says "strengthen the page that already ranks" without saying WHICH.
        var row = RowWith("widgets",
            [Q("blue widgets", 12, 600, 14.5)],
            [QP("blue widgets", "/products/widgets", 12, 600, 14.5)]);

        Assert.NotNull(row.Page);
        Assert.Equal("/products/widgets", row.Page.Path);
        Assert.Equal(600, row.Page.Impressions);
        Assert.Equal(1, row.Page.Queries);
    }

    [Fact]
    public void The_page_with_the_most_impressions_wins()
    {
        // Several pages can rank for one topic. The one that is SEEN most is the one to work on; clicks would be the wrong
        // measure here, since the whole point is often that the clicks do not come.
        var row = RowWith("widgets",
            [Q("blue widgets", 12, 600, 14.5), Q("widgets hosting", 1, 90, 22.0)],
            [
                QP("blue widgets", "/products/widgets", 12, 600, 14.5),
                QP("widgets hosting", "/blog/widgets-guide", 1, 90, 22.0),
            ]);

        Assert.Equal("/products/widgets", row.Page!.Path);
    }

    [Fact]
    public void The_page_position_is_weighted_by_impressions_over_its_pairs()
    {
        // (10 x 100 + 30 x 900) / 1000 = 28.0, the same weighting as for the term itself. A plain average would give 20.0,
        // "near page one", although 90 % of the impressions are at position 30.
        var row = RowWith("widgets",
            [Q("widgets a", 5, 100, 10.0), Q("widgets b", 1, 900, 30.0)],
            [
                QP("widgets a", "/products/widgets", 5, 100, 10.0),
                QP("widgets b", "/products/widgets", 1, 900, 30.0),
            ]);

        Assert.Equal(28.0, row.Page!.Position);
        Assert.Equal(2, row.Page.Queries);
    }

    [Fact]
    public void The_advice_names_the_page_its_position_and_its_queries()
    {
        var row = RowWith("widgets",
            [Q("blue widgets", 12, 600, 14.5)],
            [QP("blue widgets", "/products/widgets", 12, 600, 14.5)]);

        Assert.Equal(TargetAnalysis.AdvicePageNear, row.Advice.Key);
        Assert.Equal("/products/widgets", row.Advice.Arg<string>("path"));
        Assert.Equal(14.5, row.Advice.Arg<double>("position"));
        Assert.Equal(1, row.Advice.Arg<int>("queries"));
    }

    [Theory]
    [InlineData(24.5, 2, 400, TargetAnalysis.StatusDeep, TargetAnalysis.AdviceDeep, TargetAnalysis.AdvicePageDeep)]
    [InlineData(14.5, 2, 400, TargetAnalysis.StatusNear, TargetAnalysis.AdviceNear, TargetAnalysis.AdvicePageNear)]
    [InlineData(6.0, 2, 1000, TargetAnalysis.StatusTitle, TargetAnalysis.AdviceTitle, TargetAnalysis.AdvicePageTitle)]
    [InlineData(6.0, 50, 1000, TargetAnalysis.StatusPerforming, TargetAnalysis.AdvicePerforming, TargetAnalysis.AdvicePagePerforming)]
    public void The_advice_follows_the_status_and_names_the_page_only_when_it_is_known(
        double position, long clicks, long impressions, string status, string general, string withPage)
    {
        var query = Q("blue widgets", clicks, impressions, position);

        var without = Row("widgets", query);
        Assert.Equal(status, without.Status);
        Assert.Equal(general, without.Advice.Key);

        var with = RowWith("widgets", [query], [QP("blue widgets", "/products/widgets", clicks, impressions, position)]);
        Assert.Equal(status, with.Status);
        Assert.Equal(withPage, with.Advice.Key);
    }

    [Fact]
    public void The_advice_for_a_good_position_without_clicks_carries_the_click_through_rate()
    {
        var row = RowWith("widgets",
            [Q("blue widgets", 2, 1000, 6.0)],
            [QP("blue widgets", "/products/widgets", 2, 1000, 6.0)]);

        Assert.Equal(TargetAnalysis.AdvicePageTitle, row.Advice.Key);
        Assert.Equal(0.2, row.Advice.Arg<double>("ctrPercent"));
    }

    [Fact]
    public void Pairs_for_other_searches_do_not_affect_the_row()
    {
        var row = RowWith("widgets",
            [Q("blue widgets", 12, 600, 14.5)],
            [
                QP("blue widgets", "/products/widgets", 12, 600, 14.5),
                QP("green sprockets", "/", 40, 2000, 4.0),
            ]);

        Assert.Equal("/products/widgets", row.Page!.Path);
        Assert.Equal(600, row.Page.Impressions);
    }

    [Fact]
    public void A_term_without_matches_has_no_page()
    {
        var row = RowWith("gadgets",
            [Q("blue widgets", 12, 600, 14.5)],
            [QP("blue widgets", "/products/widgets", 12, 600, 14.5)]);

        Assert.Null(row.Page);
        Assert.Equal(TargetAnalysis.StatusMissing, row.Status);
    }

    [Fact]
    public void The_home_page_path_is_sent_as_it_is_and_the_client_names_it()
    {
        // "/" is a correct PATH but a meaningless sentence: "/ ranks at position 30.5". The path is data and the name is
        // presentation, so the server sends "/" untouched and en.ts says "The home page".
        var row = RowWith("gadgets",
            [Q("gadgets nearby", 0, 20, 30.5)],
            [QP("gadgets nearby", "/", 0, 20, 30.5)]);

        Assert.Equal(TargetAnalysis.AdvicePageDeep, row.Advice.Key);
        Assert.Equal("/", row.Advice.Arg<string>("path"));
    }

    [Fact]
    public void The_figures_are_raw_numbers_whatever_the_culture()
    {
        // The server never formats a number: it sends a number and the client formats it for the user's language. A formatted
        // text ("14,5" in one culture, "14.5" in another) would be a prose argument. The culture is forced to one that uses a
        // decimal comma to prove the figures do not depend on it.
        var before = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");

        try
        {
            var row = RowWith("widgets",
                [Q("blue widgets", 12, 600, 14.5)],
                [QP("blue widgets", "/products/widgets", 12, 600, 14.5)]);

            Assert.Equal(14.5, row.Advice.Arg<double>("position"));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    // ---- The position of one month -----------------------------------------------------

    [Fact]
    public void PositionOf_uses_the_same_weighting_as_the_rows()
    {
        var queries = new[] { Q("blue widgets", 10, 100, 5.0), Q("widgets error 500", 2, 900, 30.0) };

        Assert.Equal(27.5, TargetAnalysis.PositionOf("widgets", queries));
    }

    [Fact]
    public void PositionOf_is_null_without_a_match_or_without_queries()
    {
        Assert.Null(TargetAnalysis.PositionOf("gadgets", [Q("blue widgets", 10, 100, 5.0)]));
        Assert.Null(TargetAnalysis.PositionOf("widgets", null));
        Assert.Null(TargetAnalysis.PositionOf("widgets", []));
    }
}
