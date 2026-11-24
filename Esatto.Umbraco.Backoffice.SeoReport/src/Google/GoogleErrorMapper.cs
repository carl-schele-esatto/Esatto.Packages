using System.Text.Json;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Google;
using Google.Apis.Auth.OAuth2.Responses;

namespace Esatto.Umbraco.Backoffice.SeoReport.Google;

/// <summary>
/// A problem as the client shows it: a code from <see cref="ErrorCodes"/> and a few fixed arguments for its text. The arguments
/// are primitives (strings), so the problem serializes as <c>{"code":"noAccess","args":{"source":"ga4"}}</c>, and they are only ever
/// fixed words (see <see cref="GoogleErrorMapper"/>), never any text taken from an exception, a response or the user.
/// </summary>
/// <param name="Code">One of <see cref="ErrorCodes.All"/>.</param>
/// <param name="Args">The arguments of the code's text, or null when it has none.</param>
public sealed record ProblemInfo(string Code, IReadOnlyDictionary<string, object?>? Args = null);

/// <summary>
/// Turns whatever went wrong with a Google call into a <see cref="ProblemInfo"/>: one of the fixed <see cref="ErrorCodes"/> and,
/// at most, two fixed arguments. NOTHING of the exception reaches the result (not its message, not the messages of the exceptions
/// inside it, not Google's response text), because those can quote the project, the request or the user's input, and the result is
/// shown on screen and sent as JSON. The exception is only ever read for facts that are compared with fixed words.
///
/// THE ARGUMENTS, and where they come from:
///   <c>api</c>    which API is switched off, <c>analyticsData</c> or <c>searchConsole</c> (code <c>apiDisabled</c>);
///   <c>source</c> which source the problem is about, <c>ga4</c> or <c>searchConsole</c> (codes <c>noAccess</c>,
///                 <c>propertyNotFound</c>, <c>noData</c>).
/// Both come from a whitelist: the service name Google's client puts on its exception (<c>analyticsdata</c>, <c>searchconsole</c>) is
/// looked up, never copied, and so is the <c>source</c> a caller passes. An unknown name gives no argument at all.
///
/// THE E-MAIL IS NOT HERE. The texts for <c>noAccess</c> and <c>propertyNotFound</c> also name the service account's e-mail address,
/// the one to add in Google Analytics or Search Console. The mapper does not know it, so it cannot send it: the CLIENT adds an
/// <c>email</c> argument to those problems from the key status it already has (<c>KeyStatus.ServiceAccountEmail</c>, or the
/// <c>serviceAccountEmail</c> of a Test connection result). Whoever shows these problems (the fetch progress, the Settings tab) must
/// do that. A text without it still reads well.
///
/// CANCELLATION. A caller that cancels must not hand the cancellation to the mapper: it is not a problem to report. If one arrives
/// anyway (an <see cref="OperationCanceledException"/>, or a <see cref="TaskCanceledException"/> from an HTTP timeout) it is
/// <c>unknown</c>, as is every exception this class has no rule for.
/// </summary>
public static class GoogleErrorMapper
{
    /// <summary>The name of the argument that says which API is switched off.</summary>
    public const string ApiArg = "api";

    /// <summary>The name of the argument that says which source a problem is about.</summary>
    public const string SourceArg = "source";

    /// <summary>The source GA4, as the argument <c>source</c> and as the value a caller passes.</summary>
    public const string Ga4 = "ga4";

    /// <summary>The source Search Console, as the argument <c>source</c> and as the value a caller passes.</summary>
    public const string SearchConsole = "searchConsole";

    /// <summary>The Google Analytics Data API, as the argument <c>api</c>.</summary>
    public const string AnalyticsDataApi = "analyticsData";

    /// <summary>The Search Console API, as the argument <c>api</c>.</summary>
    public const string SearchConsoleApi = "searchConsole";

    // What Google's client names the service on an exception (BaseClientService.Name).
    private const string AnalyticsDataServiceName = "analyticsdata";
    private const string SearchConsoleServiceName = "searchconsole";

    // Reasons that mean the API is not switched on. The old form is in the error list, the new one in the details of the body.
    private static readonly string[] ServiceDisabledReasons = ["SERVICE_DISABLED", "accessNotConfigured"];

