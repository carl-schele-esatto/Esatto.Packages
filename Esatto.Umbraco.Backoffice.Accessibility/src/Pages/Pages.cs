namespace Esatto.Umbraco.Backoffice.Accessibility.Pages;

/// <summary>A website in the installation: one root content node, with the hosts bound to it.</summary>
public sealed record SiteInfo(Guid Key, string Name, IReadOnlyList<string> Hosts);

/// <summary>A published content item (one culture) that might be scanned.</summary>
public sealed record PageCandidate(Guid ContentKey, string Name, string Culture, string? AbsoluteUrl, bool Protected, bool HasTemplate);

/// <summary>
/// A page in the scan list. <see cref="SkipReason"/> is null for pages to scan, otherwise
/// <c>protected</c>, <c>excluded</c> or <c>no-url</c> (the dashboard adds <c>other-host:…</c>).
/// </summary>
public sealed record ScanPage(Guid ContentKey, string Name, string Culture, string Url, string? SkipReason);

public static class SkipReasons
{
    public const string Protected = "protected";
    public const string Excluded = "excluded";
    public const string NoUrl = "no-url";
}
