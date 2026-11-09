using System.Text;
using Esatto.Umbraco.Backoffice.Seo.Checks;
using Esatto.Umbraco.Backoffice.Seo.Knowledge;
using Esatto.Umbraco.Backoffice.Seo.Pages;
using Esatto.Umbraco.Backoffice.Seo.Persistence;
using Esatto.Umbraco.Backoffice.Seo.Reporting;
using Esatto.Umbraco.Backoffice.Seo.Scanning;
using Esatto.Umbraco.Backoffice.Seo.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.Common.Authorization;

namespace Esatto.Umbraco.Backoffice.Seo;

/// <summary>
/// Feeds the Content → SEO Health dashboard (<c>Client/src</c>), at
/// <c>/umbraco/management/api/v1/esatto/seo-health/…</c>.
/// </summary>
/// <remarks>
/// Everyone with the Content section can read results, start scans, re-check pages and ignore findings
/// (editors fix half the issues). Only users with the Settings section can change settings.
/// </remarks>
[ApiController]
[VersionedApiBackOfficeRoute("esatto/seo-health")]
[ApiExplorerSettings(GroupName = "Esatto SEO Health")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
public sealed class SeoHealthController(
    KnowledgeBase knowledge,
    SettingsService settings,
    SeoEnvironment environment,
    IPageSource pages,
    ScanCoordinator scans,
    ReportService reports,
    IgnoreService ignores,
    RecheckService recheck,
    IBackOfficeSecurityAccessor security) : ManagementApiControllerBase
{
    [HttpGet("knowledge")]
    [ProducesResponseType(typeof(KnowledgeResponse), StatusCodes.Status200OK)]
    public IActionResult GetKnowledge() => Ok(new KnowledgeResponse(knowledge.All, CheckCatalog.All.Select(c => c.Info).ToList()));

    [HttpGet("sites")]
    [ProducesResponseType(typeof(IReadOnlyList<SiteInfo>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSites() => Ok(await pages.GetSitesAsync());

    [HttpGet("settings")]
    [ProducesResponseType(typeof(SettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings() => Ok(ToResponse(await settings.GetAsync()));

    [HttpPut("settings")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    [ProducesResponseType(typeof(SettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> PutSettings([FromBody] SeoSettings body) => Ok(ToResponse(await settings.SaveAsync(body)));

    [HttpGet("overview")]
    [ProducesResponseType(typeof(Overview), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverview([FromQuery] Guid site) => Ok(await reports.GetOverviewAsync(site));

    [HttpGet("issues")]
    [ProducesResponseType(typeof(IReadOnlyList<IssueGroup>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIssues(
        [FromQuery] Guid site, [FromQuery] Severity? severity, [FromQuery] Owner? owner, [FromQuery] string? culture,
        [FromQuery] string? checkId, [FromQuery] bool ignored = false, [FromQuery] string? search = null)
        => Ok(await reports.GetIssuesAsync(site, new IssueFilter(severity, owner, culture, checkId, ignored, search)));

    [HttpGet("issues/csv")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIssuesCsv(
        [FromQuery] Guid site, [FromQuery] Severity? severity, [FromQuery] Owner? owner, [FromQuery] string? culture,
        [FromQuery] string? checkId, [FromQuery] bool ignored = false, [FromQuery] string? search = null)
    {
        var groups = await reports.GetIssuesAsync(site, new IssueFilter(severity, owner, culture, checkId, ignored, search));
        return File(Encoding.UTF8.GetBytes(CsvWriter.Write(groups, knowledge)), "text/csv; charset=utf-8", $"seo-issues-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    [HttpPost("scans")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartScan([FromQuery] Guid site)
    {
        if (!await IsSiteAsync(site)) return UnknownSite();
        // Pages without a bound domain are fetched from the host the backoffice is on.
        var requestBase = new Uri($"{Request.Scheme}://{Request.Host}/");
        return scans.TryStart(site, ScanTrigger.Manual, UserName, requestBase)
            ? Ok(new { started = true })
            : Conflict("A scan of this site is already running.");
    }

    [HttpGet("scans/progress")]
    [ProducesResponseType(typeof(ScanProgress), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult GetProgress([FromQuery] Guid site)
        => scans.GetProgress(site) is { } progress ? Ok(progress) : scans.IsRunning(site) ? Ok(new ScanProgress(0, 0, 0, null)) : NoContent();

    [HttpDelete("scans/current")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult CancelScan([FromQuery] Guid site) => scans.Cancel(site) ? NoContent() : NotFound();

    [HttpPost("recheck")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Recheck([FromQuery] Guid site, [FromBody] RecheckRequest body, CancellationToken ct)
    {
        try
        {
            await recheck.RecheckAsync(site, body.ContentKey, body.Culture ?? string.Empty, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("ignores")]
    [ProducesResponseType(typeof(IgnoreResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddIgnore([FromQuery] Guid site, [FromBody] IgnoreRequest body)
    {
        if (!await IsSiteAsync(site)) return UnknownSite();
        try
        {
            return Ok(new IgnoreResponse(await ignores.AddAsync(site, body.CheckId, body.ContentKey, body.Culture, body.Note, UserName)));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("ignores/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveIgnore(int id)
    {
        await ignores.RemoveAsync(id);
        return NoContent();
    }

    private string UserName => security.BackOfficeSecurity?.CurrentUser?.Name ?? "Unknown";

    /// <summary>Only a published root is a site; anything else would write a scan or ignore no one can see.</summary>
    private async Task<bool> IsSiteAsync(Guid site) => (await pages.GetSitesAsync()).Any(s => s.Key == site);

    private NotFoundObjectResult UnknownSite() => NotFound("There is no site with that key.");

    private SettingsResponse ToResponse(EffectiveSettings effective)
    {
        var user = security.BackOfficeSecurity?.CurrentUser;
        var canEdit = user is not null && security.BackOfficeSecurity!.UserHasSectionAccess(Constants.Applications.Settings, user);
        return new SettingsResponse(effective.Settings, effective.Locked, canEdit, environment.IntentionallyHidden);
    }
}

public sealed record KnowledgeResponse(IReadOnlyDictionary<string, CheckText> Texts, IReadOnlyList<CheckInfo> Checks);

public sealed record SettingsResponse(SeoSettings Settings, IReadOnlyList<string> Locked, bool CanEdit, bool IntentionallyHidden);

public sealed record RecheckRequest(Guid ContentKey, string? Culture);

public sealed record IgnoreRequest(string CheckId, Guid? ContentKey, string? Culture, string Note);

public sealed record IgnoreResponse(int Id);