    // Reasons Google gives with a 403 when a limit is reached (it uses a 403 for these in the older APIs).
    private static readonly string[] QuotaReasons = ["rateLimitExceeded", "userRateLimitExceeded", "quotaExceeded", "dailyLimitExceeded"];

    private const string ResourceExhausted = "RESOURCE_EXHAUSTED";

    // What Google's token endpoint answers when it will not accept the key itself: the key was deleted or rotated, the service account
    // was deleted or disabled (invalid_grant), or the client is not known or not allowed (invalid_client, unauthorized_client). Any
    // other token failure (a server error, temporarily_unavailable) says nothing about the key. invalid_grant is also what a server
    // clock that is far off gives; that is rare on a hosted server and the wording of keyInvalid does not rule it out.
    private static readonly string[] RefusedKeyErrors = ["invalid_grant", "invalid_client", "unauthorized_client"];

    // A response body is read only to find a status and a few reasons; a longer one is not Google's error and is left alone.
    private const int MaxBodyChars = 64 * 1024;

    /// <summary>
    /// The problem <paramref name="ex"/> stands for.
    /// <list type="bullet">
    /// <item>A <see cref="ReportFetchException"/> keeps its code when it is a known one, otherwise <c>unknown</c>.</item>
    /// <item>A <see cref="GoogleApiException"/>: 403 with a disabled-API reason is <c>apiDisabled</c> (+ <c>api</c>); 429, a 403 with a
    /// limit reason, or status <c>RESOURCE_EXHAUSTED</c> is <c>quota</c>; 404 is <c>propertyNotFound</c> (+ <c>source</c>); any other 401
    /// or 403 is <c>noAccess</c> (+ <c>source</c>); the rest is <c>unknown</c>.</item>
    /// <item>A <see cref="TokenResponseException"/> whose error is <c>invalid_grant</c>, <c>invalid_client</c> or <c>unauthorized_client</c>
    /// (Google refuses the key: it was deleted or rotated, or its service account was deleted or disabled) is <c>keyInvalid</c>; any
    /// other token failure is <c>unknown</c>. Only the error word is compared; its description is never read.</item>
    /// <item>Everything else is <c>unknown</c>.</item>
    /// </list>
    /// </summary>
    /// <param name="ex">The failure. Never null.</param>
    /// <param name="source">
    /// The source the caller was talking to when it failed, <see cref="Ga4"/> or <see cref="SearchConsole"/>; anything else is ignored.
    /// When given it decides <c>source</c>; <c>api</c> still comes from the exception's own service name where that is known.
    /// </param>
    public static ProblemInfo Map(Exception ex, string? source = null)
    {
        ArgumentNullException.ThrowIfNull(ex);

        return ex switch
        {
            ReportFetchException fetch => new ProblemInfo(ErrorCodes.IsKnown(fetch.Code) ? fetch.Code : ErrorCodes.Unknown),
            GoogleApiException google => FromGoogle(google, KnownSource(source)),
            TokenResponseException token => new ProblemInfo(RefusesTheKey(token) ? ErrorCodes.KeyInvalid : ErrorCodes.Unknown),
            _ => new ProblemInfo(ErrorCodes.Unknown),
        };
    }

    /// <summary>
    /// The HTTP status behind a failed call, or null when the exception has none. For the LOG only (an exception that no code names is
    /// logged with its type and this status, so it can be found); it is never part of a <see cref="ProblemInfo"/>.
    /// </summary>
    internal static int? HttpStatusOf(Exception ex) => ex switch
    {
        GoogleApiException { HttpStatusCode: not 0 } google => (int)google.HttpStatusCode,
        TokenResponseException { StatusCode: { } code } => (int)code,
        _ => null,
    };

    private static bool RefusesTheKey(TokenResponseException token) => token.Error?.Error is { } error && RefusedKeyErrors.Contains(error, StringComparer.Ordinal);

    /// <summary>"Connected, but nothing there yet" for <paramref name="source"/> (<see cref="Ga4"/> or <see cref="SearchConsole"/>).</summary>
    public static ProblemInfo NoData(string source) => WithSource(ErrorCodes.NoData, KnownSource(source));

