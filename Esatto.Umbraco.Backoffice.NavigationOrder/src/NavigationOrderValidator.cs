namespace Esatto.Umbraco.Backoffice.NavigationOrder;

/// <summary>Checks an order before it's saved. Aliases aren't checked against registered extensions: those live in the browser.</summary>
public static class NavigationOrderValidator
{
    public const int MaxAliasLength = 255;
    public const int MaxAliasesPerList = 200;
    public const int MaxTabLists = 100;

    /// <summary>Every problem with <paramref name="settings"/>; empty when it can be saved.</summary>
    public static IReadOnlyList<string> Validate(NavigationOrderSettings settings)
    {
        var errors = new List<string>();

        if (settings.Sections is { } sections) ValidateList(sections, "sections", errors);

        if (settings.Tabs.Count > MaxTabLists) errors.Add($"tabs has more than {MaxTabLists} sections.");
        var seenSections = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < settings.Tabs.Count; i++)
        {
            TabOrder tab = settings.Tabs[i];
            if (ValidateAlias(tab.Section, $"tabs[{i}].section", errors) && !seenSections.Add(tab.Section))
                errors.Add($"Duplicate section '{tab.Section}' in tabs.");
            ValidateList(tab.Aliases, $"tabs[{i}]", errors, itemName: $"tabs[{i}].aliases");
        }

        return errors;
    }

    private static void ValidateList(IReadOnlyList<string> aliases, string listName, List<string> errors, string? itemName = null)
    {
        itemName ??= listName;
        if (aliases.Count > MaxAliasesPerList) errors.Add($"{listName} has more than {MaxAliasesPerList} aliases.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < aliases.Count; i++)
        {
            if (ValidateAlias(aliases[i], $"{itemName}[{i}]", errors) && !seen.Add(aliases[i]))
                errors.Add($"Duplicate alias '{aliases[i]}' in {listName}.");
        }
    }

    private static bool ValidateAlias(string? alias, string name, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            errors.Add($"{name} is blank.");
            return false;
        }
        if (alias.Length > MaxAliasLength)
        {
            errors.Add($"{name} is longer than {MaxAliasLength} characters.");
            return false;
        }
        return true;
    }
}
