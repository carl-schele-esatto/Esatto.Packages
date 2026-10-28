using System.Globalization;
using Esatto.Umbraco.Backoffice.Accessibility.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Esatto.Umbraco.Backoffice.Accessibility.Public;

/// <summary>One option in the panel: the preference key the scripts use (<c>data-pref</c>) and its label.</summary>
public sealed record PanelOption(string Pref, string Label);

/// <summary>What <c>Views/Shared/Components/AccessibilityPanel/Default.cshtml</c> renders.</summary>
public sealed record PanelViewModel(
    string Position,
    string ButtonLabel,
    string DialogTitle,
    string Reset,
    string Close,
    string Note,
    IReadOnlyList<PanelOption> Options,
    string Language,
    string ScriptUrl);

/// <summary>The panel's options in display order, with the setting that switches each on.</summary>
internal static class PanelOptions
{
    private static readonly (string Pref, string TextKey, Func<PanelSettings, bool> Enabled)[] All =
    [
        ("text", "LargerText", p => p.LargerText),
        ("spacing", "Spacing", p => p.Spacing),
        ("contrast", "HighContrast", p => p.HighContrast),
        ("motion", "ReduceMotion", p => p.ReduceMotion),
        ("font", "ReadableFont", p => p.ReadableFont),
        ("links", "HighlightLinks", p => p.HighlightLinks),
        ("guide", "ReadingGuide", p => p.ReadingGuide),
    ];

    public static IEnumerable<(string Pref, string TextKey)> Enabled(PanelSettings panel)
        => All.Where(o => o.Enabled(panel)).Select(o => (o.Pref, o.TextKey));
}

/// <summary>Renders the visitor preferences panel; invoked by <c>&lt;a11y-panel /&gt;</c>.</summary>
public sealed class AccessibilityPanelViewComponent(SettingsService settings, IFileVersionProvider versions) : ViewComponent
{
    internal const string ScriptPath = "/esatto-accessibility/a11y-panel.js";

    public async Task<IViewComponentResult> InvokeAsync()
        => View(BuildModel(
            (await settings.GetAsync()).Settings.Visitor.Panel,
            CultureInfo.CurrentUICulture,
            versions.AddFileVersionToPath(HttpContext.Request.PathBase, ScriptPath)));

    /// <param name="scriptUrl">The panel script's URL, with its file version (see <see cref="AccessibilityHeadTagHelper"/>).</param>
    public static PanelViewModel BuildModel(PanelSettings panel, CultureInfo culture, string scriptUrl = ScriptPath)
    {
        string T(string key) => PanelTextProvider.Get(panel, key, culture);
        return new PanelViewModel(
            panel.Position,
            T("ButtonLabel"),
            T("DialogTitle"),
            T("Reset"),
            T("Close"),
            T("Note"),
            PanelOptions.Enabled(panel).Select(o => new PanelOption(o.Pref, T(o.TextKey))).ToList(),
            culture.TwoLetterISOLanguageName,
            scriptUrl);
    }
}
