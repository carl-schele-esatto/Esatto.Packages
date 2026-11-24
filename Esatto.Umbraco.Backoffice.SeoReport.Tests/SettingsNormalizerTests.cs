using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The two property identifiers a person types into Settings. A malformed one must come back as null (and so be refused with
/// a code), never as something empty or half-formed that would be stored and fail later at Google.
/// </summary>
public class SettingsNormalizerTests
{
    // ---- GA4 property id -------------------------------------------------------------------------------------

    [Theory]
    [InlineData("123456789", "properties/123456789")]
    [InlineData(" Properties/123456789 ", "properties/123456789")]
    [InlineData("PROPERTIES/1", "properties/1")]
    [InlineData("properties/42", "properties/42")]
    [InlineData("\t42\r\n", "properties/42")]
    [InlineData("12345678901234567890", "properties/12345678901234567890")]
    public void A_GA4_property_id_becomes_properties_slash_digits(string input, string expected)
        => Assert.Equal(expected, SettingsNormalizer.NormalizeGa4(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("properties/")]
    [InlineData("properties")]
    [InlineData("properties/12a")]
    [InlineData("properties/properties/1")]
    [InlineData("properties/123/")]
    [InlineData("properties /123")]
    [InlineData("properties\\123")]
    [InlineData("123 456")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1.5")]
    [InlineData("1e3")]
    [InlineData("\uFF11\uFF12\uFF13")] // full-width digits are not ASCII digits
    [InlineData("123456789012345678901")] // 21 digits: not a property id
    public void Anything_else_is_not_a_GA4_property_id(string? input)
        => Assert.Null(SettingsNormalizer.NormalizeGa4(input));

    // ---- Search Console property -----------------------------------------------------------------------------

    [Theory]
    [InlineData("example.com", "sc-domain:example.com")]
    [InlineData("EXAMPLE.com/", "sc-domain:example.com")]
    [InlineData("sc-domain:Example.com", "sc-domain:example.com")]
    [InlineData("SC-DOMAIN:Example.COM/", "sc-domain:example.com")]
    [InlineData("  example.com  ", "sc-domain:example.com")]
    [InlineData("www.example.com", "sc-domain:www.example.com")]
    [InlineData("sub.example.co.uk", "sc-domain:sub.example.co.uk")]
    [InlineData("my-site.example.com", "sc-domain:my-site.example.com")]
    [InlineData("B\u00DCCHER.se", "sc-domain:b\u00FCcher.se")]
    public void A_bare_host_is_a_domain_property(string input, string expected)
        => Assert.Equal(expected, SettingsNormalizer.NormalizeSearchConsole(input));

    [Theory]
    [InlineData("https://www.example.com", "https://www.example.com/")]
    [InlineData("HTTP://Example.com/Path", "http://example.com/Path/")]
    [InlineData("HTTPS://EXAMPLE.COM/Some/Path/", "https://example.com/Some/Path/")]
    [InlineData("https://example.com/a/b", "https://example.com/a/b/")]
    [InlineData("  https://example.com/a  ", "https://example.com/a/")]
    [InlineData("https://example.com:8443", "https://example.com:8443/")]
    [InlineData("https://example.com:443/", "https://example.com/")]
    [InlineData("http://example.com:80", "http://example.com/")]
    [InlineData("https://example.com/a%20b", "https://example.com/a%20b/")]
    [InlineData("http://203.0.113.5:8080", "http://203.0.113.5:8080/")]
    [InlineData("http://[2001:db8::1]/x", "http://[2001:db8::1]/x/")]
    public void A_URL_is_a_URL_prefix_property_with_its_scheme_and_host_in_lower_case_and_a_closing_slash(string input, string expected)
        => Assert.Equal(expected, SettingsNormalizer.NormalizeSearchConsole(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ftp://x")]
    [InlineData("ftp://example.com/")]
    [InlineData("has spaces.com")]
    [InlineData("example.com/path")]          // no scheme and a path: refused, not guessed
    [InlineData("//example.com")]
    [InlineData("example")]                    // no dot: not a domain
    [InlineData("example.com:8080")]
    [InlineData("exa_mple.com")]
    [InlineData(".example.com")]
    [InlineData("example..com")]
    [InlineData("example.com.")]
    [InlineData("-example.com")]
    [InlineData("example-.com")]
    [InlineData("sc-domain:")]
    [InlineData("sc-domain:/")]
    [InlineData("sc-domain:   ")]
    [InlineData("sc-domain:example.com/path")]
    [InlineData("sc-domain:exa mple.com")]
    [InlineData("sc-domain:sc-domain:example.com")]
    [InlineData("sc-domain:https://example.com")]
    [InlineData("http://localhost:5000")]      // Search Console can only verify a public name
    [InlineData("https://example/")]
    [InlineData("https://")]
    [InlineData("https:///path")]
    [InlineData("http://")]
    [InlineData("https://https://example.com")]
    [InlineData("https://example.com?x=1")]
    [InlineData("https://example.com/#top")]
    [InlineData("https://user:pw@example.com/")]
    [InlineData("https://exa mple.com/")]
    [InlineData("https://example.com/a b")]
    [InlineData("mailto:a@example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///c:/x")]
    public void Anything_else_is_not_a_Search_Console_property(string? input)
        => Assert.Null(SettingsNormalizer.NormalizeSearchConsole(input));

    [Fact]
    public void A_label_over_63_characters_or_a_host_over_253_is_not_a_domain()
    {
        Assert.Null(SettingsNormalizer.NormalizeSearchConsole(new string('a', 64) + ".com"));
        Assert.NotNull(SettingsNormalizer.NormalizeSearchConsole(new string('a', 63) + ".com"));

        var longHost = string.Join('.', Enumerable.Repeat(new string('a', 60), 5)) + ".com";
        Assert.True(longHost.Length > 253);
        Assert.Null(SettingsNormalizer.NormalizeSearchConsole(longHost));
    }

    [Fact]
    public void An_absurdly_long_input_is_refused()
        => Assert.Null(SettingsNormalizer.NormalizeSearchConsole("https://example.com/" + new string('a', 3000)));

    [Fact]
    public void The_length_limit_holds_for_what_is_stored_so_a_stored_property_always_reads_back()
    {
        // 2048 characters typed, one closing slash added: 2049 would be stored and then refused when read back.
        var typed = "https://example.com/" + new string('a', 2048 - 20);
        Assert.Equal(2048, typed.Length);
        Assert.Null(SettingsNormalizer.NormalizeSearchConsole(typed));

        var fits = "https://example.com/" + new string('a', 2047 - 20);
        Assert.Equal(typed[..^1] + "/", SettingsNormalizer.NormalizeSearchConsole(fits));
        Assert.Equal(2048, SettingsNormalizer.NormalizeSearchConsole(fits)!.Length);
    }

    // ---- Lists (brand terms, user-group aliases) and the display name --------------------------------------------

    private static string Item(int i, int length) => $"t{i:D2}" + new string('x', length - 3);

    [Fact]
    public void A_list_is_trimmed_with_inner_white_space_collapsed_and_duplicates_dropped_ignoring_case_first_spelling_kept()
        => Assert.Equal(
            ["Acme AB", "Zed Co", "other"],
            SettingsNormalizer.NormalizeList([" Acme  AB ", "acme ab", "", null, "   ", "Zed\tCo", "ZED   co", "other"]));

    [Fact]
    public void A_list_keeps_its_first_50_items()
    {
        var items = Enumerable.Range(0, 60).Select(i => Item(i, 10)).ToArray();

        Assert.Equal(items[..50], SettingsNormalizer.NormalizeList(items));
    }

    [Fact]
    public void An_item_over_100_characters_is_dropped_not_cut_and_does_not_take_a_place()
    {
        var items = new[] { Item(0, 101) }.Concat(Enumerable.Range(1, 50).Select(i => Item(i, 100))).ToArray();

        var kept = SettingsNormalizer.NormalizeList(items);

        Assert.Equal(items[1..], kept);
        Assert.All(kept, item => Assert.Equal(100, item.Length));
    }

    [Fact]
    public void An_item_is_measured_after_its_white_space_is_tidied()
    {
        var spaced = "  " + new string('a', 50) + " \t  " + new string('b', 49) + "   ";

        Assert.Equal([new string('a', 50) + " " + new string('b', 49)], SettingsNormalizer.NormalizeList([spaced]));
    }

    [Fact]
    public void A_capped_list_normalizes_to_itself()
    {
        var messy = Enumerable.Range(0, 70).Select(i => i % 7 == 0 ? Item(i, 150) : "  " + Item(i, 40).ToUpperInvariant() + " ").ToArray();

        var once = SettingsNormalizer.NormalizeList(messy);

        Assert.Equal(50, once.Length);
        Assert.Equal(once, SettingsNormalizer.NormalizeList(once));
    }

    // ---- User-group aliases ------------------------------------------------------------------------------------------
    // An empty list means "everyone with access", so a real alias dropped for being "too long" could widen who sees the findings.
    // Umbraco allows an alias up to 200 characters, so that is the limit here: no alias a person can pick is ever dropped.

    [Fact]
    public void A_group_alias_up_to_200_characters_is_kept_and_a_longer_one_is_dropped()
    {
        var longest = new string('g', SettingsNormalizer.MaxGroupAliasLength);

        Assert.Equal(200, SettingsNormalizer.MaxGroupAliasLength);
        Assert.Equal([Item(0, 150), longest], SettingsNormalizer.NormalizeGroupAliases([Item(0, 150), longest, new string('g', 201)]));
    }

    [Fact]
    public void Group_aliases_are_tidied_and_capped_like_the_other_lists()
    {
        var many = Enumerable.Range(0, 60).Select(i => Item(i, 120)).ToArray();

        Assert.Equal(["Editors", "Admins"], SettingsNormalizer.NormalizeGroupAliases([" Editors ", "editors", "", null, "Admins"]));
        Assert.Equal(many[..50], SettingsNormalizer.NormalizeGroupAliases(many));
        Assert.Equal(SettingsNormalizer.NormalizeGroupAliases(many), SettingsNormalizer.NormalizeGroupAliases(SettingsNormalizer.NormalizeGroupAliases(many)));
    }

    [Fact]
    public void Brand_terms_keep_their_100_character_limit()
        => Assert.Empty(SettingsNormalizer.NormalizeList([Item(0, 150)]));

    [Fact]
    public void A_display_name_is_trimmed_and_cut_to_200_characters()
    {
        Assert.Equal("Acme", SettingsNormalizer.NormalizeDisplayName("  Acme  "));
        Assert.Equal(new string('n', 200), SettingsNormalizer.NormalizeDisplayName(" " + new string('n', 250)));
        Assert.Equal(new string('n', 200), SettingsNormalizer.NormalizeDisplayName(new string('n', 200)));
    }

    [Fact]
    public void A_display_name_is_cut_without_leaving_half_a_character_or_trailing_white_space()
    {
        var emoji = new string('n', 199) + "\U0001F600" + "tail";
        var space = new string('n', 199) + " tail";

        Assert.Equal(new string('n', 199), SettingsNormalizer.NormalizeDisplayName(emoji));
        Assert.Equal(new string('n', 199), SettingsNormalizer.NormalizeDisplayName(space));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_display_name_is_none(string? input)
        => Assert.Null(SettingsNormalizer.NormalizeDisplayName(input));

    [Theory]
    [InlineData("example.com")]
    [InlineData("https://www.example.com/a")]
    [InlineData("sc-domain:Example.com")]
    public void Normalizing_a_Search_Console_property_twice_changes_nothing(string input)
    {
        var once = SettingsNormalizer.NormalizeSearchConsole(input);

        Assert.NotNull(once);
        Assert.Equal(once, SettingsNormalizer.NormalizeSearchConsole(once));
    }

    [Theory]
    [InlineData("123456789")]
    [InlineData("properties/5")]
    public void Normalizing_a_GA4_id_twice_changes_nothing(string input)
    {
        var once = SettingsNormalizer.NormalizeGa4(input);

        Assert.NotNull(once);
        Assert.Equal(once, SettingsNormalizer.NormalizeGa4(once));
    }
}
