namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// One declared cookie, storage entry or pixel, projected out of an editor-managed
/// <c>cookieDefinition</c> block.
/// </summary>
/// <remarks>
/// Deliberately free of Umbraco types: it is what lets the grouping rules be unit tested without a
/// published content graph, and it is the only shape the two views need.
/// <paramref name="Name"/> is the cookie/storage key; a declaration with a blank one is dropped by
/// <c>CookieRegistry.Group</c> rather than rendered as an empty cell.
/// </remarks>
public sealed record CookieDeclaration(
    string Name,
    string Provider,
    ConsentCategory Category,
    string Purpose,
    string Duration,
    string StorageType);
