using System.Collections.Generic;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Projects a <c>cookieRegistry</c> Block List into <see cref="CookieDeclaration"/> records.
/// </summary>
/// <remarks>
/// The only place in the package that touches <c>cookieDefinition</c> property aliases, so both the
/// banner and the policy page read the same block the same way.
/// <para>
/// PUBLIC, deliberately - see <see cref="CookieRegistry"/>'s remarks. <c>Views/CookiePolicy.cshtml</c>
/// calls this, and that file is compiled in the consumer's own assembly context once Umbraco writes
/// it to their <c>Views/CookiePolicy.cshtml</c> at install time.
/// </para>
/// </remarks>
public static class CookieDeclarationMapper
{
    /// <summary>
    /// Maps every block whose <c>category</c> parses to a known wire name. An unparsable or missing
    /// category is dropped: defaulting it to <c>necessary</c> would show a cookie as needing no
    /// consent while the gating code would never grant it.
    /// </summary>
    public static IReadOnlyList<CookieDeclaration> FromBlockList(
        BlockListModel? blocks,
        IPublishedValueFallback publishedValueFallback)
    {
        if (blocks is null)
        {
            return [];
        }

        var declarations = new List<CookieDeclaration>();

        foreach (BlockListItem block in blocks)
        {
            var wireCategory = block.Content.Value<string>(publishedValueFallback, "category");
            if (ConsentCategories.TryParse(wireCategory, out ConsentCategory category) is false)
            {
                continue;
            }

            declarations.Add(new CookieDeclaration(
                Name: block.Content.Value<string>(publishedValueFallback, "cookieName") ?? string.Empty,
                Provider: block.Content.Value<string>(publishedValueFallback, "provider") ?? string.Empty,
                Category: category,
                Purpose: block.Content.Value<string>(publishedValueFallback, "purpose") ?? string.Empty,
                Duration: block.Content.Value<string>(publishedValueFallback, "duration") ?? string.Empty,
                StorageType: block.Content.Value<string>(publishedValueFallback, "storageType") ?? string.Empty));
        }

        return declarations;
    }
}
