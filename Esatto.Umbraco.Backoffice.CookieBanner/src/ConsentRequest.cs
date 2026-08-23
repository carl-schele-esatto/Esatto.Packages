namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Body of the consent endpoint. Every field is untrusted and validated server-side: the action is
/// parsed by <see cref="ConsentCookieWriter.TryParseAction"/> and unknown categories are dropped.
/// </summary>
internal sealed record ConsentRequest(string[]? Categories, string Action);
