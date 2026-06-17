using NPoco;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Esatto.Umbraco.Backoffice.Redirects;

public sealed class RedirectService : IRedirectService
{
    private readonly IScopeProvider _scopeProvider;

    private volatile Dictionary<string, string>? _cache; // oldPath → newUrl
    private readonly object _cacheLock = new();

    public RedirectService(IScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    public Task<IReadOnlyList<RedirectDto>> GetAllAsync()
    {
        using var scope = _scopeProvider.CreateScope(autoComplete: true);
        var rows = scope.Database
            .Query<RedirectEntity>()
            .OrderBy(r => r.OldPath)
            .ToList();

        IReadOnlyList<RedirectDto> result = rows
            .Select(r => new RedirectDto(r.Id, r.OldPath, r.NewUrl))
            .ToList();
        return Task.FromResult(result);
    }

    public Task<string?> LookupAsync(string path)
    {
        var key = Normalize(path);
        if (string.IsNullOrEmpty(key)) return Task.FromResult<string?>(null);

        var cache = GetOrLoadCache();
        return Task.FromResult(cache.TryGetValue(key, out var target) ? target : null);
    }

    public Task<string?> TryCreateAsync(CreateRedirectRequest request)
    {
        var oldPath = Normalize(request.OldPath);
        var newUrl = (request.NewUrl ?? string.Empty).Trim();

        var validationError = Validate(oldPath, newUrl);
        if (validationError is not null) return Task.FromResult<string?>(validationError);

        InvalidateCache();
        using (var scope = _scopeProvider.CreateScope())
        {
            var exists = scope.Database.ExecuteScalar<int>(
                $"SELECT COUNT(*) FROM {RedirectEntity.TableName} WHERE oldPath = @0", oldPath) > 0;
            if (exists)
            {
                scope.Complete();
                return Task.FromResult<string?>("A redirect for that Old URL already exists.");
            }

            var now = DateTime.UtcNow;
            scope.Database.Insert(new RedirectEntity
            {
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
        var oldPath = Normalize(request.OldPath);
        var newUrl = (request.NewUrl ?? string.Empty).Trim();

        var validationError = Validate(oldPath, newUrl);
        if (validationError is not null) return Task.FromResult<string?>(validationError);

        using (var scope = _scopeProvider.CreateScope())
        {
            var existing = scope.Database.SingleOrDefaultById<RedirectEntity>(id);
            if (existing is null)
            {
                scope.Complete();
                return Task.FromResult<string?>("Redirect not found.");
            }

            if (!string.Equals(existing.OldPath, oldPath, StringComparison.OrdinalIgnoreCase))
            {
                var conflict = scope.Database.ExecuteScalar<int>(
                    $"SELECT COUNT(*) FROM {RedirectEntity.TableName} WHERE oldPath = @0 AND id <> @1",
                    oldPath, id) > 0;
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

    private Dictionary<string, string> GetOrLoadCache()
    {
        if (_cache is not null) return _cache;
        lock (_cacheLock)
        {
            if (_cache is not null) return _cache;

            using var scope = _scopeProvider.CreateScope(autoComplete: true);
            var rows = scope.Database.Query<RedirectEntity>().ToList();

            var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.NewUrl)) continue; // drafts excluded
                cache[row.OldPath] = row.NewUrl;
            }

            _cache = cache;
            return _cache;
        }
    }

    private void InvalidateCache()
    {
        lock (_cacheLock) _cache = null;
    }

    // Shared create/update validation (was duplicated inline in both methods — DRY).
    // Returns an error message, or null when valid.
    private static string? Validate(string oldPath, string newUrl)
    {
        if (string.IsNullOrEmpty(oldPath)) return "Old URL is required.";
        if (!oldPath.StartsWith('/')) return "Old URL must start with '/'.";
        if (!string.IsNullOrEmpty(newUrl) && !IsValidDestination(newUrl))
            return "New URL must be a relative path (/...) or absolute URL.";
        if (!string.IsNullOrEmpty(newUrl) && string.Equals(oldPath, newUrl, StringComparison.OrdinalIgnoreCase))
            return "Old URL and New URL cannot be the same.";
        return null;
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
