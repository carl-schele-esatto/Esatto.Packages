using System.Text.RegularExpressions;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// Brand terms are typed as a list and turned into the regular expression Search Console filters on. The pattern must match
/// the terms literally (a dot is a dot) and stay inside the syntax Google's RE2 engine accepts.
/// </summary>
public class BrandPatternTests
{
    [Fact]
    public void Terms_become_a_case_insensitive_alternation()
        => Assert.Equal("(?i)(acme|acme ab)", BrandPattern.Build(["acme", "acme ab"]));

    [Fact]
    public void Regular_expression_characters_are_escaped()
        => Assert.Equal("(?i)(a\\.b)", BrandPattern.Build(["a.b"]));

    [Fact]
    public void Blank_terms_and_duplicates_are_dropped_and_the_rest_is_trimmed()
        => Assert.Equal("(?i)(acme|other)", BrandPattern.Build(["acme", "  ", "", " acme ", "ACME", "other"]));

    [Fact]
    public void A_null_term_is_dropped_too()
        => Assert.Equal("(?i)(acme)", BrandPattern.Build(["acme", null!]));

    [Fact]
    public void The_first_spelling_of_a_duplicate_is_kept_and_the_order_is_the_input_order()
        => Assert.Equal("(?i)(Zed|Acme)", BrandPattern.Build(["Zed", "Acme", "zed", "acme"]));

    [Fact]
    public void No_terms_gives_no_pattern()
    {
        Assert.Null(BrandPattern.Build([]));
        Assert.Null(BrandPattern.Build(["", "   "]));
    }

    [Fact]
    public void A_space_is_written_as_a_plain_space_because_RE2_has_no_escape_for_it()
        => Assert.DoesNotContain('\\', BrandPattern.Build(["acme ab"])!);

    public static TheoryData<string> Awkward_terms() => new()
    {
        "c++", "(x)", "a|b", "[x]", "{1}", "^$", "a\\b", "a\\ b", "a.b", "100%", "#1", "a*", "what?", "a\\1", "c:\\2024",
    };

    [Fact]
    public void White_space_inside_a_term_is_one_space_because_Search_Console_queries_are_single_spaced()
    {
        var pattern = BrandPattern.Build(["tab\there", "acme   ab"])!;

        Assert.Equal("(?i)(tab here|acme ab)", pattern);
        Assert.Matches(new Regex(pattern), "buy acme ab online");
    }

    // ---- The length of the pattern -----------------------------------------------------------------------------------
    // Google refuses an over-long expression, and then every month fails. The pattern is kept within 4,000 characters (Search
    // Console's own filter takes 4,096) by leaving out the LAST terms that do not fit: the order is the person's priority.

    private static string Term(int i, int length) => $"t{i:D2}" + new string('x', length - 3);

    [Fact]
    public void A_pattern_of_exactly_4000_characters_keeps_every_term()
    {
        // "(?i)(" + terms joined by "|" + ")": 5 + 39 x 99 + 94 + 40 = 4000.
        var terms = Enumerable.Range(0, 39).Select(i => Term(i, 99)).Append(Term(39, 94)).ToArray();

        var pattern = BrandPattern.Build(terms)!;

        Assert.Equal(4000, pattern.Length);
        Assert.Equal(4000, BrandPattern.MaxLength);
        Assert.EndsWith(Term(39, 94) + ")", pattern);
    }

    [Fact]
    public void A_term_that_would_take_the_pattern_past_4000_characters_is_left_out_with_every_term_after_it()
    {
        var terms = Enumerable.Range(0, 39).Select(i => Term(i, 99)).Append(Term(39, 95)).Append("z").ToArray();

        var pattern = BrandPattern.Build(terms)!;

        Assert.Equal($"(?i)({string.Join('|', terms[..39])})", pattern);
    }

    [Fact]
    public void Fifty_terms_of_100_characters_that_all_need_escaping_still_give_a_usable_pattern_within_the_limit()
    {
        var terms = Enumerable.Range(0, 50).Select(i => $"{i:D2}" + new string('.', 98)).ToArray();

        var pattern = BrandPattern.Build(terms)!;

        Assert.InRange(pattern.Length, 3000, BrandPattern.MaxLength);
        var regex = new Regex(pattern);
        Assert.Matches(regex, terms[0]);
        Assert.DoesNotMatch(regex, terms[^1]);
        GscAgent.ValidateBrandRegex(pattern);
    }

    [Fact]
    public void Building_twice_from_the_stored_terms_gives_the_same_pattern()
    {
        var terms = Enumerable.Range(0, 60).Select(i => " " + Term(i, 99) + " ").ToArray();
        var stored = SettingsNormalizer.NormalizeList(terms);

        Assert.Equal(BrandPattern.Build(terms), BrandPattern.Build(stored));
    }

    [Theory]
    [MemberData(nameof(Awkward_terms))]
    public void Every_term_matches_itself_and_only_itself(string term)
    {
        var pattern = BrandPattern.Build([term])!;

        var regex = new Regex(pattern);

        Assert.True(regex.IsMatch($"I like {term} a lot"), pattern);
        Assert.Matches(regex, term.ToUpperInvariant());
        Assert.DoesNotMatch(regex, "nothing to see here");
    }

    [Fact]
    public void Alternation_characters_in_a_term_do_not_split_it()
    {
        var regex = new Regex(BrandPattern.Build(["a|b"])!);

        Assert.DoesNotMatch(regex, "a");
        Assert.DoesNotMatch(regex, "b");
        Assert.Matches(regex, "a|b");
    }

    [Fact]
    public void The_pattern_is_case_insensitive()
        => Assert.Matches(new Regex(BrandPattern.Build(["acme"])!), "ACME Corp");

    [Fact]
    public void A_dot_in_a_term_is_not_a_wildcard()
        => Assert.DoesNotMatch(new Regex(BrandPattern.Build(["a.b"])!), "axb");
}
