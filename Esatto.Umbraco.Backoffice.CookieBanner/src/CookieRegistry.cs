using System.Collections.Generic;
using System.Linq;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Groups declared cookies by consent category for the banner and the policy page.
/// </summary>
/// <remarks>
/// This logic used to exist twice - once in the consent dialog partial, once in the policy-page
/// template - with a comment in each claiming the two agreed. They did not: only the banner dropped
/// blocks with a blank cookie name. One function, one tested behaviour.
/// </remarks>
internal static class CookieRegistry
{
    /// <summary>
    /// Returns one bucket per <see cref="ConsentCategories.All"/> entry, in that order, so callers
    /// can index by category unconditionally. Declarations with a blank
    /// <see cref="CookieDeclaration.Name"/>, or a category outside the known set, are dropped.
    /// </summary>
    public static IReadOnlyDictionary<ConsentCategory, IReadOnlyList<CookieDeclaration>> Group(
        IEnumerable<CookieDeclaration> declarations)
    {
        Dictionary<ConsentCategory, List<CookieDeclaration>> buckets =
            ConsentCategories.All.ToDictionary(category => category, _ => new List<CookieDeclaration>());

        foreach (CookieDeclaration declaration in declarations)
        {
            // A cookie with no name tells a visitor nothing and cannot be matched against anything a
            // scanner finds, so it is editor noise rather than a declaration.
            if (string.IsNullOrWhiteSpace(declaration.Name))
            {
                continue;
            }

            // Defensive: the mapper already refuses unparsable category values, but an out-of-range
            // enum cast must not become a KeyNotFoundException halfway through rendering a dialog.
            if (buckets.TryGetValue(declaration.Category, out List<CookieDeclaration>? bucket) is false)
            {
                continue;
            }

            bucket.Add(declaration);
        }

        // Rebuilt in All order so key enumeration is the documented display order rather than
        // whatever insertion order happens to yield.
        return ConsentCategories.All.ToDictionary(
            category => category,
            category => (IReadOnlyList<CookieDeclaration>)buckets[category]);
    }
}
