using Xunit;

namespace Esatto.Umbraco.Backoffice.NavigationOrder.Tests;

public class NavigationOrderValidatorTests
{
    [Fact]
    public void Empty_settings_are_valid() => Assert.Empty(NavigationOrderValidator.Validate(NavigationOrderSettings.Empty));

    [Fact]
    public void A_full_order_is_valid() => Assert.Empty(NavigationOrderValidator.Validate(new(
        ["Umb.Section.Content", "Umb.Section.Forms"],
        [new TabOrder("Umb.Section.Settings", ["Umb.Dashboard.Welcome", "Esatto.Umbraco.Backoffice.ScheduledTasks.Dashboard"])])));

    [Fact]
    public void Every_problem_is_reported()
    {
        var errors = NavigationOrderValidator.Validate(new(
            ["Umb.Section.Content", " ", "Umb.Section.Content", new string('a', 256)],
            [new TabOrder("Umb.Section.Settings", ["A", "A"]), new TabOrder("Umb.Section.Settings", ["B"]), new TabOrder("", ["C"])]));

        Assert.Contains("sections[1] is blank.", errors);
        Assert.Contains("Duplicate alias 'Umb.Section.Content' in sections.", errors);
        Assert.Contains("sections[3] is longer than 255 characters.", errors);
        Assert.Contains("Duplicate alias 'A' in tabs[0].", errors);
        Assert.Contains("Duplicate section 'Umb.Section.Settings' in tabs.", errors);
        Assert.Contains("tabs[2].section is blank.", errors);
    }

    [Fact]
    public void Lists_and_tab_lists_are_capped()
    {
        var tooMany = Enumerable.Range(0, 201).Select(i => $"A{i}").ToArray();
        var tabLists = Enumerable.Range(0, 101).Select(i => new TabOrder($"S{i}", ["A"])).ToArray();

        var errors = NavigationOrderValidator.Validate(new(tooMany, tabLists));

        Assert.Contains("sections has more than 200 aliases.", errors);
        Assert.Contains("tabs has more than 100 sections.", errors);
    }

    [Fact]
    public void A_null_alias_counts_as_blank()
    {
        var errors = NavigationOrderValidator.Validate(new([null!], [new TabOrder("S", [null!])]));

        Assert.Contains("sections[0] is blank.", errors);
        Assert.Contains("tabs[0].aliases[0] is blank.", errors);
    }
}
