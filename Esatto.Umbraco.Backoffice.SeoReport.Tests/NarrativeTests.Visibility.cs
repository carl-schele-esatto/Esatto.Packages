using System.Text.Json;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>The visibility sentence and the conversions of <see cref="NarrativeTests"/>.</summary>
public partial class NarrativeTests
{
    // ---- Visibility as a sentence instead of a table -------------------------------------

    private static VisibilityMonth B(string period, int top3, int total = 200) => new(period, top3, total);

    private static TextRef VisibilityOf(string period, params VisibilityMonth[] months) =>
        Only(Narrative.Write(TestReports.With(period), movement: null, visibility: months), Narrative.KeyVisibility);

    [Fact]
    public void The_top_three_share_is_compared_with_the_same_month_last_year()
    {
        // The table this replaced said the basket size on every row, which was the row limit and not a measurement. The share is
        // the only honest way to read a basket of fixed size.
        var text = VisibilityOf("2026-07", B("2025-07", 10), B("2026-07", 20));

        Assert.Equal(20, text.Arg<int>("top3"));
        Assert.Equal(200, text.Arg<int>("total"));
        Assert.Equal(10d, text.Arg<double>("percent"));
        Assert.Equal("2025-07", text.Arg<string>("comparePeriod"));
        Assert.Equal(10, text.Arg<int>("compareTop3"));
        Assert.Equal(5d, text.Arg<double>("comparePercent"));
        Assert.True(text.Arg<bool>("yearOnYear"));
    }

    [Fact]
    public void The_share_is_rounded_to_one_decimal()
    {
        // 7 of 150 is 4.666...
        var text = VisibilityOf("2026-07", B("2026-07", 7, 150));

        Assert.Equal(4.7, text.Arg<double>("percent"));
    }

    [Fact]
    public void The_best_month_is_named_only_when_it_is_a_third_month()
    {
        var text = VisibilityOf("2026-07", B("2025-07", 10), B("2026-01", 40), B("2026-07", 20));

        Assert.Equal("2026-01", text.Arg<string>("bestPeriod"));
        Assert.Equal(40, text.Arg<int>("bestTop3"));
        Assert.Equal(20d, text.Arg<double>("bestPercent"));
    }

    [Fact]
    public void The_best_month_is_left_out_when_it_is_the_shown_month_or_the_comparison()
    {
        // Naming it again would only repeat the sentence.
        var shownIsBest = VisibilityOf("2026-07", B("2026-01", 10), B("2026-07", 40));
        var comparisonIsBest = VisibilityOf("2026-07", B("2025-07", 40), B("2026-07", 20));

        shownIsBest.AssertNullArg("bestPeriod");
        shownIsBest.AssertNullArg("bestTop3");
        shownIsBest.AssertNullArg("bestPercent");
        comparisonIsBest.AssertNullArg("bestPeriod");
    }

    [Fact]
    public void The_best_month_is_ranked_by_the_exact_share_not_by_the_rounded_one()
    {
        // 23 of 240 is 9.583 and 24 of 250 is 9.6: both print as 9.6, but the shown month is the higher of the two. Ranked on the
        // rounded figure the two would tie, the earlier one would win, and the statement would name January as the best month
        // right after saying 9.6 percent for July. The basket sizes differ because the total is the number of queries examined.
        var near = new[] { B("2025-07", 10, 250), B("2026-01", 23, 240), B("2026-07", 24, 250) };

        foreach (var months in new[] { near, [.. Enumerable.Reverse(near)] })
        {
            var text = VisibilityOf("2026-07", months);

            Assert.Equal(9.6, text.Arg<double>("percent"));
            text.AssertNullArg("bestPeriod");
            text.AssertNullArg("bestTop3");
            text.AssertNullArg("bestPercent");
        }
    }

    [Fact]
    public void The_exact_share_also_decides_when_the_earlier_month_is_the_higher_of_two_equal_looking_ones()
    {
        // The same pair the other way round: January is the higher by a hair, and it is named.
        var near = new[] { B("2025-07", 10, 250), B("2026-01", 24, 250), B("2026-07", 23, 240) };

        foreach (var months in new[] { near, [.. Enumerable.Reverse(near)] })
        {
            var text = VisibilityOf("2026-07", months);

            Assert.Equal("2026-01", text.Arg<string>("bestPeriod"));
            Assert.Equal(24, text.Arg<int>("bestTop3"));
        }
    }

