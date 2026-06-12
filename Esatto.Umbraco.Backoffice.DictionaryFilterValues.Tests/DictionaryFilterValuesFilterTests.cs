using Esatto.Umbraco.Backoffice.DictionaryFilterValues;
using Xunit;

namespace Esatto.Umbraco.Backoffice.DictionaryFilterValues.Tests;

public class DictionaryFilterValuesFilterTests
{
    private static CachedDictionaryItem Item(string name, params (string iso, string value)[] translations)
        => new(
            Guid.NewGuid(),
            ParentId: null,
            Name: name,
            Translations: translations.Select(t => new DictionaryTranslationValue(t.iso, t.value)).ToArray());

    private static readonly IReadOnlyList<CachedDictionaryItem> Sample = new[]
    {
        Item("BlocksPageListBlockShowMore", ("sv", "Visa fler"), ("en", "Show more")),
        Item("GeneralClose", ("sv", "Stäng"), ("en", "Close")),
        Item("GeneralSave", ("sv", "Spara"), ("en", "Save")),
    };

    [Fact]
    public void Empty_filter_returns_all_items()
    {
        var (items, total) = DictionaryFilterValuesFilter.Apply(Sample, null, 0, 100);
        Assert.Equal(3, items.Count);
        Assert.Equal(3, total);
    }

    [Fact]
    public void Matches_on_the_dictionary_key_name()
    {
        var (items, _) = DictionaryFilterValuesFilter.Apply(Sample, "ShowMore", 0, 100);
        Assert.Single(items);
        Assert.Equal("BlocksPageListBlockShowMore", items[0].Name);
    }

    [Fact]
    public void Matches_on_a_translation_value_not_just_the_key()
    {
        // "Visa fler" is a Swedish VALUE; the key is BlocksPageListBlockShowMore. This is the feature.
        var (items, total) = DictionaryFilterValuesFilter.Apply(Sample, "Visa fler", 0, 100);
        Assert.Equal(1, total);
        Assert.Equal("BlocksPageListBlockShowMore", items[0].Name);
    }

    [Fact]
    public void Match_is_case_insensitive()
    {
        var (items, _) = DictionaryFilterValuesFilter.Apply(Sample, "stäng", 0, 100);
        Assert.Single(items);
        Assert.Equal("GeneralClose", items[0].Name);
    }

    [Fact]
    public void Pages_results_but_reports_the_full_match_total()
    {
        var (items, total) = DictionaryFilterValuesFilter.Apply(Sample, null, 1, 1);
        Assert.Single(items);
        Assert.Equal(3, total);
    }

    [Fact]
    public void Projects_the_translated_iso_codes()
    {
        var (items, _) = DictionaryFilterValuesFilter.Apply(Sample, "GeneralSave", 0, 100);
        Assert.Equal(new[] { "sv", "en" }, items[0].TranslatedIsoCodes);
    }
}
