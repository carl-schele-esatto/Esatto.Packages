using Esatto.Umbraco.Backoffice.SeoReport.Settings;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

internal static class ReadySettingsHarness
{
    public const string Ga4Property = "properties/123456789";

    public const string Site = "sc-domain:example.com";

    /// <summary>The day the services under test think it is, unless a test moves it: a mid-month day with a finished July behind it.</summary>
    public static readonly DateTimeOffset Now = new(2026, 8, 20, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Makes the package ready to fetch: a synthetic service-account key and both properties, saved the way a person would save
    /// them, and the harness clock set to <see cref="Now"/>. The brand terms are saved with them.
    /// </summary>
    public static async Task<SettingsHarness> ReadyAsync(params string[] brandTerms)
    {
        var harness = new SettingsHarness();
        harness.Time.SetUtcNow(Now);

        await harness.Settings.SaveAsync(
            new SeoReportSettings { Ga4PropertyId = Ga4Property, SearchConsoleSite = Site, BrandTerms = brandTerms },
            FakeServiceAccount.Json(),
            removeKey: false);

        return harness;
    }
}
