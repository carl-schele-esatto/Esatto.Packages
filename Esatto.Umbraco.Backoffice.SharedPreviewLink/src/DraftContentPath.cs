using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Navigation;

namespace Esatto.Umbraco.Backoffice.SharedPreviewLink;

/// <summary>
/// Builds a document's URL path from its DRAFT URL segments, so never-published pages get their
/// own path. Shared by <see cref="PreviewLinkController"/> (mint) and
/// <see cref="PreviewLinkMiddleware"/> (legacy-token verify).
/// </summary>
/// <remarks>
/// Don't use <c>IPublishedContent.UrlSegment(culture)</c> here: since Umbraco 18 it reads the
/// draft segment only when the CURRENT request is in preview mode. Minting is a normal backoffice
/// API call, so for a never-published page it returned null, the page's own segment was dropped,
/// and the link pointed at the parent page. Asking <see cref="IDocumentUrlService"/> for
/// <c>isDraft: true</c> is explicit, and the draft segment equals the published one for pages
/// without unpublished edits. Walking keys via the navigation service also avoids
/// <c>IPublishedContent.Parent</c>, which is unreliable on draft-loaded content.
/// </remarks>
internal static class DraftContentPath
{
    /// <summary>
    /// The key of the root (level-1) node the document lives under — the document itself when
    /// it is a root.
    /// </summary>
    public static Guid RootKey(IDocumentNavigationQueryService navigation, Guid documentKey) =>
        navigation.TryGetAncestorsKeys(documentKey, out var ancestors) && ancestors.Any()
            ? ancestors.Last() // immediate parent first, root last
            : documentKey;

    /// <summary>
    /// The path BELOW the root node from draft segments: "" for the root/home page itself,
    /// "/news" for a level-2 page, "/news/article" for level 3. The root's own segment is never
    /// part of the URL (the root maps to the culture/domain root).
    /// </summary>
    public static string RelativePath(
        IDocumentNavigationQueryService navigation,
        IDocumentUrlService documentUrlService,
        Guid documentKey,
        string? culture)
    {
        if (!navigation.TryGetAncestorsKeys(documentKey, out var ancestors))
        {
            return string.Empty;
        }

        // Root first, self last, then drop the root.
        var chain = ancestors.Reverse().Append(documentKey).Skip(1);
        var segments = chain
            .Select(key => documentUrlService.GetUrlSegment(key, culture ?? string.Empty, isDraft: true))
            .Where(segment => !string.IsNullOrEmpty(segment))
            .ToList();

        return segments.Count == 0 ? string.Empty : "/" + string.Join("/", segments);
    }
}
