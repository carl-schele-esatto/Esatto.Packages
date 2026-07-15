using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Xunit;

namespace Esatto.Umbraco.Backoffice.DictionaryLocalization.Tests;

public class DictionaryLocalizationCacheTests
{
    private static IDictionaryTranslation Translation(string iso, string value)
    {
        var t = Substitute.For<IDictionaryTranslation>();
        t.LanguageIsoCode.Returns(iso);
        t.Value.Returns(value);
        return t;
    }

    private static IDictionaryItem Item(string key, params IDictionaryTranslation[] translations)
    {
        var item = Substitute.For<IDictionaryItem>();
        item.Key.Returns(Guid.NewGuid());
        item.ItemKey.Returns(key);
        item.Translations.Returns(translations);
        return item;
    }

    private static IDictionaryItemService ServiceReturning(params IDictionaryItem[] items)
    {
        var svc = Substitute.For<IDictionaryItemService>();
        svc.GetDescendantsAsync(Arg.Any<Guid?>(), Arg.Any<string?>())
            .Returns(Task.FromResult<IEnumerable<IDictionaryItem>>(items));
        return svc;
    }

    [Fact]
    public async Task Flattens_translations_by_iso_and_drops_empty_values()
    {
        var svc = ServiceReturning(
            Item("TestTag", Translation("sv-SE", "Testtag"), Translation("en", string.Empty), Translation("da", "Testtag")));
        var cache = new DictionaryLocalizationCache(svc);

        var all = await cache.GetAllAsync();

        var entry = Assert.Single(all);
        Assert.Equal("TestTag", entry.Key);
        Assert.Equal(2, entry.ValuesByIso.Count); // en dropped for empty value
        Assert.Equal("Testtag", entry.ValuesByIso["sv-SE"]);
        Assert.Equal("Testtag", entry.ValuesByIso["da"]);
        Assert.False(entry.ValuesByIso.ContainsKey("en"));
    }

    [Fact]
    public async Task Builds_once_then_serves_from_cache()
    {
        var svc = ServiceReturning(Item("Foo", Translation("en", "Foo")));
        var cache = new DictionaryLocalizationCache(svc);

        await cache.GetAllAsync();
        await cache.GetAllAsync();

        await svc.Received(1).GetDescendantsAsync(Arg.Any<Guid?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task Invalidate_forces_a_rebuild_on_next_access()
    {
        var svc = ServiceReturning(Item("Foo", Translation("en", "Foo")));
        var cache = new DictionaryLocalizationCache(svc);

        await cache.GetAllAsync();
        cache.Invalidate();
        await cache.GetAllAsync();

        await svc.Received(2).GetDescendantsAsync(Arg.Any<Guid?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task Multiple_items_return_multiple_entries()
    {
        var svc = ServiceReturning(
            Item("A", Translation("en", "A-value")),
            Item("B", Translation("en", "B-value")));
        var cache = new DictionaryLocalizationCache(svc);

        var all = await cache.GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, e => e.Key == "A");
        Assert.Contains(all, e => e.Key == "B");
    }
}
