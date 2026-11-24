using System.Text.Json;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>
/// The settings layer wired the way the host will: an in-memory store, an ephemeral Data Protection key ring, a mutable
/// in-memory configuration, a clock a test can move, and a logger that records everything. The credential source shares the
/// settings service, as it does in the host.
/// </summary>
internal sealed class SettingsHarness
{
    public FakeTimeProvider Time { get; } = new();

    public InMemoryStore Store { get; }

    /// <summary>Writable: <see cref="Configure"/> changes it after the services exist, like a configuration reload.</summary>
    public IConfigurationRoot Configuration { get; } = new ConfigurationBuilder().AddInMemoryCollection().Build();

    public SecretProtector Protector { get; }

    public CapturingLogger<SettingsService> Log { get; } = new();

    public SettingsService Settings { get; }

    public GoogleCredentialSource Credentials { get; }

    public SettingsHarness(IDataProtectionProvider? provider = null)
    {
        Store = new InMemoryStore(Time);
        Protector = new SecretProtector(provider ?? new EphemeralDataProtectionProvider());
        Settings = new SettingsService(Store, Protector, Configuration, Time, Log);
        Credentials = new GoogleCredentialSource(Settings, Time);
    }

    /// <summary>Sets (or, with null, clears) <c>Esatto:SeoReport:&lt;name&gt;</c>.</summary>
    public void Configure(string name, string? value) => Configuration[$"{SeoReportSettings.SectionName}:{name}"] = value;

    /// <summary>Writes the <c>settings</c> document the way the service does (camelCase), from any object.</summary>
    public Task SeedSettingsAsync(object settings)
        => Store.SaveDocumentAsync(DocumentKeys.Settings, JsonSerializer.Serialize(settings, JsonSerializerOptions.Web));

    /// <summary>The <c>settings</c> document as stored, or null when none was written.</summary>
    public async Task<SeoReportSettings?> StoredSettingsAsync()
    {
        var json = await Store.GetDocumentAsync(DocumentKeys.Settings);
        return json is null ? null : JsonSerializer.Deserialize<SeoReportSettings>(json, JsonSerializerOptions.Web);
    }

    /// <summary>Stores <paramref name="keyJson"/> as the database key, leaving every other setting at its default.</summary>
    public Task<EffectiveSettings> SaveKeyAsync(string keyJson) => Settings.SaveAsync(new SeoReportSettings(), keyJson, false);

    /// <summary>
    /// Writes <paramref name="plain"/> into the <c>protectedKey</c> document exactly as the service would store it, without
    /// any validation: for tests that need a stored key the service itself would have refused.
    /// </summary>
    public Task StoreRawProtectedKeyAsync(string plain, SecretProtector? protector = null)
        => Store.SaveDocumentAsync(DocumentKeys.ProtectedKey, KeyVault.ToDocument((protector ?? Protector).Protect(plain)));
}
