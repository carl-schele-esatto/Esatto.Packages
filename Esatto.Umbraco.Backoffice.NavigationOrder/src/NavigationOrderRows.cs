using System.Text.Json;

namespace Esatto.Umbraco.Backoffice.NavigationOrder;

/// <summary>Maps the saved order to and from its <see cref="NavigationOrderEntity"/> rows.</summary>
public static class NavigationOrderRows
{
    public const string SectionsKey = "sections";
    public const string TabsKeyPrefix = "tabs:";

    /// <summary>The order the rows hold. A row with an unknown key or unreadable aliases is skipped.</summary>
    public static StoredNavigationOrder ToStored(IEnumerable<NavigationOrderEntity> rows)
    {
        IReadOnlyList<string>? sections = null;
        var tabs = new List<TabOrder>();
        NavigationOrderEntity? latest = null;

        foreach (NavigationOrderEntity row in rows)
        {
            if (ReadAliases(row.AliasesJson) is not { } aliases) continue;

            if (row.ListKey == SectionsKey)
            {
                sections = aliases;
            }
            else if (row.ListKey.StartsWith(TabsKeyPrefix, StringComparison.Ordinal) && row.ListKey.Length > TabsKeyPrefix.Length)
            {
                tabs.Add(new TabOrder(row.ListKey[TabsKeyPrefix.Length..], aliases));
            }
            else
            {
                continue;
            }

            if (latest is null || row.UpdatedUtc > latest.UpdatedUtc) latest = row;
        }

        return latest is null
            ? StoredNavigationOrder.Empty
            : new StoredNavigationOrder(
                new NavigationOrderSettings(sections, tabs),
                DateTime.SpecifyKind(latest.UpdatedUtc, DateTimeKind.Utc),
                latest.UpdatedBy);
    }

    /// <summary>One row per saved list; none for sections in the default order.</summary>
    public static IReadOnlyList<NavigationOrderEntity> ToEntities(NavigationOrderSettings settings, DateTime updatedUtc, string? updatedBy)
    {
        var rows = new List<NavigationOrderEntity>();
        if (settings.Sections is { } sections) rows.Add(Row(SectionsKey, sections));
        rows.AddRange(settings.Tabs.Select(tab => Row(TabsKeyPrefix + tab.Section, tab.Aliases)));
        return rows;

        NavigationOrderEntity Row(string key, IReadOnlyList<string> aliases) => new()
        {
            ListKey = key,
            AliasesJson = JsonSerializer.Serialize(aliases),
            UpdatedUtc = updatedUtc,
            UpdatedBy = updatedBy,
        };
    }

    private static string[]? ReadAliases(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
