using Esatto.Umbraco.Backoffice.DictionaryFilterValues;
using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Xunit;

namespace Esatto.Umbraco.Backoffice.DictionaryFilterValues.Tests;

public class DictionaryFilterValuesCacheTests
{
    private static IDictionaryTranslation Translation(string iso, string value)
    {
        var t = Substitute.For<IDictionaryTranslation>();
        t.LanguageIsoCode.Returns(iso);
        t.Value.Returns(value);
        return t;
    }

    private static IDictionaryItem Item(Guid id, Guid? parentId, string key, params IDictionaryTranslation[] translations)
    {
        var item = Substitute.For<IDictionaryItem>();
        item.Key.Returns(id);
        item.ParentId.Returns(parentId);
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
    public async Task Maps_id_parent_name_and_drops_empty_translations()
    {
        var id = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var svc = ServiceReturning(
            Item(id, parent, "Foo", Translation("sv", "Hej"), Translation("en", string.Empty)));
        var cache = new DictionaryFilterValuesCache(svc);

        var all = await cache.GetAllAsync();

        var item = Assert.Single(all);
        Assert.Equal(id, item.Id);
        Assert.Equal(parent, item.ParentId);
        Assert.Equal("Foo", item.Name);
        var translation = Assert.Single(item.Translations); // the empty "en" value is dropped
        Assert.Equal("sv", translation.Iso);
        Assert.Equal("Hej", translation.Value);
    }

    [Fact]
    public async Task Builds_once_then_serves_from_cache()
    {
        var svc = ServiceReturning(Item(Guid.NewGuid(), null, "Foo"));
        var cache = new DictionaryFilterValuesCache(svc);

        await cache.GetAllAsync();
        await cache.GetAllAsync();

        await svc.Received(1).GetDescendantsAsync(Arg.Any<Guid?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task Invalidate_forces_a_rebuild_on_next_access()
    {
        var svc = ServiceReturning(Item(Guid.NewGuid(), null, "Foo"));
        var cache = new DictionaryFilterValuesCache(svc);

        await cache.GetAllAsync();
        cache.Invalidate();
        await cache.GetAllAsync();

        await svc.Received(2).GetDescendantsAsync(Arg.Any<Guid?>(), Arg.Any<string?>());
    }
}
