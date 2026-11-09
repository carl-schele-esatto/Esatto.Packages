using Microsoft.Extensions.Configuration;

namespace Esatto.Umbraco.Backoffice.Seo.Settings;

/// <summary>
/// Per-environment values that live in configuration only, never in the database (a database value would
/// travel with content between environments, and secrets don't belong there):
/// <c>Esatto:Seo:IntentionallyHidden</c>, <c>Esatto:Seo:RequestHeaders:*</c>, and Umbraco's basic-auth
/// shared secret so protected Cloud environments can be scanned.
/// </summary>
public sealed class SeoEnvironment
{
    public const string IntentionallyHiddenKey = "IntentionallyHidden";
    public const string RequestHeadersKey = "RequestHeaders";
    private const string DefaultSecretHeader = "X-Authentication-Shared-Secret";

    public SeoEnvironment(IConfiguration configuration)
    {
        var section = configuration.GetSection(Settings.SeoSettings.SectionName);
        IntentionallyHidden = section.GetValue<bool?>(IntentionallyHiddenKey) ?? false;

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in section.GetSection(RequestHeadersKey).GetChildren())
            if (!string.IsNullOrEmpty(child.Value)) headers[child.Key] = child.Value;

        var secret = configuration.GetSection("Umbraco:CMS:BasicAuth:SharedSecret");
        if (!string.IsNullOrEmpty(secret["Value"]))
            headers[string.IsNullOrWhiteSpace(secret["HeaderName"]) ? DefaultSecretHeader : secret["HeaderName"]!] = secret["Value"]!;

        RequestHeaders = headers;
    }

    /// <summary>This environment is hidden from search engines on purpose (Test/Dev).</summary>
    public bool IntentionallyHidden { get; }

    /// <summary>Extra headers sent with every scan request.</summary>
    public IReadOnlyDictionary<string, string> RequestHeaders { get; }
}
