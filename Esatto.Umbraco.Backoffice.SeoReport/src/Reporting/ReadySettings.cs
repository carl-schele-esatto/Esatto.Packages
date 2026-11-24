using Esatto.Umbraco.Backoffice.SeoReport.Settings;

namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

/// <summary>
/// The settings a Google fetch needs, once the package is set up: both properties are present and in Google's own form. Two
/// readings of the same settings are equal, whatever list objects they hold (<see cref="SettingsService"/> hands out new ones
/// whenever it reloads, and a record compares a list by reference).
/// </summary>
internal sealed record ReadySettings(string Ga4PropertyId, string SearchConsoleSite, IReadOnlyList<string> BrandTerms)
{
    public bool Equals(ReadySettings? other)
        => other is not null
           && string.Equals(Ga4PropertyId, other.Ga4PropertyId, StringComparison.Ordinal)
           && string.Equals(SearchConsoleSite, other.SearchConsoleSite, StringComparison.Ordinal)
           && BrandTerms.SequenceEqual(other.BrandTerms, StringComparer.Ordinal);

    public override int GetHashCode() => HashCode.Combine(Ga4PropertyId, SearchConsoleSite, BrandTerms.Count);
}

internal static class ReadySettingsExtensions
{
    /// <summary>
    /// The settings in force when the package has a usable key and both properties, so the agents' own complaints about blank
    /// settings (English sentences) never surface. Otherwise a <see cref="ReportFetchException"/> with the code
    /// <see cref="KeyErrorCodes.KeyUnreadable"/> or <see cref="KeyErrorCodes.NotConfigured"/>: the key problem first, as
    /// <see cref="SettingsService.GetSetupStateAsync"/> reports it.
    /// </summary>
    public static async Task<ReadySettings> RequireReadyAsync(this SettingsService settings)
    {
        switch (await settings.GetSetupStateAsync())
        {
            case SetupState.KeyUnreadable:
                throw new ReportFetchException(KeyErrorCodes.KeyUnreadable);
            case SetupState.NotConfigured:
                throw new ReportFetchException(KeyErrorCodes.NotConfigured);
        }

        return await ReadValuesAsync(settings) ?? throw new ReportFetchException(KeyErrorCodes.NotConfigured);
    }

    /// <summary>
    /// The same settings, or null when the package is not ready (no usable key, or a property missing). Never throws: it is for
    /// readers, which have to cope with a package that is not connected yet.
    /// </summary>
    public static async Task<ReadySettings?> TryGetReadyAsync(this SettingsService settings)
        => await settings.GetSetupStateAsync() == SetupState.Ready ? await ReadValuesAsync(settings) : null;

    /// <summary>The values, read after the setup state. A save in between could have blanked a property; that is "not ready" too.</summary>
    private static async Task<ReadySettings?> ReadValuesAsync(SettingsService settings)
    {
        var current = (await settings.GetAsync()).Settings;

        return current.Ga4PropertyId is { Length: > 0 } ga4 && current.SearchConsoleSite is { Length: > 0 } site
            ? new ReadySettings(ga4, site, current.BrandTerms)
            : null;
    }
}
