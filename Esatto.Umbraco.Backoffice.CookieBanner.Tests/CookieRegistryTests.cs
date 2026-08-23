using System.Linq;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class CookieRegistryTests
{
    private static CookieDeclaration Declaration(
        string name,
        ConsentCategory category = ConsentCategory.Statistics) =>
        new(name, "Example Inc", category, "Measures use of the site", "1 year", "Cookie");

    [Fact]
    public void Groups_every_declaration_under_its_own_category()
    {
        // Pins the single grouping behaviour that replaces the two hand-written copies in
        // _ConsentBanner.cshtml and CookiePolicy.cshtml, whose comments each claimed to match the other.
        var grouped = CookieRegistry.Group(
        [
            Declaration("_ga", ConsentCategory.Statistics),
            Declaration("_gcl_au", ConsentCategory.Marketing),
            Declaration("cookie-consent", ConsentCategory.Necessary),
        ]);

        Assert.Equal(new[] { "cookie-consent" }, grouped[ConsentCategory.Necessary].Select(d => d.Name).ToArray());
        Assert.Equal(new[] { "_ga" }, grouped[ConsentCategory.Statistics].Select(d => d.Name).ToArray());
        Assert.Equal(new[] { "_gcl_au" }, grouped[ConsentCategory.Marketing].Select(d => d.Name).ToArray());
    }

    [Fact]
    public void Drops_declarations_with_a_blank_name()
    {
        // The regression this whole type exists for: the banner dropped blank cookieName blocks, the
        // policy page rendered them as an empty <code> cell. The contract settles it as "drop".
        var grouped = CookieRegistry.Group(
        [
            Declaration(string.Empty),
            Declaration("   "),
            Declaration("_ga"),
        ]);

        Assert.Equal(new[] { "_ga" }, grouped[ConsentCategory.Statistics].Select(d => d.Name).ToArray());
    }

    [Fact]
    public void Drops_a_declaration_whose_category_is_outside_the_known_set()
    {
        // An unparsable category is dropped upstream by the mapper, but an out-of-range enum value
        // must not reach a bucket lookup and throw KeyNotFoundException mid-render either.
        var grouped = CookieRegistry.Group([Declaration("_mystery", (ConsentCategory)99)]);

        Assert.All(grouped.Values, declarations => Assert.Empty(declarations));
    }

    [Fact]
    public void Returns_an_empty_list_for_a_category_with_no_declarations()
    {
        // Both views index this dictionary by category unconditionally, so every category must exist
        // as a key with an empty list rather than be absent.
        var grouped = CookieRegistry.Group([Declaration("_ga", ConsentCategory.Statistics)]);

        Assert.Empty(grouped[ConsentCategory.Necessary]);
        Assert.Empty(grouped[ConsentCategory.Preferences]);
        Assert.Empty(grouped[ConsentCategory.Marketing]);
    }

    [Fact]
    public void Yields_one_bucket_per_category_for_an_empty_sequence()
    {
        // A site with no published policy page hands in nothing; the banner still renders four
        // fieldsets, so four buckets must come back.
        var grouped = CookieRegistry.Group([]);

        Assert.Equal(4, grouped.Count);
        Assert.All(grouped.Values, declarations => Assert.Empty(declarations));
    }

    [Fact]
    public void Enumerates_categories_in_ConsentCategories_All_order()
    {
        // Display order is necessary-first and is read straight off this dictionary's key order.
        var grouped = CookieRegistry.Group([Declaration("_ga")]);

        Assert.Equal(ConsentCategories.All.ToArray(), grouped.Keys.ToArray());
    }

    [Fact]
    public void Preserves_editor_ordering_within_a_category()
    {
        // Editors sort the Block List to control the table order; grouping must not reshuffle it.
        var grouped = CookieRegistry.Group(
        [
            Declaration("_ga"),
            Declaration("_gid"),
            Declaration("_ga_ABC"),
        ]);

        Assert.Equal(
            new[] { "_ga", "_gid", "_ga_ABC" },
            grouped[ConsentCategory.Statistics].Select(d => d.Name).ToArray());
    }
}
