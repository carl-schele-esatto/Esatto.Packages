using System;
using System.Collections.Generic;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Everything <c>Views/Shared/Components/ConsentBanner/Default.cshtml</c> renders.
/// </summary>
/// <remarks>
/// Public because a Razor view's model type appears in the generated view class's base type, so it
/// cannot be less accessible than the view. <see cref="Text"/> is a delegate over the internal
/// text provider for the same reason: the view must not name an internal type in a member signature.
/// </remarks>
/// <param name="NeedsDecision">
/// True on first run. Drives both the collapsed first-run layout and <c>data-consent-needs-decision</c>.
/// </param>
/// <param name="Granted">
/// Read from <c>IConsentState.HasGranted</c>, never from the raw decision, so a decision made against
/// an older policy version pre-ticks nothing.
/// </param>
/// <param name="Text">Key lookup: dictionary item, then embedded resx for the request culture, then English.</param>
public sealed record ConsentBannerViewModel(
    bool NeedsDecision,
    IReadOnlySet<ConsentCategory> Granted,
    IReadOnlyDictionary<ConsentCategory, IReadOnlyList<CookieDeclaration>> CookiesByCategory,
    string CookieName,
    int PolicyVersion,
    string EndpointPath,
    bool ConsentModeEnabled,
    Func<string, string> Text);
