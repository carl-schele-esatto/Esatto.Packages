using System.Text;
using Esatto.Umbraco.Backoffice.Accessibility.Knowledge;
using Esatto.Umbraco.Backoffice.Accessibility.Manual;
using Esatto.Umbraco.Backoffice.Accessibility.Pages;
using Esatto.Umbraco.Backoffice.Accessibility.Reporting;
using Esatto.Umbraco.Backoffice.Accessibility.Scanning;
using Esatto.Umbraco.Backoffice.Accessibility.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.Common.Authorization;

namespace Esatto.Umbraco.Backoffice.Accessibility;

/// <summary>
/// Feeds the Content → Accessibility dashboard (<c>Client/src</c>), at
/// <c>/umbraco/management/api/v1/esatto/accessibility/…</c>.
/// </summary>
/// <remarks>
/// Everyone with access to the Content section can read the report, run scans and record manual results
/// (editors are half the audience). Only users with the Settings section can change settings.
/// </remarks>
[ApiController]
[VersionedApiBackOfficeRoute("esatto/accessibility")]
[ApiExplorerSettings(GroupName = "Esatto Accessibility")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
public sealed class AccessibilityController(
    KnowledgeBase knowledge,
    SettingsService settings,
    SitePageSource pageSource,
    ScanService scans,
    ReportService reports,
    ManualService manual,
    FalsePositiveService falsePositives,
    IBackOfficeSecurityAccessor security) : ManagementApiControllerBase
{
    [HttpGet("knowledge")]
    [ProducesResponseType(typeof(KnowledgeResponse), StatusCodes.Status200OK)]
    public IActionResult GetKnowledge() => Ok(new KnowledgeResponse(knowledge.All, knowledge.Rules.ToList()));

    [HttpGet("sites")]
    [ProducesResponseType(typeof(IReadOnlyList<SiteInfo>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSites() => Ok(await pageSource.GetSitesAsync());

    [HttpGet("pages")]
    [ProducesResponseType(typeof(IReadOnlyList<ScanPage>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPages([FromQuery] Guid site)
    {
        var exclude = (await settings.GetAsync()).Settings.Scan.ExcludePaths;
        return Ok(PageListBuilder.Build(await pageSource.GetCandidatesAsync(site), exclude));
    }

    [HttpGet("settings")]
    [ProducesResponseType(typeof(SettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings() => Ok(ToResponse(await settings.GetAsync()));

    [HttpPut("settings")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    [ProducesResponseType(typeof(SettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> PutSettings([FromBody] AccessibilitySettings body) => Ok(ToResponse(await settings.SaveAsync(body)));

    [HttpPost("scans")]
    [ProducesResponseType(typeof(StartScanResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> StartScan([FromBody] StartScanRequest body)
        => Ok(new StartScanResponse(await scans.StartAsync(body.SiteKey, body.Mobile, body.PagesTotal, UserName)));

    [HttpGet("scans/{id:int}/done")]
    [ProducesResponseType(typeof(IReadOnlyList<DonePage>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDone(int id) => Ok(await scans.DoneAsync(id));

    [HttpPost("scans/{id:int}/pages")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddPage(int id, [FromBody] PageResult body)
    {
        try
        {
            await scans.AddPageAsync(id, body);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("scans/{id:int}/finish")]
    [ProducesResponseType(typeof(ScanSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FinishScan(int id, [FromBody] FinishScanRequest body)
    {
        try
        {
            return Ok(await scans.FinishAsync(id, body.Complete));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("recheck")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Recheck([FromQuery] Guid site, [FromBody] PageResult body)
    {
        try
        {
            await scans.RecheckAsync(site, body);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpGet("overview")]
    [ProducesResponseType(typeof(Overview), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverview([FromQuery] Guid site) => Ok(await reports.GetOverviewAsync(site));

    [HttpGet("issues")]
    [ProducesResponseType(typeof(IReadOnlyList<IssueGroup>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIssues(
        [FromQuery] Guid site, [FromQuery] int? principle, [FromQuery] WcagLevel? level, [FromQuery] Owner? owner,
        [FromQuery] string? impact, [FromQuery] bool falsePositives = false, [FromQuery] string? criterion = null,
        [FromQuery] string? ruleId = null, [FromQuery] string? kind = null)
        => Ok(await reports.GetIssuesAsync(site, new IssueFilter(principle, level, owner, impact, falsePositives, criterion, ruleId, kind)));

    [HttpGet("issues/csv")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIssuesCsv(
        [FromQuery] Guid site, [FromQuery] int? principle, [FromQuery] WcagLevel? level, [FromQuery] Owner? owner,
        [FromQuery] string? impact, [FromQuery] bool falsePositives = false, [FromQuery] string? criterion = null)
    {
        var groups = await reports.GetIssuesAsync(site, new IssueFilter(principle, level, owner, impact, falsePositives, criterion), withNodes: true);
        var name = $"accessibility-issues-{DateTime.UtcNow:yyyy-MM-dd}.csv";
        return File(Encoding.UTF8.GetBytes(CsvWriter.Write(groups)), "text/csv; charset=utf-8", name);
    }

    [HttpGet("checklist")]
    [ProducesResponseType(typeof(IReadOnlyList<ChecklistItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChecklist([FromQuery] Guid site) => Ok(await manual.GetChecklistAsync(site));

    [HttpPut("checklist/{criterionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetChecklist([FromQuery] Guid site, string criterionId, [FromBody] ManualRequest body)
    {
        try
        {
            await manual.SetAsync(site, criterionId, body.Status, body.Note, UserName);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("checklist/{criterionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ClearChecklist([FromQuery] Guid site, string criterionId)
    {
        await manual.ClearAsync(site, criterionId);
        return NoContent();
    }

    [HttpPost("false-positives")]
    [ProducesResponseType(typeof(FalsePositiveResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddFalsePositive([FromQuery] Guid site, [FromBody] FalsePositiveRequest body)
    {
        try
        {
            return Ok(new FalsePositiveResponse(await falsePositives.AddAsync(site, body.RuleId, body.ContentKey, body.Selector, body.Note, UserName)));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("false-positives/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveFalsePositive(int id)
    {
        await falsePositives.RemoveAsync(id);
        return NoContent();
    }

    private string UserName => security.BackOfficeSecurity?.CurrentUser?.Name ?? "Unknown";

    private SettingsResponse ToResponse(EffectiveSettings effective)
    {
        var user = security.BackOfficeSecurity?.CurrentUser;
        var canEdit = user is not null && security.BackOfficeSecurity!.UserHasSectionAccess(Constants.Applications.Settings, user);
        return new SettingsResponse(effective.Settings, effective.Locked, effective.Target, canEdit);
    }
}

public sealed record KnowledgeResponse(IReadOnlyList<Criterion> Criteria, IReadOnlyList<AxeRuleGuide> Rules);

public sealed record SettingsResponse(AccessibilitySettings Settings, IReadOnlyList<string> Locked, WcagTarget Target, bool CanEdit);

public sealed record StartScanRequest(Guid SiteKey, bool Mobile, int PagesTotal);

public sealed record StartScanResponse(int ScanId);

public sealed record FinishScanRequest(bool Complete);

public sealed record ManualRequest(string Status, string? Note);

public sealed record FalsePositiveRequest(string RuleId, Guid ContentKey, string? Selector, string Note);

public sealed record FalsePositiveResponse(int Id);
