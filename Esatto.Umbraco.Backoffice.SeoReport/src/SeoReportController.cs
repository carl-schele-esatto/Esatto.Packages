using Esatto.Umbraco.Backoffice.SeoReport.Api;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Jobs;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Authorization;

namespace Esatto.Umbraco.Backoffice.SeoReport;

/// <summary>
/// Feeds the Content → SEO Report dashboard (<c>Client/src</c>), at <c>/umbraco/management/api/v1/esatto/seo-report/…</c>.
///
/// WHO MAY DO WHAT. Everyone with the Content section reads reports, refreshes a month, starts Fetch now, and writes notes and target
/// queries: they are the people who use the report. Only users with the Settings section read or change the settings and the key, test
/// the connection and list the user groups; those endpoints carry the Settings policy on top of the class's (both must pass, which is
/// the intent). Every endpoint's policy is pinned in <c>ControllerRoutingTests</c>.
///
/// OPENING THE DASHBOARD NEVER FETCHES THE ARCHIVE. Reading (<see cref="GetStatus"/>, <see cref="GetReport"/>, <see cref="GetFetchProgress"/>,
/// <see cref="GetAnalysis"/>) asks the archive and the settings and never Google. What fetches a month is a person's click
/// (<see cref="Refresh"/>, <see cref="StartFetch"/>), saving settings that work (<see cref="PutSettings"/>) and the scheduled job.
/// The one thing that is live is the daily curve (<see cref="GetDaily"/>, <see cref="GetDailySources"/>): it asks GA4 itself, a day-fresh
/// call that its service keeps for half an hour, and it never starts the backfill.
///
/// THE SERVER SENDS NO PROSE. A refusal is a <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/> whose meaning is its <c>code</c> and
/// <c>args</c>; the client has the words. Nothing in one comes from an exception's text or the user's input, and the key is not in any
/// response or log line (<c>ApiLeakTests</c>).
///
/// ONE FETCH AT A TIME, and a double click, are the services' business (<see cref="FetchCoordinator"/>, <see cref="ISeoReportService"/>):
/// the controller passes the refusal on as a 409.
///
/// The class is split by concern: this file is the reading and the fetching, <c>.Settings.cs</c> the Settings endpoints,
/// <c>.Problems.cs</c> how a failure becomes an answer, and the analysis and daily endpoints have files of their own.
/// </summary>
[ApiController]
[VersionedApiBackOfficeRoute("esatto/seo-report")]
[ApiExplorerSettings(GroupName = "Esatto SEO Report")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
public sealed partial class SeoReportController : ManagementApiControllerBase
{
    // The name of ISeoReportService.RefreshAsync's parameter, which its range check puts on the exception (the coordinator relies on it too).
    private const string PeriodParameter = "period";

    private readonly ISeoReportService _reports;

    // _daily and _time are read by the daily endpoints (SeoReportController.Daily.cs).
    private readonly IDailyTrafficService _daily;
    private readonly INoteService _notes;
    private readonly ITargetQueryService _targets;
    private readonly SettingsService _settings;
    private readonly IFetchCoordinator _coordinator;
    private readonly ConnectionTester _connectionTester;
    private readonly ISeoReportStore _store;
    private readonly IBackOfficeSecurityAccessor _security;
    private readonly IUserGroupService _userGroups;
    private readonly TimeProvider _time;
    private readonly ILogger<SeoReportController> _logger;

    public SeoReportController(
        ISeoReportService reports,
        IDailyTrafficService daily,
        INoteService notes,
        ITargetQueryService targets,
        SettingsService settings,
        IFetchCoordinator coordinator,
        ConnectionTester connectionTester,
        ISeoReportStore store,
        IBackOfficeSecurityAccessor security,
        IUserGroupService userGroups,
        TimeProvider time,
        ILogger<SeoReportController> logger)
    {
        _reports = reports;
        _daily = daily;
        _notes = notes;
        _targets = targets;
        _settings = settings;
        _coordinator = coordinator;
        _connectionTester = connectionTester;
        _store = store;
        _security = security;
        _userGroups = userGroups;
        _time = time;
        _logger = logger;
    }

    // ---- Status ---------------------------------------------------------------------------------------------------------

    /// <summary>
    /// What the dashboard needs to open: whether the package is set up and what is missing, how the backfill is going, whether the
    /// archive is current and which months there are. Never fetches.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(StatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var effective = await _settings.GetAsync();

        return Ok(new StatusResponse(
            await _settings.GetSetupStateAsync(),
            [.. await _settings.GetMissingAsync()],
            await FetchStatusAsync(),
            await _reports.IsUpToDateAsync(ct),
            [.. await _reports.GetAvailablePeriodsAsync(ct)],
            _reports.ExpectedLatestPeriod.ToString(),
            effective.Settings.DisplayName));
    }

    // ---- Reports --------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The archived report for a month with its note and when it was fetched. No month is the newest. A month that is not archived is an
    /// empty answer (the report is null), not an error, so the dashboard can say that nothing has been fetched. Never fetches.
    /// </summary>
    /// <param name="period"><c>YYYY-MM</c>, exactly, or none for the newest.</param>
    [HttpGet("report")]
    [ProducesResponseType(typeof(ReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetReport([FromQuery] string? period, CancellationToken ct)
    {
        // Before anything is read: the value comes from a query string and never becomes a key or a query unchecked.
        if (!string.IsNullOrWhiteSpace(period) && !Period.TryParse(period, out _)) return PeriodInvalid();

        return Ok(await ReportResponseAsync(await _reports.GetReportAsync(period, ct), period, ct));
    }

    /// <summary>
    /// Fetches a month from Google and archives it (about fifteen seconds), and answers like <see cref="GetReport"/>. A person's
    /// click and nothing else: the month is always asked for, never picked here.
    /// </summary>
    /// <param name="period"><c>YYYY-MM</c>, exactly.</param>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Refresh([FromQuery] string? period, CancellationToken ct)
    {
        if (!Period.TryParse(period, out var month)) return PeriodInvalid();

        try
        {
            var report = await _reports.RefreshAsync(month, ct);
            return Ok(await ReportResponseAsync(report, month.ToString(), ct));
        }
        catch (ArgumentException ex) when (ex.ParamName == PeriodParameter)
        {
            // The service's range check: a month later than the latest finished one, or earlier than Search Console keeps. Anything
            // else that is an ArgumentException is not about the month and falls to the general case below.
            return ProblemResult(StatusCodes.Status400BadRequest, ApiCodes.PeriodOutOfRange);
        }
        catch (Exception ex) when (!CallerLeft(ex, ct))
        {
            return Failure(ex);
        }
    }

    // ---- Fetch now ------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Starts a backfill of the missing months in the background and returns at once (202) with the progress; the dashboard polls
    /// <see cref="GetFetchProgress"/>. 409 when one is running already or the package is not set up. The coordinator is told the kind
    /// (<c>manual</c>) and never the user's name.
    /// </summary>
    [HttpPost("fetch")]
    [ProducesResponseType(typeof(StartFetchResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartFetch()
    {
        switch (await _coordinator.TryStartAsync(FetchState.Manual))
        {
            case FetchStartResult.Started:
                return Accepted(new StartFetchResponse("started", await FetchStatusAsync()));

            case FetchStartResult.AlreadyRunning:
                return ProblemResult(StatusCodes.Status409Conflict, ApiCodes.AlreadyRunning);

            default:
                // Not ready: the key that cannot be read is told apart from the rest, as the setup state does.
                var unreadable = await _settings.GetSetupStateAsync() == SetupState.KeyUnreadable;
                return ProblemResult(StatusCodes.Status409Conflict, unreadable ? ErrorCodes.KeyUnreadable : ErrorCodes.NotConfigured);
        }
    }

    /// <summary>How the backfill is going: the same object <see cref="GetStatus"/> carries as <c>fetch</c>.</summary>
    [HttpGet("fetch/progress")]
    [ProducesResponseType(typeof(FetchStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFetchProgress() => Ok(await FetchStatusAsync());

    // ---- Notes and targets ----------------------------------------------------------------------------------------------

    /// <summary>Saves a month's note (trimmed, cut to the longest allowed; blank deletes it) and answers with what was kept.</summary>
    /// <param name="period"><c>YYYY-MM</c>, exactly.</param>
    /// <param name="body">The text.</param>
    [HttpPut("note")]
    [ProducesResponseType(typeof(NoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PutNote([FromQuery] string? period, [FromBody] NoteRequest? body)
    {
        if (!Period.TryParse(period, out var month)) return PeriodInvalid();
        if (body is null) return RequestInvalid();

        return Ok(new NoteResponse(await _notes.SaveAsync(month, body.Text)));
    }

    /// <summary>The editor's target queries.</summary>
    [HttpGet("targets")]
    [ProducesResponseType(typeof(TargetsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTargets() => Ok(new TargetsResponse([.. await _targets.GetAsync()]));

    /// <summary>Saves the target queries and answers with the list as it was kept: normalized, without blanks and duplicates.</summary>
    [HttpPut("targets")]
    [ProducesResponseType(typeof(TargetsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PutTargets([FromBody] TargetsRequest? body)
    {
        if (body is null) return RequestInvalid();

        return Ok(new TargetsResponse([.. await _targets.SaveAsync(body.Terms)]));
    }

    // ---- Shared ---------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The backfill as the dashboard sees it: this process's latest run (not running, nothing done when there has been none) with the
    /// code and times of the last failure and success from the <c>fetchState</c> document, which a restart does not lose.
    /// </summary>
    private async Task<FetchStatusResponse> FetchStatusAsync()
        => FetchStatusResponse.From(_coordinator.Progress, FetchState.Parse(await _store.GetDocumentAsync(DocumentKeys.FetchState)));

    /// <summary>
    /// The report, its note and when it was fetched, for the report that was found (or fetched) and the month that was asked for. The
    /// month is the report's own when there is one (a blank request means the newest, and the answer says which month that was).
    /// </summary>
    private async Task<ReportResponse> ReportResponseAsync(Report? report, string? requested, CancellationToken ct)
    {
        Period? month = report is not null && Period.TryParse(report.ReportPeriod, out var archived) ? archived
            : Period.TryParse(requested, out var asked) ? asked
            : null;

        if (month is not { } found) return new ReportResponse(await VisibleAsync(report), null, null, null);

        var text = found.ToString();
        var note = await _notes.GetAsync(found);
        var fetched = report is null ? null : await _reports.GetFetchedUtcAsync(text, ct);
        return new ReportResponse(await VisibleAsync(report), note, fetched, text);
    }

    /// <summary>
    /// The report as THIS user may see it: without its findings (the warnings of <c>traffic.search.annotations</c>, which is what makes
    /// the "To fix" tab exist) unless the user is in one of the groups the settings name, or the list is empty. Filtered on the way out,
    /// never on the way in: the archive keeps the whole report, and the cached object is shared, so <see cref="FindingsFilter.StripFindings"/>
    /// returns a copy and nothing here changes the original. Every endpoint that sends findings, or anything derived from them, goes
    /// through this and <see cref="CanSeeFindingsAsync"/>.
    /// </summary>
    private async Task<Report?> VisibleAsync(Report? report)
        => await CanSeeFindingsAsync() ? report : FindingsFilter.StripFindings(report);

    /// <summary>
    /// Whether the signed-in user's groups (by alias) pass the settings' findings groups: any user passes an empty list ("everyone with
    /// access"), otherwise one of the user's groups must be in it. NOBODY SIGNED IN is never shown the findings, whatever the list says: an
    /// empty list means everyone who has access, and a request whose user cannot be found is not one of them. The backoffice policies
    /// make that case unreachable in practice, so this only decides which way a surprise fails.
    /// </summary>
    private async Task<bool> CanSeeFindingsAsync()
    {
        if (_security.BackOfficeSecurity?.CurrentUser is not { } user) return false;

        return FindingsFilter.IsAllowed(user.Groups.Select(group => group.Alias), (await _settings.GetAsync()).Settings.FindingsUserGroups);
    }
}
