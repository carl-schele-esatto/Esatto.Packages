using Esatto.Umbraco.Backoffice.Seo.Evaluation;

namespace Esatto.Umbraco.Backoffice.Seo.Pages;

/// <summary>A website in the installation: one root content node, with the hosts bound to it.</summary>
public sealed record SiteInfo(Guid Key, string Name, IReadOnlyList<string> Hosts);

/// <summary>A published content item (one culture) that might be scanned. <see cref="Url"/> may be relative or "#".</summary>
public sealed record PageCandidate(
    Guid ContentKey, string Name, string Culture, string? Url, bool Protected, bool HasTemplate,
    bool IsRoot, bool IsVariant, IReadOnlyList<string> OtherCultures);

/// <summary>A page in the scan list; <see cref="SkipReason"/> is null for pages to fetch.</summary>
public sealed record ScanPage(PageRef Ref, string? SkipReason);

public static class SkipReasons
{
    public const string Protected = "protected";
    public const string Excluded = "excluded";
    public const string NoUrl = "no-url";
}

/// <summary>Reads sites and their published pages from Umbraco.</summary>
public interface IPageSource
{
    Task<IReadOnlyList<SiteInfo>> GetSitesAsync();

    Task<IReadOnlyList<PageCandidate>> GetCandidatesAsync(Guid siteKey);

    /// <summary>The site's languages: cultures of its domains, else every installed language.</summary>
    Task<IReadOnlyList<string>> GetSiteCulturesAsync(Guid siteKey);
}
