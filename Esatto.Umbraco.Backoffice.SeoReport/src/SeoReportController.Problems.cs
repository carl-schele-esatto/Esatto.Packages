using Esatto.Umbraco.Backoffice.SeoReport.Api;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Esatto.Umbraco.Backoffice.SeoReport;

// How a refusal or a failure becomes an answer. One place, so every endpoint answers the same way and none of them can put an
// exception's text into a response.
public sealed partial class SeoReportController
{
    /// <summary>
    /// A problem as the client reads it (see <see cref="ApiProblems"/>): the status, the <c>code</c> (and <c>args</c>) and the request's trace id.
    /// </summary>
    private ObjectResult ProblemResult(int status, string code, IReadOnlyDictionary<string, object?>? args = null)
        => StatusCode(status, ApiProblems.Create(HttpContext, status, code, args));

    /// <summary>A period that is not exactly <c>YYYY-MM</c>.</summary>
    private ObjectResult PeriodInvalid() => ProblemResult(StatusCodes.Status400BadRequest, ApiCodes.PeriodInvalid);

    /// <summary>A body that is missing, or lacks the part the endpoint needs.</summary>
    private ObjectResult RequestInvalid() => ProblemResult(StatusCodes.Status400BadRequest, ApiCodes.RequestInvalid);

    /// <summary>
    /// A failure of a fetch (the key, Google, or anything unexpected in between) as the problem its code stands for: 409 for the states
    /// the person can fix in the settings (<c>notConfigured</c>, <c>keyUnreadable</c>, <c>keyInvalid</c>), 502 for what failed at Google or
    /// that no code names (<c>apiDisabled</c>, <c>noAccess</c>, <c>propertyNotFound</c>, <c>quota</c>, <c>unknown</c>). The exception is
    /// only read by <see cref="GoogleErrorMapper"/>, which compares it with fixed words; its message is never copied anywhere.
    ///
    /// A failure no code names is logged at Warning with what is safe to say: the exception TYPE, the HTTP status when it has one, and the
    /// trace id that the person sees in the problem, so an admin can find the line. This line never carries the exception itself, the key
    /// or the settings. (The backfill, the scheduled job and Test connection do attach the exception to their own <c>unknown</c> Warning,
    /// type and status included, because Google's answer in it is what explains an <c>unknown</c>. It can name the property or the site,
    /// never the key: wherever the key text is read or loaded, a failure is turned into a code there and its exception is dropped.)
    /// </summary>
    private ObjectResult Failure(Exception exception)
    {
        var problem = GoogleErrorMapper.Map(exception);

        if (problem.Code == ErrorCodes.Unknown)
        {
            _logger.LogWarning(
                "A request failed with {ExceptionType} (HTTP status {HttpStatus}) and no code names it. Trace id {TraceId}.",
                exception.GetType().Name,
                GoogleErrorMapper.HttpStatusOf(exception),
                HttpContext.TraceIdentifier);
        }

        return ProblemResult(ApiProblems.StatusOf(problem.Code), problem.Code, problem.Args);
    }

    /// <summary>
    /// Whether <paramref name="exception"/> is only the caller going away (it cancelled the request): nothing failed and nobody is left to
    /// tell, so it is not turned into a problem. A timeout that did not come from the caller is not this.
    /// </summary>
    private static bool CallerLeft(Exception exception, CancellationToken ct) => exception is OperationCanceledException && ct.IsCancellationRequested;

    /// <summary>
    /// Settings answers must never be kept by a browser or a proxy, on a refusal as much as on success: the framework's own no-cache
    /// filter only acts on a 200.
    /// </summary>
    private void NoStore() => Response.Headers.CacheControl = "no-store";
}
