using Xunit;

namespace Esatto.Umbraco.Backoffice.NavigationOrder.Tests;

public class NavigationOrderRowsTests
{
    private static readonly DateTime Saved = new(2026, 10, 24, 8, 0, 0, DateTimeKind.Utc);

    private static readonly NavigationOrderSettings Full = new(
        ["Umb.Section.Forms", "Umb.Section.Content"],
        [new TabOrder("Umb.Section.Settings", ["Esatto.Umbraco.Backoffice.ScheduledTasks.Dashboard", "Umb.Dashboard.Welcome"])]);

    private static NavigationOrderEntity Row(string key, string json, DateTime? updated = null, string? by = null) => new()
    {
        ListKey = key,
        AliasesJson = json,
        UpdatedUtc = updated ?? Saved,
        UpdatedBy = by,
    };

    [Fact]
    public void Rows_round_trip()
    {
        var rows = NavigationOrderRows.ToEntities(Full, Saved, "Carl");

        Assert.Equal(["sections", "tabs:Umb.Section.Settings"], rows.Select(r => r.ListKey));
        var stored = NavigationOrderRows.ToStored(rows);
        Assert.Equal(Full.Sections, stored.Settings.Sections);
        var tab = Assert.Single(stored.Settings.Tabs);
        Assert.Equal("Umb.Section.Settings", tab.Section);
        Assert.Equal(Full.Tabs[0].Aliases, tab.Aliases);
        Assert.Equal(Saved, stored.UpdatedUtc);
        Assert.Equal("Carl", stored.UpdatedBy);
    }

    [Fact]
    public void Default_sections_write_no_row()
    {
        var rows = NavigationOrderRows.ToEntities(new(null, Full.Tabs), Saved, null);

        Assert.Equal("tabs:Umb.Section.Settings", Assert.Single(rows).ListKey);
        Assert.Null(NavigationOrderRows.ToStored(rows).Settings.Sections);
    }

    [Fact]
    public void Unreadable_rows_are_skipped()
    {
        var stored = NavigationOrderRows.ToStored(
        [
            Row("tabs:Umb.Section.Settings", "not json"),
            Row("other", """["A"]"""),
            Row("tabs:", """["B"]"""),
            Row("tabs:Umb.Section.Media", "null"),
            Row("sections", """["Umb.Section.Forms"]"""),
        ]);

        Assert.Equal(["Umb.Section.Forms"], stored.Settings.Sections);
        Assert.Empty(stored.Settings.Tabs);
    }

    [Fact]
    public void Latest_row_names_who_saved()
    {
        var stored = NavigationOrderRows.ToStored(
        [
            Row("sections", """["A"]""", Saved, "Anna"),
            Row("tabs:S", """["B"]""", Saved.AddMinutes(5), "Carl"),
        ]);

        Assert.Equal(Saved.AddMinutes(5), stored.UpdatedUtc);
        Assert.Equal(DateTimeKind.Utc, stored.UpdatedUtc!.Value.Kind);
        Assert.Equal("Carl", stored.UpdatedBy);
    }

    [Fact]
    public void No_rows_is_the_default_order() => Assert.Equal(StoredNavigationOrder.Empty, NavigationOrderRows.ToStored([]));
}