    [Fact]
    public void The_percent_that_is_shown_is_the_rounded_share_of_the_month_that_was_ranked_on_the_exact_one()
    {
        // January (23 of 240 = 9.5833...) is the best month and is shown as 9.6; the ranking does not leak into the figure.
        var text = VisibilityOf("2026-07", B("2025-07", 10, 250), B("2026-01", 23, 240), B("2026-07", 20, 250));

        Assert.Equal("2026-01", text.Arg<string>("bestPeriod"));
        Assert.Equal(23, text.Arg<int>("bestTop3"));
        Assert.Equal(9.6, text.Arg<double>("bestPercent"));
        Assert.Equal(8d, text.Arg<double>("percent"));
        Assert.Equal(4d, text.Arg<double>("comparePercent"));
    }

    [Fact]
    public void Months_with_exactly_the_same_share_tie_to_the_earliest_one()
    {
        // 10 of 200 and 20 of 400 are the same share to the last bit. The earliest month wins the tie (a stable sort over the
        // months in chronological order, which is the order the archive delivers them in), whatever order they arrive in.
        var tied = new[] { B("2025-07", 5), B("2025-10", 10), B("2026-01", 20, 400), B("2026-07", 6) };

        foreach (var months in new[] { tied, [.. Enumerable.Reverse(tied)] })
        {
            var text = VisibilityOf("2026-07", months);

            Assert.Equal("2025-10", text.Arg<string>("bestPeriod"));
            Assert.Equal(10, text.Arg<int>("bestTop3"));
            Assert.Equal(5d, text.Arg<double>("bestPercent"));
        }
    }

    [Fact]
    public void A_part_without_anything_to_say_is_sent_as_null_on_the_wire()
    {
        // The client reads "missing" as null, so the argument must reach it: left out would be a different shape per statement.
        var json = JsonSerializer.SerializeToElement(VisibilityOf("2026-07", B("2026-07", 20)));
        var args = json.GetProperty("args");

        Assert.Equal("visibility", json.GetProperty("key").GetString());
        Assert.Equal(20, args.GetProperty("top3").GetInt32());
        Assert.Equal(JsonValueKind.Null, args.GetProperty("comparePeriod").ValueKind);
        Assert.Equal(JsonValueKind.Null, args.GetProperty("yearOnYear").ValueKind);
        Assert.Equal(JsonValueKind.Null, args.GetProperty("bestPercent").ValueKind);
    }

    [Fact]
    public void A_single_month_is_a_statement_without_a_comparison()
    {
        var text = VisibilityOf("2026-07", B("2026-07", 20));

        Assert.Equal(20, text.Arg<int>("top3"));
        text.AssertNullArg("comparePeriod");
        text.AssertNullArg("compareTop3");
        text.AssertNullArg("comparePercent");
        text.AssertNullArg("yearOnYear");
        text.AssertNullArg("bestPeriod");
    }

    [Fact]
    public void Without_a_distribution_for_the_shown_month_nothing_is_said()
    {
        var said = Narrative.Write(TestReports.With("2026-07"), movement: null, visibility: [B("2026-06", 20)]);

        Assert.DoesNotContain(Narrative.KeyVisibility, Keys(said));
    }

    [Fact]
    public void A_comparison_that_is_not_year_on_year_says_so()
    {
        // Traffic is seasonal, and a share compared with the previous month can move because of the season and not the work. When
        // the sentence falls back it has to say so, or June is read as a deterioration in July.
        var text = VisibilityOf("2026-07", B("2026-06", 10), B("2026-07", 20));

        Assert.Equal("2026-06", text.Arg<string>("comparePeriod"));
        Assert.False(text.Arg<bool>("yearOnYear"));
    }

    [Fact]
    public void The_fallback_is_the_nearest_earlier_month_when_the_archive_has_gaps()
    {
        var text = VisibilityOf("2026-07", B("2026-02", 10), B("2026-03", 12), B("2026-07", 20));

        Assert.Equal("2026-03", text.Arg<string>("comparePeriod"));
        Assert.False(text.Arg<bool>("yearOnYear"));
    }

    [Fact]
    public void A_later_month_is_never_the_comparison()
    {
        var text = VisibilityOf("2026-07", B("2026-07", 20), B("2026-09", 90));

        text.AssertNullArg("comparePeriod");
        Assert.Equal("2026-09", text.Arg<string>("bestPeriod"));
    }

