namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Bound from the <c>Esatto:CookieBanner</c> configuration section. Every value has a
/// package-neutral default, so an empty section is a valid configuration.
/// </summary>
public sealed class CookieBannerOptions
{
    public const string SectionName = "Esatto:CookieBanner";

    /// <summary>
    /// Version of the consent text. Bumping this re-prompts every visitor, so it is configuration
    /// rather than a constant: rewording the policy is a deploy-time decision, not a code change.
    /// </summary>
    public int PolicyVersion { get; set; } = 1;

    /// <summary>
    /// Name of the consent cookie. Point this at an existing name when migrating from a
    /// hand-rolled banner and no visitor is re-prompted.
    /// </summary>
    public string CookieName { get; set; } = "cookie-consent";

    public int CookieLifetimeDays { get; set; } = 365;

    /// <summary>
    /// Google measurement id. When null, no Consent Mode snippet and no gtag script are emitted at
    /// all, rather than shipping dead script to every page.
    /// </summary>
    public string? GoogleMeasurementId { get; set; }

    /// <summary>
    /// Optional override for policy-page resolution. When null, the first published node of
    /// document type <c>cookiePolicy</c> is used.
    /// </summary>
    public Guid? PolicyPageKey { get; set; }

    /// <summary>Path the consent endpoint is mapped on by <c>UseCookieConsent()</c>.</summary>
    public string EndpointPath { get; set; } = "/api/cookie-consent";

    /// <summary>Sliding-window budget per client IP for the consent endpoint.</summary>
    public int ThrottleRequestsPerMinute { get; set; } = 10;
}
