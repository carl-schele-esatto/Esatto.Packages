using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Api.Common.Builders;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.Common.Authorization;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks;

/// <summary>
/// Management API for the Scheduled Tasks dashboard. All endpoints require
/// SectionAccessSettings (i.e. the user can see the Settings section).
/// </summary>
/// <remarks>
/// Task ids contain dots and colons (<c>recurring:Namespace.Type</c>), so they travel in the
/// query string or body rather than the path.
/// </remarks>
[ApiController]
[VersionedApiBackOfficeRoute("backoffice/scheduled-tasks")]
[ApiExplorerSettings(GroupName = "Backoffice Scheduled Tasks")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessSettings)]
public sealed class ScheduledTasksController : ManagementApiControllerBase
{
    private readonly IScheduledTaskService _service;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly IOptionsMonitor<ScheduledTasksOptions> _options;

    public ScheduledTasksController(
        IScheduledTaskService service,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IOptionsMonitor<ScheduledTasksOptions> options)
    {
        _service = service;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _options = options;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ScheduledTasksOverviewDto), StatusCodes.Status200OK)]
    public IActionResult GetAll()
        => Ok(new ScheduledTasksOverviewDto(
            Environment.MachineName,
            _service.CurrentServerRole.ToString(),
            _options.CurrentValue.RunHistoryLimit,
            _service.GetAll().Select(t => t.ToDto()).ToList()));

    [HttpGet("runs")]
    [ProducesResponseType(typeof(IReadOnlyList<ScheduledTaskRunDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetRuns([FromQuery] string id)
        => _service.GetRuns(id) is { } runs
            ? Ok(runs.Select(r => r.ToDto()).ToList())
            : NotFound();

    [HttpDelete("runs")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult ClearRuns([FromQuery] string id)
        => _service.ClearRuns(id).Status == ScheduledTaskCommandStatus.Ok ? NoContent() : NotFound();

    [HttpPost("run")]
    [ProducesResponseType(typeof(ScheduledTaskCommandResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public IActionResult Run([FromBody] ScheduledTaskIdRequest request)
        => ToResponse(request.Id, _service.RunNow(request.Id, CurrentUserName()));

    [HttpPut("schedule")]
    [ProducesResponseType(typeof(ScheduledTaskCommandResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult SetSchedule([FromBody] SetScheduledTaskScheduleRequest request)
    {
        ScheduledTaskCalendar? calendar = null;
        if (request.Schedule is { } schedule)
        {
            calendar = schedule.ToCalendar(out var error);
            if (calendar is null) return ProblemResult(StatusCodes.Status400BadRequest, error);
        }

        return ToResponse(request.Id, _service.SetSchedule(request.Id, calendar, CurrentUserName()));
    }

    [HttpPost("schedule/preview")]
    [ProducesResponseType(typeof(ScheduledTaskSchedulePreviewDto), StatusCodes.Status200OK)]
    public IActionResult PreviewSchedule([FromBody] PreviewScheduledTaskScheduleRequest request)
    {
        if (request.Schedule.ToCalendar(out var error) is not { } calendar)
            return Ok(new ScheduledTaskSchedulePreviewDto([], error));

        var (nextRuns, problem) = _service.PreviewSchedule(calendar, request.Count ?? 5);
        return Ok(new ScheduledTaskSchedulePreviewDto(nextRuns, problem));
    }

    [HttpGet("properties")]
    [ProducesResponseType(typeof(ScheduledTaskPropertiesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetProperties([FromQuery] string id)
        => _service.GetProperties(id) is { } form ? Ok(form.ToDto()) : NotFound();

    [HttpPut("properties")]
    [ProducesResponseType(typeof(ScheduledTaskCommandResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult SetProperties([FromBody] SetScheduledTaskPropertiesRequest request)
    {
        // Model binding doesn't check list entries: [null] or an entry without an alias gets here.
        if (request.Values?.Any(v => v is null || string.IsNullOrEmpty(v.Alias)) == true)
            return ProblemResult(StatusCodes.Status400BadRequest, "Each property value needs an alias: { alias, value }.");

        var values = request.Values?
            .GroupBy(v => v.Alias, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.Ordinal);

        return ToResponse(request.Id, _service.SetProperties(request.Id, values, CurrentUserName()));
    }

    [HttpPost("reset")]
    [ProducesResponseType(typeof(ScheduledTaskCommandResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public IActionResult Reset([FromBody] ResetScheduledTaskRunRequest request)
        => ToResponse(request.Id, _service.ResetRun(request.Id, request.RunningSinceUtc));

    [HttpPost("pause")]
    [ProducesResponseType(typeof(ScheduledTaskCommandResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Pause([FromBody] ScheduledTaskIdRequest request)
        => ToResponse(request.Id, _service.SetPaused(request.Id, true, CurrentUserName()));

    [HttpPost("resume")]
    [ProducesResponseType(typeof(ScheduledTaskCommandResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Resume([FromBody] ScheduledTaskIdRequest request)
        => ToResponse(request.Id, _service.SetPaused(request.Id, false, CurrentUserName()));

    [HttpPut("period")]
    [ProducesResponseType(typeof(ScheduledTaskCommandResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult SetPeriod([FromBody] SetScheduledTaskPeriodRequest request)
    {
        TimeSpan? period = null;
        if (request.PeriodSeconds is { } seconds)
        {
            if (!double.IsFinite(seconds) || seconds <= 0)
                return ProblemResult(StatusCodes.Status400BadRequest, "The interval must be a positive number of seconds.");

            // Anything past TimeSpan's range is certainly past the maximum; let the service say so.
            // Otherwise whole milliseconds: 0.7 days typed in a browser arrives as 60479.99999999999 s,
            // which FromSeconds would truncate to 100 ns short of a week.
            period = seconds >= TimeSpan.MaxValue.TotalSeconds - 1
                ? TimeSpan.MaxValue
                : TimeSpan.FromMilliseconds(Math.Round(seconds * 1000));
        }

        return ToResponse(request.Id, _service.SetPeriod(request.Id, period, CurrentUserName()));
    }

    private IActionResult ToResponse(string id, ScheduledTaskCommandResult result) => result.Status switch
    {
        ScheduledTaskCommandStatus.Ok => Ok(new ScheduledTaskCommandResponse(result.Message, _service.Get(id)?.ToDto())),
        ScheduledTaskCommandStatus.NotFound => NotFound(),
        ScheduledTaskCommandStatus.Invalid when result.Errors is { } errors => PropertyErrorsResult(result.Message, errors),
        ScheduledTaskCommandStatus.Invalid => ProblemResult(StatusCodes.Status400BadRequest, result.Message),
        _ => ProblemResult(StatusCodes.Status409Conflict, result.Message),
    };

    // The backoffice only reads a body as ProblemDetails (and shows its title) when it has type,
    // title and status; "Error" is the type Umbraco's own ProblemDetailsBuilder uses.
    private ObjectResult ProblemResult(int status, string? title)
        => StatusCode(status, new ProblemDetails { Type = "Error", Title = title, Status = status });

    // Problems by property alias as { errors: { alias: [messages] } }, the way Umbraco's own
    // controllers report request model errors. (A ValidationProblemDetails would go through the
    // backoffice's ValidationProblemDetailsConverter, which writes errors as [{ "$.alias": [...] }].)
    private ObjectResult PropertyErrorsResult(string? title, IReadOnlyDictionary<string, string[]> errors)
    {
        var problem = new ProblemDetailsBuilder()
            .WithTitle(title ?? "Some properties need attention.")
            .WithRequestModelErrors(errors.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal))
            .Build();
        problem.Status = StatusCodes.Status400BadRequest;
        return StatusCode(StatusCodes.Status400BadRequest, problem);
    }

    private string? CurrentUserName() => _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Name;
}
