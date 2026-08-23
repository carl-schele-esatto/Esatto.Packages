using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Seeds the consent banner's text as Umbraco Dictionary items.
/// </summary>
/// <remarks>
/// Dictionary items are culture-variant regardless of document type variance, which is what lets
/// the banner be multilingual while the content types stay invariant.
/// <para>
/// Culture-agnostic by design: it enumerates the languages the site actually has and seeds text
/// for any of them the package ships a translation for. It never requires a language, never
/// creates one, never deletes one, and never aborts - a package must not manage a site's language
/// set. A site with no matching language simply gets no dictionary items, and the resx fallback in
/// <c>ConsentTextProvider</c> keeps the banner readable.
/// </para>
/// <para>
/// Every item is filed under a single <see cref="ParentKey" /> node. Umbraco dictionary keys are
/// global rather than path-based, so the nesting is presentation only - it keeps 32 items from
/// sitting loose at the root of the Dictionary tree without changing a single lookup.
/// </para>
/// </remarks>
internal sealed class CookieBannerDictionaryInstaller(
    IDictionaryItemService dictionaryItemService,
    ILanguageService languageService,
    ILogger<CookieBannerDictionaryInstaller> logger)
{
    private static readonly Guid UserKey = Constants.Security.SuperUserKey;

    /// <summary>Parent node for every item below. Holds no translations: it is a folder, not a label.</summary>
    internal const string ParentKey = "Cookie.Banner";

    /// <summary>
    /// Two-letter language codes the package ships text for. A site language matches when its
    /// primary subtag is in here, so en-GB, en-US and a bare en all resolve to the English set.
    /// </summary>
    private static readonly string[] ShippedLanguages = ["en", "sv"];

    /// <summary>
    /// Key, English, Swedish. English first: it is the package's neutral fallback culture, not a
    /// site's default language.
    /// </summary>
    private static readonly (string Key, string En, string Sv)[] Items =
    [
        ("Cookies.Banner.Heading", "We use cookies", "Vi använder kakor"),
        ("Cookies.Banner.Body",
            "We use necessary cookies to make the site work. We would also like to use cookies for statistics and content from other services.",
            "Vi använder nödvändiga kakor för att sajten ska fungera. Vi vill också gärna använda kakor för statistik och innehåll från andra tjänster."),
        ("Cookies.Banner.AcceptAll", "Accept all", "Godkänn alla"),
        ("Cookies.Banner.RejectAll", "Reject all", "Neka alla"),
        ("Cookies.Banner.Customise", "Customise", "Anpassa"),
        ("Cookies.Banner.Save", "Save choices", "Spara val"),
        ("Cookies.Banner.Cancel", "Cancel", "Avbryt"),
        ("Cookies.Banner.Error", "Something went wrong. Please try again.", "Något gick fel. Försök igen."),
        ("Cookies.Banner.RateLimited",
            "You've tried too many times. Please wait a moment and try again.",
            "Du har försökt för många gånger. Vänta en stund och försök igen."),
        ("Cookies.Category.Necessary.Name", "Necessary", "Nödvändiga"),
        ("Cookies.Category.Necessary.Description",
            "Required for the site to work, for example logging in. Cannot be turned off.",
            "Krävs för att sajten ska fungera, till exempel inloggning. Kan inte stängas av."),
        ("Cookies.Category.Preferences.Name", "Preferences", "Funktionella"),
        ("Cookies.Category.Preferences.Description",
            "Remembers your choices, such as language.",
            "Sparar dina val, till exempel språk."),
        ("Cookies.Category.Statistics.Name", "Statistics", "Statistik"),
        ("Cookies.Category.Statistics.Description",
            "Helps us understand how the site is used. Fully anonymous.",
            "Hjälper oss förstå hur sajten används. Helt anonymt."),
        ("Cookies.Category.Marketing.Name", "Marketing", "Marknadsföring"),
        ("Cookies.Category.Marketing.Description",
            "Used by embedded content, such as YouTube videos.",
            "Används av inbäddat innehåll, till exempel filmer från YouTube."),
        ("Cookies.Category.Cookies", "Cookies in this category", "Kakor i den här kategorin"),
        ("Cookies.Embed.Blocked.Body",
            "This content comes from another service and needs your consent.",
            "Det här innehållet kommer från en annan tjänst och kräver ditt samtycke."),
        ("Cookies.Embed.Blocked.Button", "Show content", "Visa innehåll"),
        ("Cookies.Policy.CurrentChoice", "Your current choice", "Ditt nuvarande val"),
        ("Cookies.Policy.NoChoice", "You have not made a choice yet.", "Du har inte gjort något val än."),
        // On/Off exist because CookiePolicy.cshtml used to render a hardcoded "på"/"av", making
        // the policy page Swedish in every language including English.
        ("Cookies.Policy.On", "on", "på"),
        ("Cookies.Policy.Off", "off", "av"),
        ("Cookies.Policy.Reopen", "Change settings", "Ändra inställningar"),
        ("Cookies.Policy.Withdraw", "Withdraw consent", "Återkalla samtycke"),
        ("Cookies.Footer.Link", "Cookie settings", "Cookieinställningar"),
        ("Cookies.Table.Name", "Name", "Namn"),
        ("Cookies.Table.Provider", "Provider", "Leverantör"),
        ("Cookies.Table.Purpose", "Purpose", "Syfte"),
        ("Cookies.Table.Duration", "Duration", "Lagringstid"),
        ("Cookies.Table.Type", "Type", "Typ"),
    ];

    /// <summary>Every key this installer seeds, for the resx parity check in the text provider.</summary>
    internal static IReadOnlyList<string> Keys { get; } = [.. Items.Select(item => item.Key)];

    public async Task InstallAsync()
    {
        IEnumerable<ILanguage> siteLanguages = await languageService.GetAllAsync();

        List<(ILanguage Language, string Code)> targets = siteLanguages
            .Select(language => (Language: language, Code: PrimarySubtag(language.IsoCode)))
            .Where(target => ShippedLanguages.Contains(target.Code))
            .ToList();

        if (targets.Count == 0)
        {
            // Not a failure. The site simply has no language the package ships text for; the
            // resx fallback covers the banner. Never create a language to fix this.
            logger.LogInformation(
                "Skipping cookie dictionary seeding: the site has no language the package ships text for ({Shipped}).",
                string.Join(", ", ShippedLanguages));
            return;
        }

        Guid? parentId = await EnsureParentAsync();

        var created = 0;
        var adopted = 0;
        foreach ((string key, string en, string sv) item in Items)
        {
            IDictionaryItem? existing = await dictionaryItemService.GetAsync(item.key);
            if (existing is not null)
            {
                if (await TryAdoptAsync(existing, parentId))
                {
                    adopted++;
                }

                continue;
            }

            var translations = new List<IDictionaryTranslation>();
            foreach ((ILanguage language, string code) in targets)
            {
                translations.Add(new DictionaryTranslation(language, TextFor(code, item)));
            }

            var dictionaryItem = new DictionaryItem(parentId, item.key) { Translations = translations };

            var attempt = await dictionaryItemService.CreateAsync(dictionaryItem, UserKey);
            if (attempt.Success is false)
            {
                logger.LogWarning("Could not create dictionary item {Key}: {Status}.", item.key, attempt.Status);
                continue;
            }

            created++;
        }

        if (created > 0)
        {
            logger.LogInformation(
                "Seeded {Count} cookie dictionary items for {Languages}.",
                created,
                string.Join(", ", targets.Select(target => target.Language.IsoCode)));
        }

        if (adopted > 0)
        {
            logger.LogInformation(
                "Filed {Count} existing cookie dictionary items under '{Parent}'.", adopted, ParentKey);
        }
    }

    /// <summary>The primary language subtag, lowercased: "en-GB" -> "en", "sv" -> "sv".</summary>
    private static string PrimarySubtag(string isoCode)
    {
        int dash = isoCode.IndexOf('-');
        return (dash < 0 ? isoCode : isoCode[..dash]).ToLowerInvariant();
    }

    private static string TextFor(string code, (string Key, string En, string Sv) item)
        => code == "sv" ? item.Sv : item.En;

    /// <summary>
    /// Returns the id of the parent node, creating it if absent. Returns null when it cannot be
    /// created: seeding the text still matters more than where the items sit in the tree.
    /// </summary>
    private async Task<Guid?> EnsureParentAsync()
    {
        IDictionaryItem? existing = await dictionaryItemService.GetAsync(ParentKey);
        if (existing is not null)
        {
            return existing.Key;
        }

        // No translations. The tree labels a node by its key, so this reads as "Cookie.Banner"
        // while staying invisible to GetDictionaryValue - nothing renders it.
        var parent = new DictionaryItem(ParentKey) { Translations = [] };

        var attempt = await dictionaryItemService.CreateAsync(parent, UserKey);
        if (attempt.Success is false)
        {
            logger.LogWarning(
                "Could not create the '{Parent}' dictionary parent: {Status}. Items stay at the root.",
                ParentKey,
                attempt.Status);
            return null;
        }

        return attempt.Result?.Key;
    }

    /// <summary>
    /// Files an item that is still at the root under the parent - the one-off tidy for items
    /// seeded before this grouping existed. An item an editor has deliberately moved somewhere
    /// else is left where they put it: this seeder creates and tidies, it does not enforce a
    /// shape on every boot.
    /// </summary>
    private async Task<bool> TryAdoptAsync(IDictionaryItem item, Guid? parentId)
    {
        if (parentId is null || item.ParentId is not null)
        {
            return false;
        }

        var attempt = await dictionaryItemService.MoveAsync(item, parentId, UserKey);
        if (attempt.Success)
        {
            return true;
        }

        logger.LogWarning(
            "Could not file dictionary item {Key} under '{Parent}': {Status}.",
            item.ItemKey,
            ParentKey,
            attempt.Status);
        return false;
    }
}
