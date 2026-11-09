using Esatto.Umbraco.Backoffice.Seo.Checks;
using Esatto.Umbraco.Backoffice.Seo.Knowledge;
using Esatto.Umbraco.Backoffice.Seo.Reporting;
using Esatto.Umbraco.Backoffice.Seo.Scanning;
using Esatto.Umbraco.Backoffice.Seo.Settings;
using Esatto.Umbraco.Backoffice.Seo.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Seo.Tests;

public class ReportServiceTests
{
    private static readonly Guid Bare = Guid.NewGuid();
    private const string Noindex = """<meta name="robots" content="noindex">""";

    internal static ReportService Reports(Rig rig)
    {
        var env = new SeoEnvironment(rig.Configuration);
        return new ReportService(rig.Store, rig.Coordinator, env, rig.Cache);
    }

    internal static IgnoreService Ignores(Rig rig)
        => new(rig.Store, new SnapshotLoader(rig.Store, new SeoEnvironment(rig.Configuration)), rig.Cache, rig.RowLock);

    private static async Task<Rig> Scanned()
    {
        var rig = new Rig()
            .Page("/", root: true)
            .Page("/bare", "<html lang=\"en\"><body><main><p>x</p></main></body></html>", key: Bare)
            .Page("/hidden", Snap.Good("/hidden", head: Noindex))
            .Page("/sv/a", Snap.Good("/sv/a", "sv", title: "x"), culture: "sv");
        await rig.RunAsync();
        return rig;
    }

    [Fact]
    public async Task Overview_without_scan_is_empty()
    {
        var o = await Reports(new Rig()).GetOverviewAsync(Rig.Site);
        Assert.Null(o.LastScan);
        Assert.Null(o.Score);
        Assert.Empty(o.Top);
    }

    [Fact]
    public async Task Overview_top_five_by_priority()
    {
        var o = await Reports(await Scanned()).GetOverviewAsync(Rig.Site);
        Assert.NotNull(o.Score);
        Assert.Equal(5, o.Top.Count);
        Assert.Equal(Severity.Critical, o.Top[0].Severity);
        Assert.Contains(o.Top, t => t.CheckId == CheckIds.TitleMissing);
        Assert.True(o.Top.Zip(o.Top.Skip(1)).All(p => p.First.Priority >= p.Second.Priority));
        Assert.True(o.Counts.Critical >= 1);
    }

    [Fact]
    public async Task Overview_lists_hidden_pages_and_site_checks()
    {
        var o = await Reports(await Scanned()).GetOverviewAsync(Rig.Site);
        var hidden = Assert.Single(o.Hidden);
        Assert.EndsWith("/hidden", hidden.Url);
        Assert.Equal("robots meta noindex", hidden.Reason);
        Assert.Contains(o.SiteChecks, c => c.CheckId == CheckIds.SitemapMissing && c.Passed == false);
        Assert.Contains(o.SiteChecks, c => c.CheckId == CheckIds.HttpsRedirect && c.Passed is not null);
    }

    [Fact]
    public async Task Overview_shows_latest_failed_attempt_message()
    {
        var rig = new Rig().Page("/", url: "/", root: true);
        await rig.RunAsync();
        var o = await Reports(rig).GetOverviewAsync(Rig.Site);
        Assert.Null(o.LastScan);
        Assert.Contains("base URL", o.LatestAttempt!.Message);
    }

    [Fact]
    public async Task Issues_filter_by_owner_and_culture()
    {
        var reports = Reports(await Scanned());
        var editor = await reports.GetIssuesAsync(Rig.Site, new IssueFilter(Owner: Owner.Editor));
        Assert.All(editor, g => Assert.Equal(Owner.Editor, g.Summary.Owner));

        var sv = await reports.GetIssuesAsync(Rig.Site, new IssueFilter(Culture: "sv"));
        Assert.All(sv.SelectMany(g => g.Occurrences), o => Assert.Equal("sv", o.Culture));
        Assert.Contains(sv, g => g.Summary.CheckId == CheckIds.TitleLength);
    }

    [Fact]
    public async Task Issues_search_matches_url_or_name()
    {
        var groups = await Reports(await Scanned()).GetIssuesAsync(Rig.Site, new IssueFilter(Search: "bare"));
        Assert.NotEmpty(groups);
        Assert.All(groups.SelectMany(g => g.Occurrences), o => Assert.Contains("bare", o.Url));
    }

    [Fact]
    public async Task Ignored_shown_only_with_flag()
    {
        var rig = await Scanned();
        var id = await Ignores(rig).AddAsync(Rig.Site, CheckIds.TitleMissing, Bare, null, "Landing page without title on purpose", "Carl");
        var reports = Reports(rig);

        Assert.DoesNotContain(await reports.GetIssuesAsync(Rig.Site, new IssueFilter()), g => g.Summary.CheckId == CheckIds.TitleMissing);
        var ignored = await reports.GetIssuesAsync(Rig.Site, new IssueFilter(Ignored: true));
        var occurrence = ignored.Single(g => g.Summary.CheckId == CheckIds.TitleMissing).Occurrences.Single();
        Assert.True(occurrence.Ignored);
        Assert.Equal(id, occurrence.IgnoreId);
    }

    [Fact]
    public async Task Ignore_requires_note_and_known_check()
    {
        var rig = await Scanned();
        await Assert.ThrowsAsync<ArgumentException>(() => Ignores(rig).AddAsync(Rig.Site, CheckIds.TitleMissing, Bare, null, "  ", "Carl"));
        await Assert.ThrowsAsync<ArgumentException>(() => Ignores(rig).AddAsync(Rig.Site, "nope", Bare, null, "note", "Carl"));
    }

    [Fact]
    public async Task Ignore_recomputes_score_and_remove_restores_it()
    {
        var rig = await Scanned();
        var before = rig.Store.Scans.Single().Score;
        var id = await Ignores(rig).AddAsync(Rig.Site, CheckIds.TitleMissing, null, null, "We know", "Carl");
        var ignoredScore = rig.Store.Scans.Single().Score;
        Assert.True(ignoredScore > before);
        Assert.Equal(ignoredScore, (await Reports(rig).GetOverviewAsync(Rig.Site)).Score);

        await Ignores(rig).RemoveAsync(id);
        Assert.Equal(before, rig.Store.Scans.Single().Score);
    }
}

public class CsvWriterTests
{
    [Fact]
    public void Escapes_quotes_newlines_and_formulas()
    {
        var group = new IssueGroup(
            new Evaluation.IssueSummary(CheckIds.TitleLength, Severity.Warning, Owner.Editor, 1, 1, 10),
            [new IssueOccurrence(Guid.Empty, "=SUM(A1)", "sv", "https://esatto.se/a", "61 characters: \"Hej,\nvärlden\"", false, null)]);
        var csv = CsvWriter.Write([group], new KnowledgeBase());
        var lines = csv.Split("\r\n");
        Assert.Equal("Check,Severity,Owner,Page,Language,URL,Value", lines[0].TrimStart((char)0xFEFF));
        Assert.StartsWith("Page title too short or too long,Warning,Editor,'=SUM(A1),sv,https://esatto.se/a,\"61 characters: \"\"Hej,", lines[1]);
    }
}
