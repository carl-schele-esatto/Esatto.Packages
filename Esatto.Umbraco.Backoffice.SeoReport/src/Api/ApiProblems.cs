using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Esatto.Umbraco.Backoffice.SeoReport.Api;

/// <summary>
/// The problems the API answers with. A <see cref="ProblemDetails"/> whose meaning is its <c>code</c> (one of <see cref="ErrorCodes"/>
/// or <see cref="ApiCodes"/>) and, when the code's text has blanks to fill, <c>args</c>; plus a <c>traceId</c>, the framework's own
/// id of the request, so that what a person sees can be matched to a line in the log.
///
/// THE SERVER SENDS NO PROSE. The title is a short fixed English phrase for whoever reads a raw response (a developer in the network
/// tab) and is never shown by the client; nothing in a problem comes from the user's input or from an exception's text.
/// </summary>
internal static class ApiProblems
{
    /// <summary>The status a code is answered with: a configuration state conflicts with the request (409), a failure at Google is a bad gateway (502).</summary>
    public static int StatusOf(string code) => code switch
    {
        ErrorCodes.NotConfigured or ErrorCodes.KeyUnreadable or ErrorCodes.KeyInvalid => StatusCodes.Status409Conflict,
        ErrorCodes.Ga4PropertyInvalid or ErrorCodes.SearchConsoleInvalid => StatusCodes.Status400BadRequest,
        _ => StatusCodes.Status502BadGateway,
    };

    /// <summary>
    /// The start of every problem's <c>type</c>: a stable URI per code (<c>urn:esatto:seo-report:problem:quota</c>), which names what the
    /// problem is without being an address to open.
    /// </summary>
    public const string TypePrefix = "urn:esatto:seo-report:problem:";

    /// <summary>
    /// The problem for a code, with <c>type</c>, <c>title</c> and <c>status</c> ALWAYS set. That is not decoration: the backoffice's HTTP
    /// interceptor keeps the body of an error answer only when it has all three (Umbraco's <c>isProblemDetailsLike</c>), and otherwise swaps in a
    /// generic problem of its own, in which the <c>code</c>, the <c>args</c> and the <c>traceId</c> are gone and every refusal would read as
    /// "unknown". ASP.NET leaves <c>Type</c> null and does not write a null, so it has to be set here and not left to the framework's defaults.
    /// </summary>
    public static ProblemDetails Create(HttpContext http, int status, string code, IReadOnlyDictionary<string, object?>? args = null)
    {
        var problem = new ProblemDetails { Type = TypePrefix + code, Status = status, Title = TitleOf(status) };
        problem.Extensions["code"] = code;

        if (PrimitivesOf(args) is { Count: > 0 } primitives) problem.Extensions["args"] = primitives;

        problem.Extensions["traceId"] = http.TraceIdentifier;
        return problem;
    }

    // Arguments are fixed words and numbers. Anything else (a nested object, a list) would be a way for text to get through.
    private static Dictionary<string, object?>? PrimitivesOf(IReadOnlyDictionary<string, object?>? args)
    {
        if (args is null) return null;

        var kept = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (name, value) in args)
        {
            if (value is null or string or bool or int or long or double or decimal) kept[name] = value;
        }

        return kept;
    }

    private static string TitleOf(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "The request is not valid.",
        StatusCodes.Status409Conflict => "The request conflicts with the current state.",
        StatusCodes.Status502BadGateway => "A fetch from Google failed.",
        _ => "The request failed.",
    };
}
