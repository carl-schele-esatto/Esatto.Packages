namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// A visitor's recorded consent choice, as carried by the consent cookie (its name comes from
/// <see cref="CookieBannerOptions.CookieName"/>).
/// </summary>
public sealed record ConsentDecision(
    int PolicyVersion,
    DateTimeOffset DecidedAt,
    string ConsentId,
    IReadOnlySet<ConsentCategory> Granted)
{
    /// <summary>
    /// True when the category may run. <see cref="ConsentCategory.Necessary"/> is implied rather
    /// than stored, so it is always granted.
    /// </summary>
    public bool HasGranted(ConsentCategory category)
        => category == ConsentCategory.Necessary || Granted.Contains(category);

    /// <summary>
    /// True when the visitor last decided against an older version of the consent text, which means
    /// the banner must be shown again with their previous choice pre-selected.
    /// </summary>
    public bool NeedsRePrompt(int currentPolicyVersion) => PolicyVersion < currentPolicyVersion;
}
