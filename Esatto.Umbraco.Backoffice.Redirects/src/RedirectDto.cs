namespace Esatto.Umbraco.Backoffice.Redirects;

public sealed record RedirectDto(int Id, string OldPath, string NewUrl);

public sealed record CreateRedirectRequest(string OldPath, string NewUrl);

public sealed record UpdateRedirectRequest(string OldPath, string NewUrl);
