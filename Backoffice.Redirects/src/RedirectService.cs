using NPoco;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Backoffice.Redirects;

public sealed class RedirectService : IRedirectService
{
    private readonly IScopeProvider _scopeProvider;

    private volatile Dictionary<string, Dictionary<string, string>>? _cache; // siteKey → (oldPath → newUrl)
    private readonly object _cacheLock = new();

    public RedirectService(IScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    public Task<IReadOnlyList<RedirectDto>> GetAllAsync(string siteKey)
    {
        siteKey ??= string.Empty;
        using var scope = _scopeProvider.CreateScope(autoComplete: true);
        var rows = scope.Database
            .Query<RedirectEntity>()
            .Where(r => r.SiteKey == siteKey)
            .OrderBy(r => r.OldPath)
            .ToList();

        IReadOnlyList<RedirectDto> result = rows
            .Select(r => new RedirectDto(r.Id, r.SiteKey, r.OldPath, r.NewUrl))
            .ToList();
        return Task.FromResult(result);
    }

    public Task<string?> LookupAsync(string siteKey, string path)
    {
        var key = Normalize(path);
        if (string.IsNullOrEmpty(key)) return Task.FromResult<string?>(null);

        var perSite = GetOrLoadCache();
        if (!perSite.TryGetValue(siteKey ?? string.Empty, out var siteCache)) return Task.FromResult<string?>(null);
        return Task.FromResult(siteCache.TryGetValue(key, out var target) ? target : null);
    }

    public Task<string?> GetSiteKeyAsync(int id)
    {
        using var scope = _scopeProvider.CreateScope(autoComplete: true);
        var siteKey = scope.Database.ExecuteScalar<string?>(
            $"SELECT siteKey FROM {RedirectEntity.TableName} WHERE id = @0", id);
        return Task.FromResult(siteKey);
    }

    public Task<string?> TryCreateAsync(CreateRedirectRequest request)
    {
        // Empty siteKey is valid (single-site mode). Trim + lowercase consistently.
        var siteKey = (request.SiteKey ?? string.Empty).Trim().ToLowerInvariant();

        var oldPath = Normalize(request.OldPath);
        var newUrl = (request.NewUrl ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(oldPath)) return Task.FromResult<string?>("Old URL is required.");
        if (!oldPath.StartsWith('/')) return Task.FromResult<string?>("Old URL must start with '/'.");
        if (!string.IsNullOrEmpty(newUrl) && !IsValidDestination(newUrl))
            return Task.FromResult<string?>("New URL must be a relative path (/...) or absolute URL.");
        if (!string.IsNullOrEmpty(newUrl) && string.Equals(oldPath, newUrl, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<string?>("Old URL and New URL cannot be the same.");

        InvalidateCache();
        using (var scope = _scopeProvider.CreateScope())
        {
            var exists = scope.Database.ExecuteScalar<int>(
                $"SELECT COUNT(*) FROM {RedirectEntity.TableName} WHERE siteKey = @0 AND oldPath = @1",
                siteKey, oldPath) > 0;
            if (exists)
            {
                scope.Complete();
                return Task.FromResult<string?>("A redirect for that Old URL already exists.");
            }

            var now = DateTime.UtcNow;
            scope.Database.Insert(new RedirectEntity
            {
                SiteKey = siteKey,
                OldPath = oldPath,
                NewUrl = newUrl,
                CreatedUtc = now,
                UpdatedUtc = now,
            });
            scope.Complete();
        }

        InvalidateCache();
        return Task.FromResult<string?>(null);
    }

    public Task<string?> TryUpdateAsync(int id, UpdateRedirectRequest request)
    {
        var siteKey = (request.SiteKey ?? string.Empty).Trim().ToLowerInvariant();

        var oldPath = Normalize(request.OldPath);
        var newUrl = (request.NewUrl ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(oldPath)) return Task.FromResult<string?>("Old URL is required.");
        if (!oldPath.StartsWith('/')) return Task.FromResult<string?>("Old URL must start with '/'.");
        if (!string.IsNullOrEmpty(newUrl) && !IsValidDestination(newUrl))
            return Task.FromResult<string?>("New URL must be a relative path (/...) or absolute URL.");
        if (!string.IsNullOrEmpty(newUrl) && string.Equals(oldPath, newUrl, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<string?>("Old URL and New URL cannot be the same.");

        using (var scope = _scopeProvider.CreateScope())
        {
            var existing = scope.Database.SingleOrDefaultById<RedirectEntity>(id);
            if (existing is null)
            {
                scope.Complete();
                return Task.FromResult<string?>("Redirect not found.");
            }

            // Site-key changes are not allowed via update — that's effectively "move to another
            // site" and we want to keep the audit trail clean. Reject if the request tries it.
            if (!string.Equals(existing.SiteKey, siteKey, StringComparison.OrdinalIgnoreCase))
            {
                scope.Complete();
                return Task.FromResult<string?>("Cannot move a redirect between sites.");
            }

            if (!string.Equals(existing.OldPath, oldPath, StringComparison.OrdinalIgnoreCase))
            {
                var conflict = scope.Database.ExecuteScalar<int>(
                    $"SELECT COUNT(*) FROM {RedirectEntity.TableName} WHERE siteKey = @0 AND oldPath = @1 AND id <> @2",
                    siteKey, oldPath, id) > 0;
                if (conflict)
                {
                    scope.Complete();
                    return Task.FromResult<string?>("A redirect for that Old URL already exists.");
                }
            }

            existing.OldPath = oldPath;
            existing.NewUrl = newUrl;
            existing.UpdatedUtc = DateTime.UtcNow;
            scope.Database.Update(existing);
            scope.Complete();
        }

        InvalidateCache();
        return Task.FromResult<string?>(null);
    }

    public Task<bool> DeleteAsync(int id)
    {
        InvalidateCache();
        int rows;
        using (var scope = _scopeProvider.CreateScope())
        {
            rows = scope.Database.Execute(
                $"DELETE FROM {RedirectEntity.TableName} WHERE id = @0", id);
            scope.Complete();
        }
        if (rows == 0) return Task.FromResult(false);

        InvalidateCache();
        return Task.FromResult(true);
    }

    private Dictionary<string, Dictionary<string, string>> GetOrLoadCache()
    {
        if (_cache is not null) return _cache;
        lock (_cacheLock)
        {
            if (_cache is not null) return _cache;

            using var scope = _scopeProvider.CreateScope(autoComplete: true);
            var rows = scope.Database.Query<RedirectEntity>().ToList();

            var cache = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.NewUrl)) continue; // drafts excluded
                var bucketKey = row.SiteKey ?? string.Empty;
                if (!cache.TryGetValue(bucketKey, out var siteMap))
                {
                    siteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    cache[bucketKey] = siteMap;
                }
                siteMap[row.OldPath] = row.NewUrl;
            }

            _cache = cache;
            return _cache;
        }
    }

    private void InvalidateCache()
    {
        lock (_cacheLock) _cache = null;
    }

    internal static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var value = input.Trim();
        if (!value.StartsWith('/')) value = '/' + value;
        while (value.StartsWith("//", StringComparison.Ordinal)) value = value[1..];
        if (value.Length > 1) value = value.TrimEnd('/');
        return value.ToLowerInvariant();
    }

    private static bool IsValidDestination(string value)
    {
        if (value.StartsWith('/')) return true;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
