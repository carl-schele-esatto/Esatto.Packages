namespace Backoffice.Redirects;

public interface IRedirectService
{
    /// <summary>All redirects for one site.</summary>
    Task<IReadOnlyList<RedirectDto>> GetAllAsync(string siteKey);

    /// <summary>Looks up a destination by inbound site + path. Returns null if none.</summary>
    Task<string?> LookupAsync(string siteKey, string path);

    /// <summary>Returns the site key of the redirect with the given id, or null if no such row exists.
    /// Used by the controller to gate Update/Delete on the row's actual site rather than the client's claim.</summary>
    Task<string?> GetSiteKeyAsync(int id);

    /// <summary>Creates a redirect. Returns null on success, or a validation error message.</summary>
    Task<string?> TryCreateAsync(CreateRedirectRequest request);

    /// <summary>Updates an existing redirect. Returns null on success, error message on failure.</summary>
    Task<string?> TryUpdateAsync(int id, UpdateRedirectRequest request);

    /// <summary>Deletes by id. Returns true if a row was removed.</summary>
    Task<bool> DeleteAsync(int id);
}
