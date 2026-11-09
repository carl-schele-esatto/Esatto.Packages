namespace Esatto.Umbraco.Backoffice.Seo.Settings;

/// <summary>
/// Everything editable on the dashboard's Settings tab. Stored as JSON in the database; any value also
/// present under <see cref="SectionName"/> in configuration wins (see <see cref="SettingsService"/>).
/// </summary>
/// <remarks>Mutable classes with setters, because the configuration binder binds onto them.</remarks>
public sealed class SeoSettings
{
    public const string SectionName = "Esatto:Seo";

    public ScheduleSettings Schedule { get; set; } = new();

    /// <summary>URL path prefixes that are never scanned, e.g. "/search".</summary>
    public string[] ExcludePaths { get; set; } = [];

    /// <summary>Pages fetched at the same time (1–8).</summary>
    public int Parallelism { get; set; } = 2;

    /// <summary>Pause between requests per worker, in ms (0–5000).</summary>
    public int DelayMs { get; set; }

    /// <summary>Used for pages whose URL is relative (no domain bound). Normally empty.</summary>
    public string? BaseUrl { get; set; }
}

public sealed class ScheduleSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>Hour of day (server time, 0–23) the nightly scan starts.</summary>
    public int Hour { get; set; } = 3;
}

/// <summary>
/// The settings in force: database values with configuration applied on top. <see cref="Locked"/> lists
/// the camelCase dotted paths set by configuration (e.g. "schedule.enabled"), shown read-only.
/// </summary>
public sealed record EffectiveSettings(SeoSettings Settings, IReadOnlyList<string> Locked);
