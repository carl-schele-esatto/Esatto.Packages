using Esatto.Umbraco.Backoffice.Accessibility.Knowledge;

namespace Esatto.Umbraco.Backoffice.Accessibility.Settings;

/// <summary>
/// The settings in force: the database values with configuration applied on top.
/// <see cref="Locked"/> lists the camelCase dotted paths set by configuration (e.g. "visitor.panel.enabled"),
/// which the dashboard shows read-only.
/// </summary>
public sealed record EffectiveSettings(AccessibilitySettings Settings, IReadOnlyList<string> Locked, WcagTarget Target);
