using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
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
/// <para>
/// Text comes from the same embedded resx <see cref="ConsentTextProvider" /> reads
/// (<c>Resources/ConsentText.resx</c> / <c>ConsentText.sv.resx</c>), not a second hand-maintained
/// table: the key list and every translation are read out of the resx at class-load time, so
/// there is exactly one place - Task 8's resx files - where consent copy is authored. That is also
/// why this file contains no Swedish literals: Swedish lives only in <c>ConsentText.sv.resx</c>.
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

    /// <summary>Same resx family <see cref="ConsentTextProvider" /> reads - see that type for why.</summary>
    private static readonly ResourceManager Resources = new(
        "Esatto.Umbraco.Backoffice.CookieBanner.Resources.ConsentText",
        typeof(CookieBannerDictionaryInstaller).Assembly);

    /// <summary>
    /// Two-letter language codes the package ships text for. A site language matches when its
    /// primary subtag is in here, so en-GB, en-US and a bare en all resolve to the English set.
    /// </summary>
    /// <remarks>
    /// Kept as an explicit list rather than derived from the satellite assemblies actually shipped:
    /// .NET has no supported API to enumerate which culture satellites exist for an assembly at
    /// runtime without probing the filesystem for "xx/AssemblyName.resources.dll" next to the main
    /// assembly, which is brittle under single-file publish and trimmed deployments and is exactly
    /// the kind of thing an Umbraco host's own publish settings could quietly break. This list is a
    /// fact about which culture folders ship, not a copy of the translated text, so it does not
    /// carry the same drift risk decision #4 is about.
    /// </remarks>
    private static readonly string[] ShippedLanguages = ["en", "sv"];

    /// <summary>
    /// Every key this installer seeds, for the resx parity check in the text provider. Read from
    /// the neutral (English) resource set rather than hand-listed, so the key list can never drift
    /// from what <see cref="ConsentTextProvider" /> actually ships.
    /// </summary>
    internal static IReadOnlyList<string> Keys { get; } = LoadKeys();

    private static IReadOnlyList<string> LoadKeys()
    {
        ResourceSet? neutral = Resources.GetResourceSet(CultureInfo.InvariantCulture, true, true);
        if (neutral is null)
        {
            return [];
        }

        var keys = new List<string>();
        foreach (DictionaryEntry entry in neutral)
        {
            if (entry.Key is string key)
            {
                keys.Add(key);
            }
        }

        return keys;
    }

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
        foreach (string key in Keys)
        {
            IDictionaryItem? existing = await dictionaryItemService.GetAsync(key);
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
                string? text = TextFor(Resources, code, key);
                if (text is null)
                {
                    // Neither this culture's resx nor the neutral one has the key. Cannot happen
                    // for a real shipped key (Keys is read from the same neutral set), but seeding
                    // an empty translation would be worse than seeding none.
                    continue;
                }

                translations.Add(new DictionaryTranslation(language, text));
            }

            var dictionaryItem = new DictionaryItem(parentId, key) { Translations = translations };

            var attempt = await dictionaryItemService.CreateAsync(dictionaryItem, UserKey);
            if (attempt.Success is false)
            {
                logger.LogWarning("Could not create dictionary item {Key}: {Status}.", key, attempt.Status);
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

    /// <summary>
    /// The text for <paramref name="key" /> in <paramref name="cultureCode" />, falling back to the
    /// neutral (English) resource set when that culture's resx omits the key, rather than seeding
    /// an empty translation. Internal - and takes <paramref name="resources" /> as a parameter
    /// rather than closing over the private static field - purely so a test can exercise the
    /// fallback with a resx pair that actually has a gap; the shipped resx never does (Task 8 keeps
    /// both cultures at 32/32 parity).
    /// </summary>
    internal static string? TextFor(ResourceManager resources, string cultureCode, string key)
    {
        string? value = resources.GetString(key, new CultureInfo(cultureCode));
        if (string.IsNullOrEmpty(value))
        {
            value = resources.GetString(key, CultureInfo.InvariantCulture);
        }

        return string.IsNullOrEmpty(value) ? null : value;
    }

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
