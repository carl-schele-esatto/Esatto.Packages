using System.Text.Json.Serialization;

namespace Esatto.CookieScan.Core;

/// <summary>
/// One row of the cookie catalogue: what a recognised name is, who sets it, and what to write
/// about it on the policy page.
/// </summary>
/// <remarks>
/// <paramref name="DurationDays"/> is machine-readable rather than pre-written text so that
/// <c>DurationFormatter</c> can render it in the requested locale - the spec's original
/// "24 månader" string could not honour an English run. <c>0</c> means a session cookie;
/// <c>null</c> means no documented lifetime, so use what the browser reported.
/// <para>
/// <paramref name="ConsentCookie"/> marks the one entry whose name is a per-site setting rather
/// than a fact about a product: the banner's own consent cookie, whose name any site may change.
/// <c>CookieCatalogue.WithConsentCookieNamed</c> rewrites the pattern of whatever is flagged here,
/// so a renamed consent cookie is still recognised as necessary instead of being reported as an
/// undeclared marketing cookie set in defiance of a refusal.
/// </para>
/// </remarks>
public sealed record CatalogueEntry(
    [property: JsonPropertyName("pattern")] string Pattern,
    [property: JsonPropertyName("provider")] LocalisedText Provider,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("purpose")] LocalisedText Purpose,
    [property: JsonPropertyName("durationDays")] int? DurationDays = null,
    [property: JsonPropertyName("tracker")] bool Tracker = false,
    [property: JsonPropertyName("expected")] bool Expected = false,
    [property: JsonPropertyName("consentCookie")] bool ConsentCookie = false);
