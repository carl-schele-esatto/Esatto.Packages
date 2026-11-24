using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The analysis classes (opportunities, target analysis, narrative, daily chart) send translation keys and raw figures, never
/// prose. These tests read what they emit the way the client does, by where a text sits: an opportunity's text is looked up in
/// area "opportunity" with the parts Title and Text, a target row's status in area "target" with the part Label and its advice
/// with the part Text, a narrative statement in area "narrative" with the part Text, the daily chart's id in area "chart" with
/// the parts Title, Subtitle and Description and its series keys in area "series" with the part Label. They check that each is
/// registered, that every registered one is reached, and that en.ts reads exactly the arguments sent.
///
/// A text always carries the SAME arguments: a part that has nothing to say (a comparison month that does not exist) is sent
/// as null, not left out, so the client can tell "missing" from "never sent" and this test can hold every sample to one set.
/// </summary>
public class AnalysisTextsTests
{
    /// <summary>One text an analysis names: where it sits, its area, base id and parts, and the argument names it carries.</summary>
    private sealed record Emitted(string Location, string Area, string BaseId, string[] Parts, string[] ArgNames)
    {
        public IEnumerable<string> Keys => Parts.Select(part => ReportKeys.Lookup(Area, BaseId, part));
    }

    private static SearchQuery Q(string query, long clicks, long impressions, double position) =>
        new(query, clicks, impressions, impressions == 0 ? 0 : Math.Round(clicks / (double)impressions * 100.0, 2), position);

    private static SearchQueryPage QP(string query, string path, long clicks, long impressions, double position) =>
        new(query, $"https://example.com{path}", path, clicks, impressions,
            impressions == 0 ? 0 : Math.Round(clicks / (double)impressions * 100.0, 2), position);

    /// <summary>A report in which both kinds of opportunity occur.</summary>
    private static IEnumerable<Emitted> OpportunityTexts()
    {
        var report = TestReports.With(
            "2026-07",
            queries: [Q("blue widgets", 5, 400, 15.0)],
            queryPages: [QP("red gadgets", "/one", 0, 300, 8.0), QP("red gadgets", "/two", 0, 200, 18.0)]);

        return Opportunities.Find(report).Select(o =>
            new Emitted("Opportunity.Text", "opportunity", o.Text.Key, ["Title", "Text"], o.Text.ArgNames()));
    }

    /// <summary>
    /// Target rows that between them reach every status and every piece of advice: five terms, one per status, once without
    /// and once with the page link, plus a term in a month that has no search queries at all.
    /// </summary>
    private static IEnumerable<Emitted> TargetTexts()
    {
        string[] terms = ["alpha", "beta", "gamma", "delta", "zeta"];
        SearchQuery[] queries =
        [
            Q("alpha one", 2, 400, 24.5),      // deep
            Q("beta one", 2, 400, 14.5),       // near
            Q("gamma one", 2, 1000, 6.0),      // title
            Q("delta one", 50, 1000, 6.0),     // performing
        ];
        SearchQueryPage[] pages = [.. queries.Select(q => QP(q.Query, "/" + q.Query.Replace(' ', '-'), q.Clicks, q.Impressions, q.Position))];

        var rows = TargetAnalysis.Build(terms, queries, null, 250)
            .Concat(TargetAnalysis.Build(terms, queries, pages, 250))
            .Concat(TargetAnalysis.Build(["zeta"], null, null, 0));

        return rows.SelectMany(row => new[]
        {
            new Emitted("TargetRow.Status", "target", row.Status, ["Label"], []),
            new Emitted("TargetRow.Advice", "target", row.Advice.Key, ["Text"], row.Advice.ArgNames()),
        });
    }

    private static VisibilityMonth B(string period, int top3, int total = 200) => new(period, top3, total);

    private static Move Loss(double? position, MoveDelta? vsPrevious, MoveDelta? vsYearAgo) =>
        new("/example/page", "https://example.com/example/page", 1000, position, vsPrevious, vsYearAgo);

    /// <summary>
    /// Narratives that between them reach every statement and every variant of one: sources that agree and disagree, a visibility
    /// sentence with a year comparison and a best month, with the fallback month and with no comparison at all, and a drop with
    /// both comparisons and positions, with one comparison and none.
    /// </summary>
    private static IEnumerable<Emitted> NarrativeTexts()
    {
        var delta = new MoveDelta(1400, -400, -28.6, 20.5);

        var disagreeing = TestReports.With(
            "2026-07",
            kpis:
            [
                TestReports.Kpi("gsc_clicks", 120, "up", 10.0),
                TestReports.Kpi("organic_sessions", 80, "down", -20.0),
                TestReports.Kpi("organic_key_events", 2),
            ],
            segments: [TestReports.Segment("non_brand", 20)]);

        var agreeing = TestReports.With(
            "2026-07",
            kpis: [TestReports.Kpi("gsc_clicks", 120, "up", 10.0), TestReports.Kpi("organic_sessions", 80, "up", 9.0)]);

        var both = new MovementResult(
            "2026-07", new Comparison("2026-06"), new Comparison("2025-07"), Movement.RankYearAgo,
            [Loss(35.5, delta, delta)], []);
        var previousOnly = new MovementResult(
            "2026-07", new Comparison("2026-06"), null, Movement.RankPrevious, [Loss(null, delta, null)], []);

        (Report? Report, MovementResult? Movement, VisibilityMonth[] Visibility)[] inputs =
        [
            (disagreeing, both, [B("2025-07", 10), B("2026-01", 40), B("2026-07", 20)]),
            (agreeing, previousOnly, [B("2026-06", 10), B("2026-07", 20)]),
            (TestReports.With("2026-07"), null, [B("2026-07", 20)]),
        ];

        return inputs
            .SelectMany(i => Narrative.Write(i.Report, i.Movement, i.Visibility))
            .Select(s => new Emitted("Statement.Text", "narrative", s.Text.Key, ["Text"], s.Text.ArgNames()));
    }