    [Fact]
    public void The_months_may_come_in_any_order()
    {
        var sorted = VisibilityOf("2026-07", B("2025-07", 10), B("2026-01", 40), B("2026-07", 20));
        var reversed = VisibilityOf("2026-07", B("2026-07", 20), B("2026-01", 40), B("2025-07", 10));

        Assert.Equal(sorted.Args, reversed.Args, new ArgsComparer());
    }

    [Fact]
    public void A_month_without_examined_queries_is_not_a_comparison_and_never_divides_by_zero()
    {
        // A month with a total of zero has no share. It is skipped as comparison and as best month, so no figure is NaN or
        // infinity and the comparison falls back to the nearest month that does have one.
        var text = VisibilityOf("2026-07", B("2025-07", 0, 0), B("2026-01", 0, 0), B("2026-06", 10), B("2026-07", 20));

        Assert.Equal("2026-06", text.Arg<string>("comparePeriod"));
        Assert.False(text.Arg<bool>("yearOnYear"));
        text.AssertNullArg("bestPeriod");
        text.AssertFinite();
    }

    [Fact]
    public void A_shown_month_without_examined_queries_gives_no_statement()
    {
        var said = Narrative.Write(TestReports.With("2026-07"), movement: null, visibility: [B("2026-07", 0, 0), B("2025-07", 10)]);

        Assert.DoesNotContain(Narrative.KeyVisibility, Keys(said));
    }

    [Fact]
    public void A_report_period_that_is_not_a_month_is_read_without_error()
    {
        // "Not a month" cannot have a same month last year: the statement falls back, it does not throw.
        var said = Narrative.Write(
            TestReports.With("latest"), movement: null, visibility: [B("2026-06", 10), B("latest", 20)]);

        Assert.False(Only(said, Narrative.KeyVisibility).Arg<bool>("yearOnYear"));
    }

    // ---- Conversions against the traffic -------------------------------------------------

    private static IReadOnlyList<Statement> Conversion(double? events, double? sessions)
    {
        var kpis = new List<Kpi>();
        if (events is { } e) kpis.Add(K("organic_key_events", e));
        if (sessions is { } s) kpis.Add(K("organic_sessions", s));

        return Narrative.Write(Report(kpis), movement: null, visibility: []);
    }

    [Fact]
    public void Conversions_are_set_against_the_traffic_that_produced_them()
    {
        // The point: "1 key event" says almost nothing, while the same number set against the visits is something to act on.
        var text = Only(Conversion(events: 1, sessions: 30), Narrative.KeyConversion);

        Assert.Equal(30d, text.Arg<double>("sessions"));
        Assert.Equal(1d, text.Arg<double>("events"));
    }

    [Fact]
    public void No_rate_is_calculated()
    {
        // 1 of 30 is 3.3 percent, but one event more or less makes it 0 or 6.7. A conversion rate on that basis is noise with
        // decimals, and it would be read with the same confidence as the figures it is built on: only the two counts are sent.
        var text = Only(Conversion(events: 1, sessions: 30), Narrative.KeyConversion);

        Assert.Equal(["events", "sessions"], text.ArgNames().Order());
    }

    [Fact]
    public void Without_conversions_the_statement_is_absent()
    {
        // The usual case until key events are set up in GA4: the mapper does not even add the KPI when everything is zero.
        Assert.DoesNotContain(Narrative.KeyConversion, Keys(Conversion(events: null, sessions: 30)));
    }

    [Fact]
    public void Zero_conversions_give_no_statement_either()
    {
        // Should the KPI ever carry zero, the sentence must not start claiming "0 key events": that reads as an outcome, not as a
        // missing measurement.
        Assert.DoesNotContain(Narrative.KeyConversion, Keys(Conversion(events: 0, sessions: 30)));
    }

    [Fact]
    public void Without_the_traffic_the_statement_is_absent()
    {
        // Without the visits to set the number against, the sentence is just the KPI again: the JOIN of the two figures is the
        // analysis.
        Assert.DoesNotContain(Narrative.KeyConversion, Keys(Conversion(events: 1, sessions: null)));
        Assert.DoesNotContain(Narrative.KeyConversion, Keys(Conversion(events: 1, sessions: 0)));
    }
}
