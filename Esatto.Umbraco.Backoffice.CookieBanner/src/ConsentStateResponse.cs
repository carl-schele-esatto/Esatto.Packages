namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Canonical consent state after a decision: the endpoint's confirmation that the server accepted
/// it and what it actually stored, not an echo of what the client asked for.
/// </summary>
/// <remarks>
/// The shipped <c>consent.js</c> does not read individual fields out of this response - after a
/// successful request it reloads the page, which re-renders every server-driven bit of consent
/// state (the tag helpers, the dialog, this very cookie) from scratch, rather than trying to patch
/// the page in place from this payload. The shape still matters: this is the body of a documented
/// HTTP endpoint, and a consumer who calls it directly from their own script depends on it too.
/// </remarks>
internal sealed record ConsentStateResponse(
    int Version,
    string[] Categories,
    string ConsentId,
    string DecidedAt);