    /// <summary>The daily chart in both modes: its id gives the chart texts, its series keys the series labels.</summary>
    private static IEnumerable<Emitted> DailyChartTexts()
    {
        var today = new DateOnly(2026, 8, 28);

        foreach (var month in new string?[] { null, "2026-06" })
        {
            var window = Ga4DailyAgent.WindowFor(today, month);
            var chart = DailyChart.Build(Ga4DailyAgent.Split([new Ga4DailyPoint(window.Start, 3)], window));
            var args = DailyChart.TextArgs(window, today).Keys.ToArray();

            yield return new Emitted("ChartBlock.Id (daily)", "chart", chart.Id, ["Title", "Subtitle", "Description"], args);

            foreach (var series in chart.Series)
            {
                yield return new Emitted("Series.Key (daily)", "series", series.Key, ["Label"], []);
            }
        }
    }

    private static List<Emitted> AllTexts() => [.. OpportunityTexts(), .. TargetTexts(), .. NarrativeTexts(), .. DailyChartTexts()];

    [Fact]
    public void The_samples_name_both_kinds_of_opportunity_and_every_status()
    {
        Assert.Equal(["cannibal", "near"], OpportunityTexts().Select(x => x.BaseId).Order());
        Assert.Equal(TargetAnalysis.Statuses.Order(), TargetTexts().Where(x => x.Location == "TargetRow.Status").Select(x => x.BaseId).Distinct().Order());
        Assert.Equal(Narrative.Kinds.Order(), NarrativeTexts().Select(x => x.BaseId).Distinct().Order());
        Assert.Equal([DailyChart.ChartId], DailyChartTexts().Where(x => x.Area == "chart").Select(x => x.BaseId).Distinct());
        Assert.Equal(
            new[] { DailyChart.SeriesBaseline, DailyChart.SeriesDaily }.Order(),
            DailyChartTexts().Where(x => x.Area == "series").Select(x => x.BaseId).Distinct().Order());
    }

    [Fact]
    public void Every_text_an_analysis_names_resolves_to_a_registered_key()
    {
        var uses = AllTexts();

        Assert.NotEmpty(uses);
        Assert.All(uses, use => Assert.All(use.Keys, key => Assert.True(ReportKeys.All.Contains(key), $"{use.Location}: {key} is not registered")));
    }

    [Fact]
    public void The_samples_reach_every_text_the_analysis_classes_register()
    {
        // No registered key is dead: each one is emitted by some sample.
        var emitted = AllTexts().SelectMany(use => use.Keys).ToHashSet(StringComparer.Ordinal);

        Assert.Empty(ReportKeys.AnalysisKeys().SelectMany(keys => keys).Except(emitted));
    }

    [Fact]
    public void A_text_only_reads_arguments_the_server_sends_and_every_argument_sent_is_read()
    {
        // Each text (an area and base id) with the argument names it is sent with, across every sample. The English function
        // of each part reads some of them; between them the parts must read exactly the arguments sent.
        var groups = AllTexts().GroupBy(u => (u.Area, u.BaseId)).ToList();

        Assert.NotEmpty(groups);
        foreach (var group in groups)
        {
            var sent = group.SelectMany(u => u.ArgNames).ToHashSet(StringComparer.Ordinal);
            var read = new HashSet<string>(StringComparer.Ordinal);

            foreach (var key in group.SelectMany(u => u.Keys).Distinct())
            {
                var parameters = EnglishTextsTests.Parameters(key);
                Assert.True(parameters.IsSubsetOf(sent), $"{key} reads {string.Join(", ", parameters.Except(sent))}, which the server never sends");
                read.UnionWith(parameters);
            }

            Assert.True(sent.IsSubsetOf(read), $"{group.Key.Area}/{group.Key.BaseId} is sent {string.Join(", ", sent.Except(read))}, which no text reads");
        }
    }

    [Fact]
    public void A_text_sent_with_the_same_id_always_carries_the_same_arguments()
    {
        // The client resolves a text by where it sits and its id alone, so one id at one location with different argument sets
        // would have some of its figures read as missing.
        Assert.All(
            AllTexts().GroupBy(u => (u.Location, u.Area, u.BaseId)),
            group => Assert.Single(group.Select(u => string.Join(",", u.ArgNames.Order())).Distinct()));
    }
}
