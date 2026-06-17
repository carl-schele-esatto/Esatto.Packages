using System.Net;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Routing;

namespace Esatto.Umbraco.Backoffice.Redirects;

/// <summary>
/// Resolves a 301 redirect from the <c>Redirects</c> table during Umbraco's
/// request pipeline. Hooks into Umbraco's <see cref="IContentFinder"/> chain.
/// </summary>
public sealed class RedirectContentFinder : IContentFinder
{
    private readonly IRedirectService _redirects;
    private readonly ILogger<RedirectContentFinder> _logger;

    public RedirectContentFinder(
        IRedirectService redirects,
        ILogger<RedirectContentFinder> logger)
    {
        _redirects = redirects;
        _logger = logger;
    }

    public async Task<bool> TryFindContent(IPublishedRequestBuilder request)
    {
        var path = request.AbsolutePathDecoded;
        if (string.IsNullOrEmpty(path)) return false;

        var destination = await _redirects.LookupAsync(path);
        if (destination is null) return false;

        var query = request.Uri.Query;
        var finalUrl = AppendQuery(destination, query);

        _logger.LogInformation("Redirecting {From} -> {To} (301)", path, finalUrl);
        request.SetRedirect(finalUrl, (int)HttpStatusCode.MovedPermanently);
        return true;
    }

    private static string AppendQuery(string destination, string incomingQuery)
    {
        if (string.IsNullOrEmpty(incomingQuery)) return destination;

        // incomingQuery begins with '?'; after trimming, a bare '?' leaves nothing to merge.
        var stripped = incomingQuery.TrimStart('?');
        if (stripped.Length == 0) return destination;

        // Split off any fragment so the query merges BEFORE the '#'.
        var hashIndex = destination.IndexOf('#');
        var beforeHash = hashIndex >= 0 ? destination[..hashIndex] : destination;
        var afterHash = hashIndex >= 0 ? destination[hashIndex..] : string.Empty;

        var separator = beforeHash.Contains('?') ? '&' : '?';
        return beforeHash + separator + stripped + afterHash;
    }
}
