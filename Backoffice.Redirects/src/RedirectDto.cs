namespace Backoffice.Redirects;

public sealed record RedirectDto(int Id, string SiteKey, string OldPath, string NewUrl);

// SiteKey is nullable: single-site installs operate on the empty key, which the
// client sends as "". A non-nullable string here makes NRT add an implicit
// [Required] that rejects empty strings, 400-ing every single-site request
// before the action runs. The service layer normalises null/empty to "".
public sealed record CreateRedirectRequest(string? SiteKey, string OldPath, string NewUrl);

public sealed record UpdateRedirectRequest(string? SiteKey, string OldPath, string NewUrl);
