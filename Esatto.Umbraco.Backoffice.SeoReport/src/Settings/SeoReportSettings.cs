namespace Esatto.Umbraco.Backoffice.SeoReport.Settings;

/// <summary>
/// Everything editable on the dashboard's Settings tab. Stored as JSON in the database; any value also present under
/// <see cref="SectionName"/> in configuration wins (see <see cref="SettingsService"/>). The service-account key is not
/// here: it is a secret with its own storage, and configuration supplies it through two keys that are never settings.
/// </summary>
/// <remarks>A mutable class with setters, because the configuration binder and the JSON serializer fill it in.</remarks>
public sealed class SeoReportSettings
{
    public const string SectionName = "Esatto:SeoReport";

    /// <summary>The GA4 property, as <c>properties/123456789</c>.</summary>
    public string? Ga4PropertyId { get; set; }

    /// <summary>The Search Console property: <c>sc-domain:example.com</c> or a URL prefix such as <c>https://www.example.com/</c>.</summary>
    public string? SearchConsoleSite { get; set; }

    /// <summary>Terms that mark a search query as a brand query; see <see cref="BrandPattern"/>.</summary>
    public string[] BrandTerms { get; set; } = [];

    /// <summary>The name shown on the report instead of the site's address. Empty means none.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Aliases of the user groups that see the findings. Empty means everyone with access.</summary>
    public string[] FindingsUserGroups { get; set; } = [];

    /// <summary>Whether the scheduled job fetches missing months by itself.</summary>
    public bool AutoFetch { get; set; } = true;
}

/// <summary>
/// The settings in force: database values with configuration applied on top. <see cref="Locked"/> lists the camelCase names
/// of the fields configuration sets (shown read-only), and <see cref="Key"/> says which key is in force, never the key.
/// </summary>
public sealed record EffectiveSettings(SeoReportSettings Settings, string[] Locked, KeyStatus Key);
