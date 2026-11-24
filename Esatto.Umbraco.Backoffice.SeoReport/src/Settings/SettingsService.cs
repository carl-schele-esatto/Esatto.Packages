using System.Text.Json;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Esatto.Umbraco.Backoffice.SeoReport.Settings;

/// <summary>Whether the package has what it needs to fetch a report.</summary>
public enum SetupState
{
    /// <summary>Something is missing: the key, the GA4 property or the Search Console property (see <see cref="SettingsService.GetMissingAsync"/>).</summary>
    NotConfigured,

    /// <summary>A key is present but cannot be used (see <see cref="KeyStatus.Unreadable"/>), whatever else is set.</summary>
    KeyUnreadable,

    /// <summary>A usable key and both properties are set.</summary>
    Ready,
}

/// <summary>
/// A save that was refused. <see cref="Code"/> is a fixed word (<c>ga4PropertyInvalid</c>, <c>searchConsoleInvalid</c>,
/// <c>keyInvalid</c>) and is also the message: nothing the person typed is ever put in it.
/// </summary>
public sealed class SettingsValidationException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

/// <summary>
/// Reads and saves <see cref="SeoReportSettings"/> and the service-account key: database first, configuration
/// (<c>Esatto:SeoReport</c>) wins and locks the field. Singleton with a one-minute cache, invalidated on save; the same
/// approach as the SEO Health package. The key is encrypted at rest, write-only (nothing here returns it), and read from
/// configuration when it is set there (see <see cref="KeyVault"/>).
/// </summary>
public sealed class SettingsService(
    ISeoReportStore store,
    SecretProtector protector,
    IConfiguration configuration,
    TimeProvider time,
    ILogger<SettingsService>? logger = null)
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(1);

    // The names a person is told are missing (they are also the camelCase names of the settings).
    private const string MissingKey = "key";
    private const string MissingGa4 = "ga4PropertyId";
    private const string MissingSearchConsole = "searchConsoleSite";

    /// <summary>
    /// Every setting, once: its configuration name, whether it is a list, and how to copy it between two settings objects. The
    /// lock list, the configuration read and the save all go through this, so a new setting is added in one place.
    /// </summary>
    private static readonly Field[] Fields =
    [
        new(nameof(SeoReportSettings.Ga4PropertyId), false, (to, from) => to.Ga4PropertyId = from.Ga4PropertyId),
        new(nameof(SeoReportSettings.SearchConsoleSite), false, (to, from) => to.SearchConsoleSite = from.SearchConsoleSite),
        new(nameof(SeoReportSettings.BrandTerms), true, (to, from) => to.BrandTerms = from.BrandTerms),
        new(nameof(SeoReportSettings.DisplayName), false, (to, from) => to.DisplayName = from.DisplayName),
        new(nameof(SeoReportSettings.FindingsUserGroups), true, (to, from) => to.FindingsUserGroups = from.FindingsUserGroups),
        new(nameof(SeoReportSettings.AutoFetch), false, (to, from) => to.AutoFetch = from.AutoFetch),
    ];

    private readonly KeyVault _keys = new(store, protector, configuration);
    private volatile CacheEntry? _cache;
    private int _generation;
    private int _unreadableWarned;

    /// <summary>The key finder, for <see cref="GoogleCredentialSource"/>: the key text never leaves it, only a credential does.</summary>
    internal KeyVault Keys => _keys;

    /// <summary>Changes on every save, so a cache that depends on the settings or the key can tell it is stale at once.</summary>
    internal int Generation => Volatile.Read(ref _generation);

    public async Task<EffectiveSettings> GetAsync()
    {
        var generation = Generation;
        if (_cache is { } cached && cached.Generation == generation && time.GetUtcNow() < cached.Until) return cached.Value;

        var effective = await LoadAsync();
        _cache = new CacheEntry(effective, time.GetUtcNow() + CacheFor, generation);
        return effective;
    }

    /// <summary>
    /// Stores <paramref name="incoming"/> and returns what is now in force. A field configuration sets is ignored here (the
    /// database keeps its old value for it, which applies again if the configuration is removed). A non-blank
    /// <paramref name="newKeyJson"/> replaces the stored key, and wins over <paramref name="removeKey"/>; otherwise
    /// <paramref name="removeKey"/> deletes it. Everything is checked before anything is written, and a refusal is a
    /// <see cref="SettingsValidationException"/>: <c>ga4PropertyInvalid</c>, <c>searchConsoleInvalid</c> or <c>keyInvalid</c>,
    /// checked in that order.
    /// </summary>
    public async Task<EffectiveSettings> SaveAsync(SeoReportSettings incoming, string? newKeyJson, bool removeKey)
    {
        ArgumentNullException.ThrowIfNull(incoming);

        var locked = ReadConfiguration().Locked;
        RequireValid(locked, nameof(SeoReportSettings.Ga4PropertyId), incoming.Ga4PropertyId, SettingsNormalizer.NormalizeGa4, ErrorCodes.Ga4PropertyInvalid);
        RequireValid(locked, nameof(SeoReportSettings.SearchConsoleSite), incoming.SearchConsoleSite, SettingsNormalizer.NormalizeSearchConsole, ErrorCodes.SearchConsoleInvalid);

        var protectedKey = string.IsNullOrWhiteSpace(newKeyJson)
            ? null
            : await _keys.ProtectForStorageAsync(newKeyJson, CancellationToken.None);

        var toStore = Clean(Deserialize(await store.GetDocumentAsync(DocumentKeys.Settings)));
        var wanted = Clean(incoming);
        foreach (var field in Fields.Where(f => !locked.Contains(f.Name))) field.Copy(toStore, wanted);

        try
        {
            await store.SaveDocumentAsync(DocumentKeys.Settings, JsonSerializer.Serialize(toStore, JsonSerializerOptions.Web));
            if (protectedKey is not null)
            {
                await store.SaveDocumentAsync(DocumentKeys.ProtectedKey, protectedKey);
                LogKeyChange("saved");
            }
            else if (removeKey)
            {
                // Only a key that was there is "removed": deleting nothing is no change worth a line.
                var hadKey = await store.GetDocumentAsync(DocumentKeys.ProtectedKey) is not null;
                await store.DeleteDocumentAsync(DocumentKeys.ProtectedKey);
                if (hadKey) LogKeyChange("removed");
            }
        }
        finally
        {
            // Even a half-finished save makes both the settings cache and the credential cache stale.
            Interlocked.Increment(ref _generation);
            Volatile.Write(ref _unreadableWarned, 0);
        }

        return await GetAsync();
    }

    /// <summary><see cref="SetupState.KeyUnreadable"/> wins; otherwise <see cref="SetupState.Ready"/> when nothing is missing.</summary>
    public async Task<SetupState> GetSetupStateAsync()
    {
        var effective = await GetAsync();
        if (effective.Key.Unreadable) return SetupState.KeyUnreadable;

        return Missing(effective).Count == 0 ? SetupState.Ready : SetupState.NotConfigured;
    }

    /// <summary>What is still needed to be ready: any of <c>key</c>, <c>ga4PropertyId</c>, <c>searchConsoleSite</c>, in that order. An unreadable key counts as missing.</summary>
    public async Task<IReadOnlyList<string>> GetMissingAsync() => Missing(await GetAsync());

    private static List<string> Missing(EffectiveSettings effective)
    {
        var missing = new List<string>(3);
        if (!effective.Key.HasKey) missing.Add(MissingKey);
        if (string.IsNullOrEmpty(effective.Settings.Ga4PropertyId)) missing.Add(MissingGa4);
        if (string.IsNullOrEmpty(effective.Settings.SearchConsoleSite)) missing.Add(MissingSearchConsole);
        return missing;
    }

    private async Task<EffectiveSettings> LoadAsync()
    {
        var settings = Clean(Deserialize(await store.GetDocumentAsync(DocumentKeys.Settings)));
        var (overrides, locked) = ReadConfiguration();
        foreach (var field in Fields.Where(f => locked.Contains(f.Name))) field.Copy(settings, overrides);

        var key = await _keys.ResolveAsync(CancellationToken.None);
        WarnOnceIfUnreadable(key);

        return new EffectiveSettings(
            settings,
            Fields.Where(f => locked.Contains(f.Name)).Select(f => f.LockedName).ToArray(),
            new KeyStatus(key.HasKey, key.Source, key.ClientEmail, key.Unreadable));
    }

    /// <summary>
    /// The values configuration sets, and the names of the fields they lock. It binds one field at a time, so one bad value
    /// (<c>AutoFetch: "lots"</c>) does not discard the valid ones; a field whose value was rejected is not locked and keeps its
    /// database value. A list is bound whole, because binding its items one by one would not keep their order. Warnings name
    /// the field and never the value, which may be anything.
    /// </summary>
    private (SeoReportSettings Overrides, HashSet<string> Locked) ReadConfiguration()
    {
        var section = configuration.GetSection(SeoReportSettings.SectionName);
        var entries = section.AsEnumerable(makePathsRelative: true).Where(kv => !string.IsNullOrWhiteSpace(kv.Value)).ToList();

        var bound = new SeoReportSettings();
        var locked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in Fields)
        {
            var all = entries.Where(kv => field.Owns(kv.Key)).ToList();
            if (all.Count == 0) continue;

            // A single value cannot make a list, and the binder would skip it without a word, so it is refused here.
            var items = field.IsList ? all.Where(kv => !kv.Key.Equals(field.Name, StringComparison.OrdinalIgnoreCase)).ToList() : all;

            var accepted = items.Count > 0 && TryBind(bound, items);
            if (accepted) locked.Add(field.Name);
            if (!accepted || items.Count < all.Count) WarnIgnored(field.Name);
        }

        var overrides = Clean(bound);
        RejectUnusable(locked, nameof(SeoReportSettings.Ga4PropertyId), overrides.Ga4PropertyId);
        RejectUnusable(locked, nameof(SeoReportSettings.SearchConsoleSite), overrides.SearchConsoleSite);
        return (overrides, locked);
    }

    // The binder reports a value it cannot convert as an InvalidOperationException whose inner exception quotes the value, so
    // the exception is dropped here and only the caller's warning (field name only) is logged.
    private static bool TryBind(SeoReportSettings target, IEnumerable<KeyValuePair<string, string?>> values)
    {
        var mini = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        try
        {
            mini.Bind(target);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    // A trace of every change to the stored key: what happened and where the key is kept. Never the key, its fingerprint or the
    // service account's e-mail; who made the change is in the host's own request log.
    private void LogKeyChange(string action)
        => logger?.LogInformation("The service account key was {Action} ({Source}).", action, KeySourceKind.Database);

    private void WarnIgnored(string field)
        => logger?.LogWarning("Ignoring invalid configuration value {Section}:{Key}", SeoReportSettings.SectionName, field);

    /// <summary>A configured id that does not normalize would blank the field; it is dropped instead, and the database value stands.</summary>
    private void RejectUnusable(HashSet<string> locked, string field, string? normalized)
    {
        if (normalized is not null || !locked.Remove(field)) return;

        WarnIgnored(field);
    }

    /// <summary>A value to save for an unlocked field must be blank (it clears the field) or normalize; a locked field's incoming value is ignored, so never checked.</summary>
    private static void RequireValid(HashSet<string> locked, string field, string? value, Func<string?, string?> normalize, string code)
    {
        if (locked.Contains(field) || string.IsNullOrWhiteSpace(value)) return;

        if (normalize(value) is null) throw new SettingsValidationException(code);
    }

    private void WarnOnceIfUnreadable(ResolvedKey key)
    {
        if (!key.Unreadable)
        {
            Volatile.Write(ref _unreadableWarned, 0);
            return;
        }

        if (Interlocked.Exchange(ref _unreadableWarned, 1) == 0)
        {
            logger?.LogWarning(
                "The service account key from {Source} cannot be used ({Code}). A saved key has to be entered again (it may have been encrypted with another key ring); a configured one has to be fixed.",
                key.Source,
                key.ErrorCode);
        }
    }

    private static SeoReportSettings Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new SeoReportSettings();

        try
        {
            return JsonSerializer.Deserialize<SeoReportSettings>(json, JsonSerializerOptions.Web) ?? new SeoReportSettings();
        }
        catch (JsonException)
        {
            return new SeoReportSettings(); // a corrupt row must never take the dashboard or the fetch down
        }
    }

    /// <summary>
    /// A normalized copy: ids in Google's form (null when they do not normalize), lists tidied, de-duplicated and capped (see
    /// <see cref="SettingsNormalizer.NormalizeList"/>), the name trimmed and capped, a blank name as null. Silently: what is kept is
    /// returned by the save and shown in the Settings tab.
    /// </summary>
    private static SeoReportSettings Clean(SeoReportSettings settings) => new()
    {
        Ga4PropertyId = SettingsNormalizer.NormalizeGa4(settings.Ga4PropertyId),
        SearchConsoleSite = SettingsNormalizer.NormalizeSearchConsole(settings.SearchConsoleSite),
        BrandTerms = SettingsNormalizer.NormalizeList(settings.BrandTerms),
        DisplayName = SettingsNormalizer.NormalizeDisplayName(settings.DisplayName),
        FindingsUserGroups = SettingsNormalizer.NormalizeGroupAliases(settings.FindingsUserGroups),
        AutoFetch = settings.AutoFetch,
    };

    private sealed record Field(string Name, bool IsList, Action<SeoReportSettings, SeoReportSettings> Copy)
    {
        /// <summary>"Ga4PropertyId" → "ga4PropertyId": the name the dashboard knows the field by.</summary>
        public string LockedName { get; } = char.ToLowerInvariant(Name[0]) + Name[1..];

        /// <summary>Whether the configuration key (relative to the section) belongs to this field: the field itself or an item under it.</summary>
        public bool Owns(string configKey)
            => configKey.Equals(Name, StringComparison.OrdinalIgnoreCase)
               || configKey.StartsWith(Name + ":", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record CacheEntry(EffectiveSettings Value, DateTimeOffset Until, int Generation);
}
