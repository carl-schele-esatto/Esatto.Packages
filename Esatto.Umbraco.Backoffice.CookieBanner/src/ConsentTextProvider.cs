using System.Globalization;
using System.Resources;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Dictionary;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Resolves consent copy: Umbraco dictionary item, then the embedded resx for the request culture,
/// then English.
/// </summary>
/// <remarks>
/// The dictionary comes first because consent copy is exactly the text that changes for legal
/// reasons; editors must be able to reword it without a deploy. The resx layer exists so the
/// package works on a site that has never seen the seeder - the previous design put Swedish
/// literals in Razor fallbacks instead.
/// <para>
/// The culture comes from <see cref="ICultureDictionary.Culture" /> rather than
/// <see cref="CultureInfo.CurrentUICulture" />, so a consumer who replaces
/// <see cref="ICultureDictionaryFactory" /> gets their culture honoured on both layers.
/// </para>
/// </remarks>
internal sealed class ConsentTextProvider(
    ICultureDictionaryFactory cultureDictionaryFactory,
    ILogger<ConsentTextProvider> logger) : IConsentTextProvider
{
    private static readonly ResourceManager Resources = new(
        "Esatto.Umbraco.Backoffice.CookieBanner.Resources.ConsentText",
        typeof(ConsentTextProvider).Assembly);

    /// <summary>The ultimate fallback. Its strings live in the main assembly, not a satellite.</summary>
    private static readonly CultureInfo English = new("en");

    public string Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        CultureInfo culture = English;

        try
        {
            ICultureDictionary dictionary = cultureDictionaryFactory.CreateDictionary();
            culture = dictionary.Culture ?? English;

            // Umbraco returns an empty string for an absent item, so blank means "not translated"
            // rather than "translated to nothing". Falling through is what makes the fallback work.
            var edited = dictionary[key];
            if (string.IsNullOrWhiteSpace(edited) is false)
            {
                return edited;
            }
        }
        catch (Exception ex)
        {
            // Text lookup must never take a page down: outside an Umbraco request scope, or before
            // the database is reachable, the dictionary can throw. The shipped text still renders.
            logger.LogDebug(ex, "Dictionary lookup for {Key} failed; using the shipped text.", key);
        }

        return FromResources(key, culture)
            ?? FromResources(key, English)
            ?? key;
    }

    private static string? FromResources(string key, CultureInfo culture)
    {
        try
        {
            var value = Resources.GetString(key, culture);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (MissingManifestResourceException)
        {
            return null;
        }
    }
}
