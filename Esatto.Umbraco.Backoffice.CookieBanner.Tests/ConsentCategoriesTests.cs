using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentCategoriesTests
{
    // Pins the wire names against a member rename: the names are baked into every consent cookie
    // already in the wild, so Enum.ToString must never become the source of them.
    [Theory]
    [InlineData(ConsentCategory.Necessary, "necessary")]
    [InlineData(ConsentCategory.Preferences, "preferences")]
    [InlineData(ConsentCategory.Statistics, "statistics")]
    [InlineData(ConsentCategory.Marketing, "marketing")]
    public void Round_trips_every_wire_name(ConsentCategory category, string wireName)
    {
        Assert.Equal(wireName, ConsentCategories.ToWireName(category));

        Assert.True(ConsentCategories.TryParse(wireName, out ConsentCategory parsed));
        Assert.Equal(category, parsed);
    }

    // Pins that parsing is exact and case-sensitive: the codec feeds it hand-editable cookie
    // content, and a lenient parse would let "Marketing" grant marketing on a rejected cookie.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Necessary")]
    [InlineData("MARKETING")]
    [InlineData("Statistics")]
    [InlineData("telepathy")]
    [InlineData(" statistics")]
    public void Rejects_anything_that_is_not_an_exact_wire_name(string? wireName)
    {
        Assert.False(ConsentCategories.TryParse(wireName, out ConsentCategory parsed));
        Assert.Equal(default, parsed);
    }

    // Pins that an out-of-range cast is loud rather than silently written to a cookie as an empty
    // or wrong category name.
    [Fact]
    public void Throws_when_asked_for_the_wire_name_of_an_undefined_value()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => ConsentCategories.ToWireName((ConsentCategory)99));

        Assert.Equal("category", exception.ParamName);
    }

    // Pins policy-page display order (necessary first) and that every declared enum member is
    // listed - a fifth category added to the enum must not silently vanish from the policy page.
    [Fact]
    public void All_lists_every_category_in_policy_page_order()
    {
        Assert.Equal(
            new[]
            {
                ConsentCategory.Necessary,
                ConsentCategory.Preferences,
                ConsentCategory.Statistics,
                ConsentCategory.Marketing,
            },
            ConsentCategories.All);

        Assert.Equal(Enum.GetValues<ConsentCategory>().Length, ConsentCategories.All.Count);
    }

    // Pins banner order and that necessary is never offered as a choice: it is implied, never
    // stored, and a checkbox for it would be a false promise.
    [Fact]
    public void Consentable_lists_the_choosable_categories_in_banner_order()
    {
        Assert.Equal(
            new[]
            {
                ConsentCategory.Preferences,
                ConsentCategory.Statistics,
                ConsentCategory.Marketing,
            },
            ConsentCategories.Consentable);

        Assert.DoesNotContain(ConsentCategory.Necessary, ConsentCategories.Consentable);
    }
}
