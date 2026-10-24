using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace Esatto.Umbraco.Backoffice.NavigationOrder;

public interface INavigationOrderStore
{
    /// <summary>The saved order; <see cref="StoredNavigationOrder.Empty"/> when nothing is saved.</summary>
    StoredNavigationOrder Get();

    /// <summary>Replaces the whole saved order with <paramref name="settings"/>.</summary>
    StoredNavigationOrder Save(NavigationOrderSettings settings, string? updatedBy);
}

/// <summary>
/// Reads straight from the database on every call: it's a handful of rows, read once per backoffice
/// load, and no cache means no stale order on the other servers of a load-balanced site.
/// </summary>
public sealed class NavigationOrderStore : INavigationOrderStore
{
    private readonly IScopeProvider _scopes;
    private readonly TimeProvider _time;

    public NavigationOrderStore(IScopeProvider scopes, TimeProvider time)
    {
        _scopes = scopes;
        _time = time;
    }

    public StoredNavigationOrder Get()
    {
        using IScope scope = _scopes.CreateScope(autoComplete: true);
        List<NavigationOrderEntity> rows = scope.Database.Fetch<NavigationOrderEntity>(
            scope.SqlContext.Sql().SelectAll().From<NavigationOrderEntity>());
        return NavigationOrderRows.ToStored(rows ?? []);
    }

    public StoredNavigationOrder Save(NavigationOrderSettings settings, string? updatedBy)
    {
        IReadOnlyList<NavigationOrderEntity> rows = NavigationOrderRows.ToEntities(settings, _time.GetUtcNow().UtcDateTime, updatedBy);

        using IScope scope = _scopes.CreateScope();
        scope.Database.Execute(scope.SqlContext.Sql().Delete<NavigationOrderEntity>());
        foreach (NavigationOrderEntity row in rows) scope.Database.Insert(row);
        scope.Complete();

        return NavigationOrderRows.ToStored(rows);
    }
}
