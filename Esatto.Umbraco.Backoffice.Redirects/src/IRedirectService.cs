namespace Esatto.Umbraco.Backoffice.Redirects;

public interface IRedirectService
{
    /// <summary>All redirects, ordered by old path.</summary>
    Task<IReadOnlyList<RedirectDto>> GetAllAsync();

    /// <summary>Looks up a destination by inbound path. Returns null if none.</summary>
    Task<string?> LookupAsync(string path);

    /// <summary>Creates a redirect. Returns null on success, or a validation error message.</summary>
    Task<string?> TryCreateAsync(CreateRedirectRequest request);

    /// <summary>Updates an existing redirect. Returns null on success, error message on failure.</summary>
    Task<string?> TryUpdateAsync(int id, UpdateRedirectRequest request);

    /// <summary>Deletes by id. Returns true if a row was removed.</summary>
    Task<bool> DeleteAsync(int id);
}
