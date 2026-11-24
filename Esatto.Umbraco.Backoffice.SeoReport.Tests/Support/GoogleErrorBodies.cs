using System.Net;
using System.Text.Json.Nodes;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>
/// Error bodies the way Google writes them, with <see cref="FakeServiceAccount.LeakMarker"/> in the message so a test can prove the
/// text of a response never travels on. All made up; nothing here is a real response.
/// </summary>
internal static class GoogleErrorBodies
{
    private const string Marker = FakeServiceAccount.LeakMarker;

    /// <summary>
    /// An error body: the code, a message with the marker in it, the RPC status, and optionally a reason in the error list (the old
    /// form, <c>errors[].reason</c>) and one in the details (the new form, <c>details[].reason</c> of an ErrorInfo).
    /// </summary>
    public static string Body(int code, string status, string? listReason = null, string? detailReason = null)
    {
        var error = new JsonObject { ["code"] = code, ["message"] = $"made up {Marker}", ["status"] = status };
        if (listReason is not null)
        {
            error["errors"] = new JsonArray(new JsonObject { ["message"] = "m", ["domain"] = "usageLimits", ["reason"] = listReason });
        }

        if (detailReason is not null)
        {
            error["details"] = new JsonArray(new JsonObject
            {
                ["@type"] = "type.googleapis.com/google.rpc.ErrorInfo",
                ["reason"] = detailReason,
                ["domain"] = "googleapis.com",
                ["metadata"] = new JsonObject { ["service"] = "x.googleapis.com", ["consumer"] = "projects/123" },
            });
        }

        return new JsonObject { ["error"] = error }.ToJsonString();
    }

    /// <summary>A disabled API as Google answers it today: a 403 PERMISSION_DENIED whose list carries the old reason and whose details carry the new one.</summary>
    public static string Disabled(bool inList = true, bool inDetails = true)
        => Body(403, "PERMISSION_DENIED", inList ? "accessNotConfigured" : null, inDetails ? "SERVICE_DISABLED" : null);

    /// <summary>A 403 PERMISSION_DENIED with no reason: the service account has not been given access.</summary>
    public static string Denied() => Body(403, "PERMISSION_DENIED");

    public static string Error(int code, string status, string? reason = null) => Body(code, status, reason);

    /// <summary>An answer of the given status carrying <paramref name="body"/>.</summary>
    public static FakeGoogleTransport.Answer Answer(HttpStatusCode status, string body) => new(status, body);

    /// <summary>A 403 for a switched-off API.</summary>
    public static FakeGoogleTransport.Answer DisabledAnswer() => new(HttpStatusCode.Forbidden, Disabled());

    /// <summary>A 403 for a missing permission.</summary>
    public static FakeGoogleTransport.Answer DeniedAnswer() => new(HttpStatusCode.Forbidden, Denied());

    /// <summary>A 404.</summary>
    public static FakeGoogleTransport.Answer NotFoundAnswer() => new(HttpStatusCode.NotFound, Error(404, "NOT_FOUND"));
}
