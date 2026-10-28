using System.Text.Json;
using Esatto.Umbraco.Backoffice.Accessibility.Knowledge;
using Esatto.Umbraco.Backoffice.Accessibility.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Esatto.Umbraco.Backoffice.Accessibility.Settings;

/// <summary>
/// Reads and saves <see cref="AccessibilitySettings"/>: database first, configuration
/// (<c>Esatto:Accessibility</c>) wins. Register as a singleton; the result is cached for a minute so the
/// public tag helpers don't hit the database on every request, and invalidated on save.
/// </summary>
/// <remarks>
/// Configuration exists so a site can pin the public-facing switches in appsettings and have them travel
/// from Test to Live with the code, while everything else stays editable in the backoffice.
/// </remarks>
public sealed class SettingsService(IAccessibilityStore store, IConfiguration configuration, TimeProvider time, ILogger<SettingsService>? logger = null)
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(1);

    private const string ExcludePathsKey = "Scan:ExcludePaths";

    private volatile CacheEntry? _cache;

    /// <summary>Bumped by every save; a cache entry read under an older generation is stale.</summary>
    private int _generation;

    public async Task<EffectiveSettings> GetAsync()
    {
        var generation = Volatile.Read(ref _generation);
        if (_cache is { } cached && cached.Generation == generation && time.GetUtcNow() < cached.Until) return cached.Value;

        var effective = Apply(Deserialize(await store.GetSettingsJsonAsync()));
        // Tagged with the generation the read started in: if a save finished meanwhile, this entry is ignored.
        _cache = new CacheEntry(effective, time.GetUtcNow() + CacheFor, generation);
        return effective;
    }

    /// <summary>Stores <paramref name="incoming"/> and returns what's now in force. Locked values in it are ignored.</summary>
    public async Task<EffectiveSettings> SaveAsync(AccessibilitySettings incoming)
    {
        Normalize(incoming);
        await store.SaveSettingsJsonAsync(JsonSerializer.Serialize(incoming, KnowledgeBase.Json));
        Interlocked.Increment(ref _generation);
        return await GetAsync();
    }

    private EffectiveSettings Apply(AccessibilitySettings settings)
    {
        var section = configuration.GetSection(AccessibilitySettings.SectionName);
        var entries = section.AsEnumerable(makePathsRelative: true).ToList();
        var leaves = entries.Where(kv => kv.Value is not null).Select(kv => kv.Key).ToList();

        // The binder appends configured array items to the existing array; a configured list must
        // replace the database list instead. ExcludePaths is the only array in the model. An empty list
        // ("ExcludePaths": [] in JSON, or an empty environment variable) arrives as the key alone, and
        // pins "no exclusions".
        var excludeConfigured = entries.Any(kv => kv.Key.Equals(ExcludePathsKey, StringComparison.OrdinalIgnoreCase));
        if (excludeConfigured)
        {
            settings.Scan.ExcludePaths = [];
            leaves.RemoveAll(k => k.Equals(ExcludePathsKey, StringComparison.OrdinalIgnoreCase)); // a value, not a list item: nothing to bind
        }

        try
        {
            section.Bind(settings);
        }
        catch (InvalidOperationException)
        {
            // One bad value (e.g. "Level": "AA+") must not take every public page down: apply the valid ones.
            foreach (var key in leaves)
            {
                var single = new ConfigurationBuilder().AddInMemoryCollection([new(key, section[key])]).Build();
                try
                {
                    single.Bind(settings);
                }
                catch (InvalidOperationException ex)
                {
                    logger?.LogWarning(ex, "Ignoring invalid configuration value {Section}:{Key}", AccessibilitySettings.SectionName, key);
                }
            }
        }
        Normalize(settings);

        var locked = leaves.Append(excludeConfigured ? ExcludePathsKey : null).OfType<string>().Select(LockedPath).Distinct().ToList();
        return new EffectiveSettings(settings, locked, new WcagTarget(settings.Version, settings.Level));
    }

    /// <summary>"Scan:ExcludePaths:0" → "scan.excludePaths".</summary>
    private static string LockedPath(string configKey)
        => string.Join('.', configKey
            .Split(':')
            .Where(segment => segment.Length > 0 && !segment.All(char.IsAsciiDigit))
            .Select(segment => char.ToLowerInvariant(segment[0]) + segment[1..]));

    private static AccessibilitySettings Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new AccessibilitySettings();
        try
        {
            return JsonSerializer.Deserialize<AccessibilitySettings>(json, KnowledgeBase.Json) ?? new AccessibilitySettings();
        }
        catch (JsonException)
        {
            // A corrupt row must never take the dashboard or the public site down.
            return new AccessibilitySettings();
        }
    }

    private static void Normalize(AccessibilitySettings s)
    {
        s.Scan ??= new ScanSettings();
        s.Visitor ??= new VisitorSettings();
        s.Visitor.Panel ??= new PanelSettings();
        s.Scan.ExcludePaths = (s.Scan.ExcludePaths ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        s.Scan.RecheckMonths = Math.Clamp(s.Scan.RecheckMonths, 1, 24);
        if (s.Visitor.Panel.Position is not ("bottom-right" or "bottom-left")) s.Visitor.Panel.Position = "bottom-right";
        s.Visitor.Panel.Texts = IgnoreCase(s.Visitor.Panel.Texts);
    }

    /// <summary>
    /// JSON deserialization drops the dictionaries' case-insensitive comparers, so "sv-se" saved in Settings
    /// wouldn't match the page's "sv-SE". Rebuilt here, merging keys that differ only in case.
    /// </summary>
    private static Dictionary<string, Dictionary<string, string>> IgnoreCase(Dictionary<string, Dictionary<string, string>>? texts)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (culture, values) in texts ?? [])
        {
            if (!result.TryGetValue(culture, out var merged)) result[culture] = merged = new(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, text) in values ?? []) merged[key] = text;
        }

        return result;
    }

    private sealed record CacheEntry(EffectiveSettings Value, DateTimeOffset Until, int Generation);
}
