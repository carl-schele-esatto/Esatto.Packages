using Esatto.Umbraco.Backoffice.SeoReport.Api;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Jobs;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Web.Common.Authorization;

namespace Esatto.Umbraco.Backoffice.SeoReport;

// The Settings endpoints. Each carries the Settings policy on top of the class's Content policy (both must pass), and every one of
// them answers with Cache-Control: no-store, a refusal included.
public sealed partial class SeoReportController
{
    // A site has a handful of user groups. The service pages its answer, and the findings picker has no use for a second page.
    private const int MaxUserGroups = 500;

    /// <summary>The settings in force, the fields configuration locks, and what is known about the key (never the key).</summary>
    [HttpGet("settings")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    [ProducesResponseType(typeof(SettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings()
    {
        NoStore();
        return Ok(ToResponse(await _settings.GetAsync()));
    }

    /// <summary>
    /// Saves the settings and, when a key is given, the key (encrypted; <c>RemoveKey</c> deletes it). A refusal is a 400 whose <c>code</c>
    /// is <c>ga4PropertyInvalid</c>, <c>searchConsoleInvalid</c> or <c>keyInvalid</c> and nothing is saved. A save that is accepted then
    /// checks the connection with what is now saved, and when the key and both sources work starts the backfill at once: the answer does
    /// not wait for it, and neither a failed check nor a failed start ever fails the save.
    /// </summary>
    /// <remarks>
    /// The request is read with the host's default body size limit (30 MB with Kestrel and IIS alike): a key over 16 KB reaches the
    /// service and is refused there as <c>keyInvalid</c> (400), so it is never an unhandled 413. Only a body past the host's own limit
    /// is refused by the host, before this action.
    /// </remarks>
    [HttpPut("settings")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    [ProducesResponseType(typeof(SettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PutSettings([FromBody] PutSettingsRequest? body, CancellationToken ct)
    {
        NoStore();
        if (body?.Settings is null) return RequestInvalid();

        EffectiveSettings saved;
        try
        {
            saved = await _settings.SaveAsync(body.Settings, body.ServiceAccountJson, body.RemoveKey);
        }
        catch (SettingsValidationException ex)
        {
            // The code is a fixed word; anything else is not passed on.
            return ProblemResult(StatusCodes.Status400BadRequest, ErrorCodes.IsKnown(ex.Code) ? ex.Code : ErrorCodes.Unknown);
        }

        await StartBackfillWhenWorkingAsync(saved, ct);
        return Ok(ToResponse(saved));
    }

    /// <summary>
    /// Test connection: the key and the two properties as they stand in the form (a blank key tests the saved or configured one), each
    /// source on its own with its own code. Never saves anything and never starts a fetch.
    /// </summary>
    [HttpPost("settings/test")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    [ProducesResponseType(typeof(ConnectionTestResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TestConnection([FromBody] ConnectionTestRequest? body, CancellationToken ct)
    {
        NoStore();
        if (body is null) return RequestInvalid();

        return Ok(await _connectionTester.TestAsync(body, ct));
    }

    /// <summary>The backoffice user groups, for the picker of who sees the findings: the alias is what is stored.</summary>
    [HttpGet("user-groups")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
    [ProducesResponseType(typeof(UserGroupItem[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserGroups()
    {
        NoStore();

        var groups = await _userGroups.GetAllAsync(0, MaxUserGroups);
        return Ok(groups.Items
            .Select(group => new UserGroupItem(group.Alias, group.Name ?? group.Alias))
            .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Alias, StringComparer.Ordinal)
            .ToArray());
    }

    private static SettingsResponse ToResponse(EffectiveSettings effective) => new(effective.Settings, effective.Locked, effective.Key);

    /// <summary>
    /// After a save: checks the connection with what is now saved and, only when the key and both sources are fine (a source that is
    /// connected but has no data yet is fine), asks for a backfill as a person does. Whatever the start answers (it may be running
    /// already) is of no concern to the save, and neither is a check or a start that fails: the settings are saved, and the scheduled job
    /// or Fetch now will do what this did not. What is logged is codes and the exception's type, never its text.
    /// </summary>
    private async Task StartBackfillWhenWorkingAsync(EffectiveSettings saved, CancellationToken ct)
    {
        try
        {
            // The identifiers are the saved ones, normalized: a blank identifier would be reported as invalid, and the key is blank,
            // which is "the key in force".
            var check = await _connectionTester.TestAsync(
                new ConnectionTestRequest(saved.Settings.Ga4PropertyId, saved.Settings.SearchConsoleSite, null), ct);

            if (!(check.Key.Ok && check.Ga4.Ok && check.SearchConsole.Ok))
            {
                _logger.LogDebug(
                    "The connection check after saving the settings did not pass (key: {KeyCode}, GA4: {Ga4Code}, Search Console: {SearchConsoleCode}); no backfill was started.",
                    check.Key.ErrorCode,
                    check.Ga4.ErrorCode,
                    check.SearchConsole.ErrorCode);
                return;
            }

            var started = await _coordinator.TryStartAsync(FetchState.Manual);
            _logger.LogInformation("The settings were saved and work; asking for a backfill: {Result}.", started);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The caller left before the check was done: the save stands, and nothing more is asked of Google for it.
        }
        catch (Exception ex)
        {
            _logger.LogInformation("The check after saving the settings could not be completed ({ExceptionType}); no backfill was started.", ex.GetType().Name);
        }
    }
}
