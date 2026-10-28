using Esatto.Umbraco.Backoffice.Accessibility.Knowledge;

namespace Esatto.Umbraco.Backoffice.Accessibility.Settings;

/// <summary>
/// Everything editable on the dashboard's Settings tab. Stored as JSON in the database; any value
/// also present under <see cref="SectionName"/> in configuration wins (see <see cref="SettingsService"/>).
/// </summary>
/// <remarks>Mutable classes with setters, because the configuration binder binds onto them.</remarks>
public sealed class AccessibilitySettings
{
    public const string SectionName = "Esatto:Accessibility";

    public WcagVersion Version { get; set; } = WcagVersion.V22;
    public WcagLevel Level { get; set; } = WcagLevel.AA;
    public ScanSettings Scan { get; set; } = new();
    public VisitorSettings Visitor { get; set; } = new();
}

public sealed class ScanSettings
{
    /// <summary>URL path prefixes that are never scanned, e.g. "/search".</summary>
    public string[] ExcludePaths { get; set; } = [];

    /// <summary>Also scan every page at 320 CSS px (doubles scan time).</summary>
    public bool MobileWidth { get; set; }

    /// <summary>Run axe's best-practice rules too (shown separately, never scored).</summary>
    public bool BestPractice { get; set; }

    /// <summary>A manual result older than this many months is flagged "Needs re-check" (1–24).</summary>
    public int RecheckMonths { get; set; } = 6;
}

public sealed class VisitorSettings
{
    /// <summary>Honour the visitor's prefers-reduced-motion setting.</summary>
    public bool FollowOsMotion { get; set; } = true;

    /// <summary>Honour the visitor's prefers-contrast: more setting (generic CSS, off by default).</summary>
    public bool FollowOsContrast { get; set; }

    public PanelSettings Panel { get; set; } = new();
}

public sealed class PanelSettings
{
    /// <summary>Show the accessibility preferences button on the public site.</summary>
    public bool Enabled { get; set; }

    /// <summary>"bottom-right" or "bottom-left".</summary>
    public string Position { get; set; } = "bottom-right";

    public bool LargerText { get; set; } = true;
    public bool Spacing { get; set; } = true;
    public bool HighContrast { get; set; } = true;
    public bool ReduceMotion { get; set; } = true;
    public bool ReadableFont { get; set; } = true;
    public bool HighlightLinks { get; set; } = true;
    public bool ReadingGuide { get; set; }

    /// <summary>Per-culture text overrides for the panel: culture ("sv") → text key ("ButtonLabel") → text.</summary>
    public Dictionary<string, Dictionary<string, string>> Texts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
