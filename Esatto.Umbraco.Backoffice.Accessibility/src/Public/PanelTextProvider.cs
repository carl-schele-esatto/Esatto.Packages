using System.Globalization;
using System.Resources;
using Esatto.Umbraco.Backoffice.Accessibility.Settings;

namespace Esatto.Umbraco.Backoffice.Accessibility.Public;

/// <summary>
/// Texts for the visitor panel: the override set in Settings for the page's language, then the shipped
/// text for that language (<c>Resources/PanelText*.resx</c>: English and Swedish), then English.
/// </summary>
public static class PanelTextProvider
{
    private static readonly ResourceManager Resources = new(
        "Esatto.Umbraco.Backoffice.Accessibility.Resources.PanelText",
        typeof(PanelTextProvider).Assembly);

    public static string Get(PanelSettings panel, string key, CultureInfo culture)
    {
        if (panel.Texts.TryGetValue(culture.Name, out var exact) && exact.TryGetValue(key, out var e) && !string.IsNullOrWhiteSpace(e)) return e;
        if (panel.Texts.TryGetValue(culture.TwoLetterISOLanguageName, out var language) && language.TryGetValue(key, out var l) && !string.IsNullOrWhiteSpace(l)) return l;
        return Resources.GetString(key, culture) ?? key;
    }
}
