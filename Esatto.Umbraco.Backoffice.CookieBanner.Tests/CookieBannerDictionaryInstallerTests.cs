using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Esatto.Umbraco.Backoffice.CookieBanner;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class CookieBannerDictionaryInstallerTests
{
    private static (CookieBannerDictionaryInstaller Installer,
                    IDictionaryItemService Items,
                    List<IDictionaryItem> Created)
        CreateSut(params ILanguage[] siteLanguages)
    {
        var languages = Substitute.For<ILanguageService>();
        languages.GetAllAsync().Returns(siteLanguages.AsEnumerable());

        var items = Substitute.For<IDictionaryItemService>();

        // NSubstitute auto-substitutes a non-null recursive mock for an unconfigured call whose
        // return type is Task<TInterface>, rather than returning null. Without this baseline every
        // GetAsync(key) in the loop would come back as a bogus non-null IDictionaryItem, so every
        // item would look "existing" and take the adopt path instead of the create path. A test's
        // explicit GetAsync(key).Returns(existing) below still wins for that one key, because
        // NSubstitute matches the most specific/most-recently-configured call.
        items.GetAsync(Arg.Any<string>()).Returns((IDictionaryItem?)null);

        var created = new List<IDictionaryItem>();
        items
            .CreateAsync(Arg.Any<IDictionaryItem>(), Arg.Any<Guid>())
            .Returns(call =>
            {
                IDictionaryItem item = call.Arg<IDictionaryItem>();
                created.Add(item);
                return Attempt.SucceedWithStatus<IDictionaryItem, DictionaryItemOperationStatus>(
                    DictionaryItemOperationStatus.Success, item);
            });

        var installer = new CookieBannerDictionaryInstaller(
            items, languages, NullLogger<CookieBannerDictionaryInstaller>.Instance);

        return (installer, items, created);
    }

    private static IEnumerable<string> SeededKeys(IEnumerable<IDictionaryItem> created)
        => created
            .Select(item => item.ItemKey)
            .Where(key => key != CookieBannerDictionaryInstaller.ParentKey);

    [Fact]
    public async Task Seeds_nothing_and_does_not_throw_when_the_site_has_no_shipped_language()
    {
        // Pins the fix for NDSTK's unshippable hard abort: the old installer demanded a 'sv'
        // language (which only existed because NdstkLanguageInstaller forced it in) and bailed
        // out of ALL seeding when it was missing. A package must never require a language, and
        // must never throw on a site whose languages it ships no text for.
        var (installer, items, created) = CreateSut(new Language("de-DE", "German"));

        await installer.InstallAsync();

        Assert.Empty(created);
        _ = items.DidNotReceiveWithAnyArgs().CreateAsync(null!, default);
    }

    [Fact]
    public async Task Seeds_English_only_for_an_English_only_site()
    {
        // Pins culture-agnostic seeding: an en-GB-only site gets all 32 keys with exactly one
        // translation each, and no 'sv' language is created to hang the Swedish text off.
        var (installer, _, created) = CreateSut(new Language("en-GB", "English (United Kingdom)"));

        await installer.InstallAsync();

        Assert.Equal(32, SeededKeys(created).Count());
        Assert.Contains(CookieBannerDictionaryInstaller.ParentKey, created.Select(item => item.ItemKey));

        IDictionaryItem heading = created.Single(item => item.ItemKey == "Cookies.Banner.Heading");
        IDictionaryTranslation translation = Assert.Single(heading.Translations);
        Assert.Equal("en-GB", translation.LanguageIsoCode);
        Assert.Equal("We use cookies", translation.Value);
    }

    [Fact]
    public async Task Seeds_both_languages_for_a_Swedish_and_English_site()
    {
        // Pins that matching is by the two-letter language part, so sv-SE and en-US match the
        // shipped 'sv'/'en' text sets, and pins the new Cookies.Policy.On/Off keys that replace
        // the hardcoded "på"/"av" literals on the policy page.
        var (installer, _, created) = CreateSut(
            new Language("sv-SE", "Swedish (Sweden)"),
            new Language("en-US", "English (United States)"));

        await installer.InstallAsync();

        Assert.Equal(32, SeededKeys(created).Count());
        Assert.Contains("Cookies.Policy.On", SeededKeys(created));
        Assert.Contains("Cookies.Policy.Off", SeededKeys(created));

        IDictionaryItem heading = created.Single(item => item.ItemKey == "Cookies.Banner.Heading");
        Assert.Equal(2, heading.Translations.Count());
        Assert.Equal(
            "Vi använder kakor",
            heading.Translations.Single(t => t.LanguageIsoCode == "sv-SE").Value);
        Assert.Equal(
            "We use cookies",
            heading.Translations.Single(t => t.LanguageIsoCode == "en-US").Value);
    }

    [Fact]
    public async Task Skips_an_existing_key_and_leaves_an_item_an_editor_moved_where_it_is()
    {
        // Pins create-if-missing only (a re-boot must not overwrite an editor's reworded copy)
        // and pins the TryAdopt guard: an item whose ParentId is already set was deliberately
        // filed somewhere, so the seeder must not re-parent it under Cookie.Banner.
        var existing = Substitute.For<IDictionaryItem>();
        existing.ItemKey.Returns("Cookies.Banner.Heading");
        existing.ParentId.Returns((Guid?)Guid.NewGuid());

        var (installer, items, created) = CreateSut(new Language("en-GB", "English (United Kingdom)"));
        items.GetAsync("Cookies.Banner.Heading").Returns(existing);

        await installer.InstallAsync();

        Assert.DoesNotContain("Cookies.Banner.Heading", SeededKeys(created));
        Assert.Equal(31, SeededKeys(created).Count());
        _ = items.DidNotReceiveWithAnyArgs().MoveAsync(null!, null, default);
    }
}
