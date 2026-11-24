using System.Text.Json;
using Esatto.Umbraco.Backoffice.SeoReport.Api;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using NSubstitute;
using Xunit;
using static Esatto.Umbraco.Backoffice.SeoReport.Tests.Support.ApiResults;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// <c>GET analysis</c>: the movers, the opportunities, the written summary, the target queries and their history for a month, put
/// together from the archive. It reads the archive and nothing else (never Google), answers an empty archive with an empty answer
/// and never with an error, and keeps the quick wins from a user outside the findings groups, who does not get the findings either.
/// </summary>
public class AnalysisEndpointTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static async Task<ApiRig> RigAsync(ApiRig.Options? options = null, bool findings = false, bool terms = true)
    {
        var rig = await ApiRig.CreateAsync(options);

        foreach (var report in AnalysisArchive.Reports())
        {
            rig.Reports.ArchiveReport(findings && report.ReportPeriod == AnalysisArchive.Newest ? AnalysisArchive.WithFindings(report) : report);
        }

        if (terms) await rig.Targets.SaveAsync(AnalysisArchive.Terms);
        return rig;
    }

    private static async Task<AnalysisResponse> AnalysisAsync(ApiRig rig, string? period = null)
        => Ok<AnalysisResponse>(await rig.Controller.GetAnalysis(period, Ct));

    // ---- what it puts together --------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_newest_month_comes_with_its_movers_opportunities_summary_targets_history_and_note()
    {
        var rig = await RigAsync();
        await rig.Notes.SaveAsync(Period.Parse("2026-07"), "Relaunch.");

        var analysis = await AnalysisAsync(rig);

        Assert.True(analysis.HasAnalysis);
        Assert.Equal("2026-07", analysis.Period);
        Assert.Equal("Relaunch.", analysis.Note);

        var movement = analysis.Movement!;
        Assert.Equal("2026-06", movement.Previous!.Period);
        Assert.Equal("2025-07", movement.YearAgo!.Period);
        Assert.Equal(Movement.RankPrevious, movement.RankedBy);
        Assert.Equal(["/", "/hats"], movement.Losses.Select(m => m.Path));
        Assert.Equal(["/shoes", "/new"], movement.Gains.Select(m => m.Path));

        Assert.Equal([Opportunities.KindNear, Opportunities.KindCannibal], analysis.Opportunities!.Select(o => o.Kind)); // cheapest first
        Assert.Equal(
            [Narrative.KeySources, Narrative.KeyVisibility, Narrative.KeyNonBrand, Narrative.KeyConversion, Narrative.KeyMovement],
            analysis.Summary.Select(s => s.Text.Key));
    }

    [Fact]
    public async Task The_target_queries_are_held_up_against_the_month_in_the_editors_order_and_across_the_archive()
    {
        var rig = await RigAsync();

        var analysis = await AnalysisAsync(rig);

        var targets = analysis.Targets!;
        Assert.Equal(4, targets.RowsFetched); // what the month holds, not the code's limit
        Assert.Equal(["red shoes", "blue hat", "garden gnome"], targets.Rows.Select(r => r.Term));
        Assert.Equal([TargetAnalysis.StatusPerforming, TargetAnalysis.StatusNear, TargetAnalysis.StatusMissing], targets.Rows.Select(r => r.Status));
        Assert.Equal("/shoes", targets.Rows[0].Page!.Path);
        Assert.Null(targets.Rows[2].Page);

        var history = analysis.TargetHistory!;
        Assert.Equal(["2025-07", "2026-05", "2026-06", "2026-07"], history.Months.Select(m => m.Period));
        Assert.Equal(["red shoes", "blue hat", "garden gnome"], history.Rows.Select(r => r.Term));
        Assert.Equal([null, null, null, 14.2], history.Rows[1].Positions);
    }

    [Fact]
    public async Task A_month_asked_for_is_the_month_analysed()
    {
        var rig = await RigAsync();

        var analysis = await AnalysisAsync(rig, "2026-06");

        Assert.Equal("2026-06", analysis.Period);
        Assert.Equal("2026-06", analysis.Movement!.Period);
        Assert.Equal("2026-05", analysis.Movement.Previous!.Period);
        Assert.Null(analysis.Movement.YearAgo); // 2025-06 is not archived
        Assert.Contains(analysis.Summary, s => s.Text.Key == Narrative.KeySeason); // the caveat for a month compared with the one before it
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task No_month_is_the_newest_as_it_is_for_the_report(string? period)
    {
        var rig = await RigAsync();

        var analysis = await AnalysisAsync(rig, period);
        var report = Ok<ReportResponse>(await rig.Controller.GetReport(period, Ct));

        Assert.Equal(report.Period, analysis.Period);
        Assert.Equal("2026-07", analysis.Period);
    }

    [Fact]
    public async Task A_month_that_is_not_archived_gets_what_the_rest_of_the_archive_can_say_and_nothing_made_up()
    {
        var rig = await RigAsync();
        await rig.Notes.SaveAsync(Period.Parse("2026-03"), "Nothing was fetched.");

        var analysis = await AnalysisAsync(rig, "2026-03");

        Assert.Equal("2026-03", analysis.Period);
        Assert.Equal("Nothing was fetched.", analysis.Note);
        Assert.Null(analysis.Movement);
        Assert.Empty(analysis.Summary);
        Assert.Empty(analysis.Opportunities!);
        Assert.All(analysis.Targets!.Rows, row => Assert.Equal(TargetAnalysis.StatusMissing, row.Status));
        Assert.All(analysis.Targets.Rows, row => Assert.Equal("missingNoQueries", row.Advice.Key)); // no basis, and it says so
        Assert.Equal(0, analysis.Targets.RowsFetched);
        Assert.Equal(4, analysis.TargetHistory!.Months.Length); // the history belongs to the archive, not to the month
    }

    [Fact]
    public async Task An_empty_archive_is_hasAnalysis_false_and_nothing_else()
    {
        var rig = await ApiRig.CreateAsync();

        foreach (var period in new string?[] { null, "2026-07" })
        {
            var result = await rig.Controller.GetAnalysis(period, Ct);

            Assert.False(Ok<NoAnalysisResponse>(result).HasAnalysis);
            Assert.Equal("{\"hasAnalysis\":false}", ManagementJson.Body(result));
        }
    }

    // ---- what it refuses ----------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026-00")]
    [InlineData("2026-7")]
    [InlineData(" 2026-07")]
    [InlineData("2026-07 ")]
    [InlineData("../2026-07")]
    [InlineData("2026-07-01")]
    [InlineData("juli")]
    public async Task A_period_that_is_not_exactly_YYYY_MM_is_refused_before_anything_is_read(string period)
    {
        var rig = await RigAsync();

        var problem = Problem(await rig.Controller.GetAnalysis(period, Ct), 400);

        Assert.Equal(ApiCodes.PeriodInvalid, Code(problem));
        Assert.Equal(0, rig.Reports.ReportReads);
    }

    [Fact]
    public async Task The_analysis_reads_the_archive_and_never_fetches()
    {
        var rig = await RigAsync();

        await rig.Controller.GetAnalysis(null, Ct);
        await rig.Controller.GetAnalysis("2026-06", Ct);
        await rig.Controller.GetAnalysis("2026-03", Ct); // a month with nothing is an empty answer, not a reason to fetch
        await rig.Controller.GetAnalysis("2026-13", Ct);

        var empty = await ApiRig.CreateAsync();
        await empty.Controller.GetAnalysis(null, Ct);

        foreach (var each in new[] { rig, empty })
        {
            Assert.Empty(each.Reports.Refreshed);
            Assert.Empty(each.Spy.Starts);
            Assert.Equal(0, each.Google.Calls);
            Assert.Empty(each.Daily.ReceivedCalls());
        }
    }

    // ---- who sees the opportunities -------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_user_outside_the_findings_groups_gets_no_opportunities_and_the_rest_of_the_analysis()
    {
        var rig = await RigAsync(new ApiRig.Options(Groups: ["editors"], FindingsGroups: ["seo"]));

        var analysis = await AnalysisAsync(rig);

        Assert.Null(analysis.Opportunities); // not an empty list: the quick wins are not for this user at all
        Assert.NotNull(analysis.Movement);
        Assert.NotEmpty(analysis.Summary);
        Assert.NotNull(analysis.Targets);
    }

    [Fact]
    public async Task A_user_in_one_of_the_findings_groups_gets_them_whatever_the_case_of_the_alias()
    {
        var rig = await RigAsync(new ApiRig.Options(Groups: ["SEO", "editors"], FindingsGroups: ["seo"]));

        Assert.NotEmpty((await AnalysisAsync(rig)).Opportunities!);
    }

    [Theory]
    [InlineData("editors")]
    [InlineData("")] // a user who is in no group
    public async Task With_no_findings_groups_every_signed_in_user_gets_the_opportunities(string groups)
    {
        var rig = await RigAsync(new ApiRig.Options(Groups: groups.Split(',', StringSplitOptions.RemoveEmptyEntries), FindingsGroups: []));

        Assert.NotEmpty((await AnalysisAsync(rig)).Opportunities!);
    }

    [Theory]
    [InlineData("seo")]
    [InlineData("")]
    public async Task Nobody_signed_in_never_gets_the_opportunities_whatever_the_list_says(string findingsGroups)
    {
        var rig = await RigAsync(new ApiRig.Options(SignedIn: false, FindingsGroups: findingsGroups.Split(',', StringSplitOptions.RemoveEmptyEntries)));

        Assert.Null((await AnalysisAsync(rig)).Opportunities);
    }

    [Theory]
    [InlineData(new[] { "editors" }, new[] { "seo" })]
    [InlineData(new[] { "seo" }, new[] { "seo" })]
    [InlineData(new[] { "editors" }, new string[0])]
    public async Task No_part_of_the_analysis_repeats_a_finding_to_anyone(string[] groups, string[] findingsGroups)
    {
        // The warnings are the report's own to send (GET report, filtered); the analysis has no member that carries them, so it cannot
        // give to one user what the report keeps from another.
        var rig = await RigAsync(new ApiRig.Options(Groups: groups, FindingsGroups: findingsGroups), findings: true);

        var json = ManagementJson.Body(await rig.Controller.GetAnalysis(null, Ct));

        foreach (var text in new[] { "brandDominated", "noKeyEvents", "anonymizedQueries", "warning", "severity", "annotation", "findings" })
        {
            Assert.DoesNotContain(text, json);
        }
    }

    [Fact]
    public async Task The_archived_report_is_not_changed_by_reading_the_analysis()
    {
        var rig = await RigAsync(new ApiRig.Options(Groups: ["editors"], FindingsGroups: ["seo"]), findings: true);
        var archived = (await rig.Reports.GetReportAsync("2026-07", Ct))!;

        await AnalysisAsync(rig);

        Assert.Equal(2, archived.Traffic.Search!.Annotations.Count(a => a.Severity == "warning"));
    }

    // ---- the target queries ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task With_no_target_queries_there_are_no_target_rows_and_no_history()
    {
        var rig = await RigAsync(terms: false);

        var analysis = await AnalysisAsync(rig);

        Assert.Null(analysis.Targets);
        Assert.Null(analysis.TargetHistory);
        Assert.NotNull(analysis.Movement); // the rest of the analysis does not depend on them
    }

    [Fact]
    public async Task Blank_and_repeated_target_queries_never_reach_the_analysis()
    {
        var rig = await RigAsync(terms: false);
        await rig.Targets.SaveAsync(["  Red   Shoes ", "red shoes", "", "   ", "BLUE HAT"]);

        var analysis = await AnalysisAsync(rig);

        Assert.Equal(["red shoes", "blue hat"], analysis.Targets!.Rows.Select(r => r.Term));
        Assert.Equal(["red shoes", "blue hat"], analysis.TargetHistory!.Rows.Select(r => r.Term));
    }

    [Fact]
    public async Task A_month_without_a_search_block_has_rows_that_say_there_was_nothing_to_look_in()
    {
        var rig = await ApiRig.CreateAsync();
        rig.Reports.ArchiveReport(TestReports.With("2026-07"));
        await rig.Targets.SaveAsync(["red shoes"]);

        var analysis = await AnalysisAsync(rig);

        Assert.Equal(0, analysis.Targets!.RowsFetched);
        Assert.Equal("missingNoQueries", Assert.Single(analysis.Targets.Rows).Advice.Key);
        Assert.Null(analysis.TargetHistory); // no month with search queries: an empty table is worse than none
        Assert.Null(analysis.Movement);
    }

    // ---- an archive with little or nothing in it ---------------------------------------------------------------------------

    private static readonly Dictionary<string, Func<List<Report>>> Archives = new()
    {
        ["one month with no search block"] = () => [TestReports.With("2026-07")],
        ["empty lists everywhere"] = () => [TestReports.With("2026-06", queries: [], pages: [], queryPages: []), TestReports.With("2026-07", queries: [], pages: [], queryPages: [])],
        ["a new site with noData reports"] = () => [.. new[] { "2026-05", "2026-06", "2026-07" }.Select(p => TestReports.With(p) with { DataStatus = "noData" })],
        ["zero impressions and no position"] = () =>
        [
            TestReports.With("2026-06", queries: [AnalysisArchive.Query("red shoes", 0, 0, 0)], pages: [AnalysisArchive.Page("/", 0, 0)], queryPages: [AnalysisArchive.Pair("red shoes", "/", 0, 0)]),
            TestReports.With("2026-07", queries: [AnalysisArchive.Query("red shoes", 0, 0, 0)], pages: [AnalysisArchive.Page("/", 0, 0)], queryPages: [AnalysisArchive.Pair("red shoes", "/", 0, 0)]),
        ],
        ["key figures that are not numbers"] = () =>
        [
            TestReports.With(
                "2026-07",
                queries: [AnalysisArchive.Query("red shoes", 10, 3.0)],
                pages: [AnalysisArchive.Page("/", 10)],
                queryPages: [],
                kpis:
                [
                    TestReports.Kpi(ReportMapper.GscClicks, double.NaN, "up", double.NaN),
                    TestReports.Kpi(ReportMapper.OrganicSessions, double.PositiveInfinity, "down", double.NegativeInfinity),
                    TestReports.Kpi(ReportMapper.OrganicKeyEvents, double.PositiveInfinity),
                ]),
        ],
        ["months with pages only on one side"] = () =>
        [
            TestReports.With("2026-06", pages: []),
            TestReports.With("2026-07", pages: [AnalysisArchive.Page("/", 100)]),
        ],
    };

    public static TheoryData<string> ArchiveNames => new(Archives.Keys);

    [Theory]
    [MemberData(nameof(ArchiveNames))]
    public async Task A_thin_archive_gives_an_answer_with_empty_sections_and_valid_JSON(string name)
    {
        foreach (var terms in new[] { Array.Empty<string>(), ["red shoes", "garden gnome"] })
        {
            var rig = await ApiRig.CreateAsync();
            foreach (var report in Archives[name]()) rig.Reports.ArchiveReport(report);
            await rig.Targets.SaveAsync(terms);

            var result = await rig.Controller.GetAnalysis(null, Ct);
            var json = ManagementJson.Body(result); // throws on a number JSON cannot hold

            using var parsed = JsonDocument.Parse(json);
            Assert.True(parsed.RootElement.GetProperty("hasAnalysis").GetBoolean(), name);
            Assert.DoesNotContain("NaN", json);
            Assert.DoesNotContain("Infinity", json);
        }
    }
}
