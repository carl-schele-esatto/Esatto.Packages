using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>
/// A synthetic Google service-account key in the shape Google's console downloads. The RSA key is generated here, once per
/// test process, and is worthless anywhere else: a real key must never enter this repository, in a test or otherwise.
/// </summary>
internal static class FakeServiceAccount
{
    public const string DefaultClientEmail = "svc@proj.iam.gserviceaccount.com";

    /// <summary>A text that must never surface in a result, an error code, an exception or a log line.</summary>
    public const string LeakMarker = "LEAK-MARKER-9f3a";

    private static readonly Lazy<string> GeneratedPem = new(() =>
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportPkcs8PrivateKeyPem();
    });

    /// <summary>The PKCS#8 PEM of the synthetic key: <c>-----BEGIN PRIVATE KEY-----</c>, base64 lines, END line, <c>\n</c> newlines.</summary>
    public static string PrivateKeyPem => GeneratedPem.Value;

    /// <summary>The key as a mutable object, so a test can remove or replace a property before serializing it.</summary>
    public static JsonObject Object(
        string clientEmail = DefaultClientEmail,
        string? privateKey = null,
        string type = "service_account")
        => new()
        {
            ["type"] = type,
            ["project_id"] = "proj",
            ["private_key_id"] = "0123456789abcdef0123456789abcdef01234567",
            ["private_key"] = privateKey ?? PrivateKeyPem,
            ["client_email"] = clientEmail,
            ["client_id"] = "123456789012345678901",
            ["auth_uri"] = "https://accounts.google.com/o/oauth2/auth",
            ["token_uri"] = "https://oauth2.googleapis.com/token",
            ["auth_provider_x509_cert_url"] = "https://www.googleapis.com/oauth2/v1/certs",
            ["client_x509_cert_url"] = "https://www.googleapis.com/robot/v1/metadata/x509/svc%40proj.iam.gserviceaccount.com",
            ["universe_domain"] = "googleapis.com",
        };

    /// <summary>The key as the JSON text Google's console downloads (compact, <c>\n</c>-escaped PEM).</summary>
    public static string Json(
        string clientEmail = DefaultClientEmail,
        string? privateKey = null,
        string type = "service_account")
        => Object(clientEmail, privateKey, type).ToJsonString();
}