    private static ProblemInfo FromGoogle(GoogleApiException ex, string? callerSource)
    {
        var facts = Facts.Of(ex);
        var service = ServiceOf(ex.ServiceName);
        var source = callerSource ?? SourceOf(service);

        if (facts.Status == 403 && facts.HasAny(ServiceDisabledReasons))
        {
            var api = service ?? ApiOf(source);
            return api is null
                ? new ProblemInfo(ErrorCodes.ApiDisabled)
                : new ProblemInfo(ErrorCodes.ApiDisabled, Args((ApiArg, api)));
        }

        if (facts.Status == 429 || facts.RpcStatus == ResourceExhausted || (facts.Status == 403 && facts.HasAny(QuotaReasons)))
        {
            return new ProblemInfo(ErrorCodes.Quota);
        }

        return facts.Status switch
        {
            404 => WithSource(ErrorCodes.PropertyNotFound, source),
            401 or 403 => WithSource(ErrorCodes.NoAccess, source),
            _ => new ProblemInfo(ErrorCodes.Unknown),
        };
    }

    private static ProblemInfo WithSource(string code, string? source)
        => source is null ? new ProblemInfo(code) : new ProblemInfo(code, Args((SourceArg, source)));

    private static Dictionary<string, object?> Args(params (string Name, string Value)[] pairs)
        => pairs.ToDictionary(pair => pair.Name, pair => (object?)pair.Value, StringComparer.Ordinal);

    // The whitelist: only these exact words go on, whatever was passed in.
    private static string? KnownSource(string? source) => source is Ga4 or SearchConsole ? source : null;

    private static string? ServiceOf(string? serviceName) => serviceName switch
    {
        AnalyticsDataServiceName => AnalyticsDataApi,
        SearchConsoleServiceName => SearchConsoleApi,
        _ => null,
    };

    private static string? SourceOf(string? api) => api switch
    {
        AnalyticsDataApi => Ga4,
        SearchConsoleApi => SearchConsole,
        _ => null,
    };

    private static string? ApiOf(string? source) => source switch
    {
        Ga4 => AnalyticsDataApi,
        SearchConsole => SearchConsoleApi,
        _ => null,
    };

    /// <summary>
    /// What an exception says that the mapper may use: the HTTP status, the RPC status, and every reason, from the error list and from
    /// the body's details. Google's client fills the status and the error list; it has no property for the RPC status or the details,
    /// so those are read from <c>Error.ErrorResponseContent</c>, the body as it came. The reasons are kept for comparison only: they
    /// are never put in a result.
    /// </summary>
    private sealed class Facts
    {
        private readonly HashSet<string> _reasons = new(StringComparer.OrdinalIgnoreCase);

        public int Status { get; private init; }

        public string? RpcStatus { get; private set; }

        public bool HasAny(string[] reasons) => reasons.Any(_reasons.Contains);

        public static Facts Of(GoogleApiException ex)
        {
            var facts = new Facts { Status = ex.HttpStatusCode != 0 ? (int)ex.HttpStatusCode : ex.Error?.Code ?? 0 };

            foreach (var single in ex.Error?.Errors ?? [])
            {
                if (single?.Reason is { Length: > 0 } reason) facts._reasons.Add(reason);
            }

            facts.ReadBody(ex.Error?.ErrorResponseContent);
            return facts;
        }

        private void ReadBody(string? body)
        {
            if (string.IsNullOrEmpty(body) || body.Length > MaxBodyChars) return;

            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind != JsonValueKind.Object
                    || !document.RootElement.TryGetProperty("error", out var error)
                    || error.ValueKind != JsonValueKind.Object)
                {
                    return;
                }

                if (error.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String) RpcStatus = status.GetString();

                if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
                {
                    foreach (var detail in details.EnumerateArray())
                    {
                        if (detail.ValueKind == JsonValueKind.Object
                            && detail.TryGetProperty("reason", out var reason)
                            && reason.ValueKind == JsonValueKind.String
                            && reason.GetString() is { Length: > 0 } text)
                        {
                            _reasons.Add(text);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException)
            {
                // Not JSON (an HTML page from a proxy, say): the HTTP status alone decides. The parser's message can quote the body,
                // so it is dropped here.
            }
        }
    }
}
