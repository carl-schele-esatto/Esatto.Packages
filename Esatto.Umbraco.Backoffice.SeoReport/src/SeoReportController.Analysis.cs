using Esatto.Umbraco.Backoffice.SeoReport.Api;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Esatto.Umbraco.Backoffice.SeoReport;

// The analysis: what the archive says about one month and about the months around it.
public sealed partial class SeoReportController
{
    /// <summary>
    /// The analysis of a month: the pages that gained and lost most, the quick wins, the month in a few statements, the editor's
    /// target queries held up against it and their positions across the archive, and the month's note. No month is the newest.
    ///
    /// READS THE ARCHIVE AND NOTHING ELSE. Every month comes from <see cref="ISeoReportService"/>, which has them cached, and the
    /// analysis classes are pure functions over those reports, so opening the tab never reaches Google and never starts a fetch.
    /// An archive with nothing in it is <c>{ "hasAnalysis": false }</c> and not an error; so is a month that is not archived when
    /// the rest of the archive is: it gets what the archive can say (the history, its note) and sections that are empty.
    ///
    /// WHO SEES THE QUICK WINS. They are the same sort of advice as the findings under "To fix", which a user outside the findings
    /// groups does not get (see <see cref="VisibleAsync"/>), so they are withheld from that user too (null, not an empty list):
    /// otherwise the restriction would be one tab deep. The warnings themselves are not part of this answer for anyone; they are
    /// the report's own and travel, filtered, in <see cref="GetReport"/>. Nothing else in the analysis is withheld: the movers, the
    /// summary and the target queries are figures and not findings.
    ///
    /// The visibility of the archive's months is worked out once and feeds the summary only; the table it once made is gone on
    /// purpose (it presented the row limit as if it were a measurement), so it is not sent.
    /// </summary>
    /// <param name="period"><c>YYYY-MM</c>, exactly, or none for the newest.</param>
    /// <response code="200">
    /// The analysis. <c>hasAnalysis</c> is true. When it is false the body is exactly <c>{ "hasAnalysis": false }</c> (an
    /// <see cref="NoAnalysisResponse"/>) and none of the other members is there: a client reads <c>hasAnalysis</c> first.
    /// </response>
    // ONE DECLARED 200 TYPE. ASP.NET's ApiExplorer keeps one entry per status code and the last attribute wins, so declaring the empty
    // answer as a second 200 would replace this type in the OpenAPI document (and in the generated client) with { hasAnalysis }.
    // ResponseTypesTests holds every action to one declared type per status code.
    [HttpGet("analysis")]
    [ProducesResponseType(typeof(AnalysisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAnalysis([FromQuery] string? period, CancellationToken ct)
    {
        // Before anything is read: the value comes from a query string and never becomes a key or a query unchecked.
        var newest = string.IsNullOrWhiteSpace(period);
        if (!newest && !Period.TryParse(period, out _)) return PeriodInvalid();

        var reports = new List<Report>();
        foreach (var archived in await _reports.GetAvailablePeriodsAsync(ct))
        {
            // A month that is listed and cannot be read now (another instance replaced it, or the settings changed) is left out.
            if (await _reports.GetReportAsync(archived, ct) is { } report) reports.Add(report);
        }

        if (reports.Count == 0) return Ok(new NoAnalysisResponse());

        // The newest of the months just read, so the month and the months it is compared with come from one snapshot of the archive.
        var current = newest ? reports.MaxBy(x => x.ReportPeriod, StringComparer.Ordinal)!.ReportPeriod : period!;

        var month = reports.FirstOrDefault(x => x.ReportPeriod == current);
        var movement = Movement.Compare(reports, current);
        var terms = await _targets.GetAsync();

        return Ok(new AnalysisResponse(
            current,
            [.. Narrative.Write(month, movement, Visibility.PerMonth(reports))],
            Period.TryParse(current, out var noted) ? await _notes.GetAsync(noted) : null,
            TargetHistory.Grid(terms, reports),
            movement,
            await CanSeeFindingsAsync() ? [.. Opportunities.Find(month)] : null,
            TargetsFor(month, terms)));
    }

    /// <summary>
    /// The target queries held up against the month shown. Calculated when it is read and never archived: a term added tomorrow
    /// applies to every month at once. Null without terms, as the table is not drawn then. The depth is the number of queries the
    /// month really holds, so that "not among the N most shown" says the right N for a month fetched under a smaller limit.
    /// </summary>
    private static TargetAnalysisResponse? TargetsFor(Report? report, IReadOnlyList<string> terms)
    {
        if (terms.Count == 0) return null;

        var search = report?.Traffic?.Search;
        var rowsFetched = search?.Queries?.Count ?? 0;

        return new TargetAnalysisResponse(rowsFetched, [.. TargetAnalysis.Build(terms, search?.Queries, search?.QueryPages, rowsFetched)]);
    }
}
