using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Cms.Core.Dictionary;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentTextProviderTests
{
    /// <summary>
    /// Stands in for Umbraco's dictionary. The indexer returns <see cref="string.Empty" /> for an
    /// absent key, which is exactly what <c>DefaultCultureDictionary</c> does.
    /// </summary>
    private sealed class StubDictionary : ICultureDictionary, ICultureDictionaryFactory
    {
        private readonly Dictionary<string, string> _items;

        public StubDictionary(CultureInfo culture, params (string Key, string Value)[] items)
        {
            Culture = culture;
            _items = items.ToDictionary(i => i.Key, i => i.Value);
        }

        public string this[string key] => _items.TryGetValue(key, out var value) ? value : string.Empty;

        public CultureInfo Culture { get; }

        public IDictionary<string, string> GetChildren(string key) => new Dictionary<string, string>();

        public ICultureDictionary CreateDictionary() => this;

        public ICultureDictionary CreateDictionary(CultureInfo culture) => this;
    }

    private static ConsentTextProvider Provider(StubDictionary dictionary) =>
        new(dictionary, NullLogger<ConsentTextProvider>.Instance);

    // Pins the resolution order's first rung: an editor's dictionary edit beats the shipped resx,
    // which is the whole reason the dictionary stays the editable source of truth for legal copy.
    [Fact]
    public void A_dictionary_item_wins_over_the_shipped_resx()
    {
        var dictionary = new StubDictionary(
            new CultureInfo("sv-SE"),
            ("Cookies.Banner.AcceptAll", "Ja tack till allt"));

        Assert.Equal("Ja tack till allt", Provider(dictionary).Get("Cookies.Banner.AcceptAll"));
    }

    // Pins the second rung: with no dictionary item the request culture's embedded resx is used,
    // including neutral-parent fallback (sv-SE resolves the sv satellite).
    [Fact]
    public void The_request_cultures_resx_is_used_when_the_dictionary_has_no_item()
    {
        var dictionary = new StubDictionary(new CultureInfo("sv-SE"));

        Assert.Equal("Godkänn alla", Provider(dictionary).Get("Cookies.Banner.AcceptAll"));
    }

    // Pins the third rung: a culture the package ships no resx for falls back to English rather
    // than to the Swedish literals that used to be hardcoded in the .cshtml fallbacks.
    [Fact]
    public void English_is_used_when_the_culture_has_no_resx()
    {
        var dictionary = new StubDictionary(new CultureInfo("de-DE"));

        Assert.Equal("Accept all", Provider(dictionary).Get("Cookies.Banner.AcceptAll"));
    }

    // Pins the bug fix: on/off on the policy page is a real key in both languages - it was
    // hardcoded Swedish at CookiePolicy.cshtml:45 and rendered "på"/"av" even in English.
    [Fact]
    public void The_policy_on_and_off_text_is_translated()
    {
        var swedish = new StubDictionary(new CultureInfo("sv-SE"));
        var german = new StubDictionary(new CultureInfo("de-DE"));

        Assert.Equal("på", Provider(swedish).Get("Cookies.Policy.On"));
        Assert.Equal("av", Provider(swedish).Get("Cookies.Policy.Off"));
        Assert.Equal("on", Provider(german).Get("Cookies.Policy.On"));
        Assert.Equal("off", Provider(german).Get("Cookies.Policy.Off"));
    }

    // Pins that a blank dictionary translation is treated as absent, not as an answer. Umbraco
    // returns "" for a missing item, and returning that rendered empty buttons and paragraphs.
    [Fact]
    public void A_blank_dictionary_translation_falls_through_to_the_resx()
    {
        var dictionary = new StubDictionary(
            new CultureInfo("sv-SE"),
            ("Cookies.Banner.AcceptAll", "   "));

        Assert.Equal("Godkänn alla", Provider(dictionary).Get("Cookies.Banner.AcceptAll"));
    }

    // Pins that lookup is total: an unknown, null or blank key degrades instead of throwing, so a
    // typo in a view can never 500 the page.
    [Fact]
    public void An_unknown_key_returns_the_key_instead_of_throwing()
    {
        ConsentTextProvider provider = Provider(new StubDictionary(new CultureInfo("sv-SE")));

        Assert.Equal("Cookies.Does.Not.Exist", provider.Get("Cookies.Does.Not.Exist"));
        Assert.Equal(string.Empty, provider.Get(null!));
        Assert.Equal(string.Empty, provider.Get("  "));
    }
}
