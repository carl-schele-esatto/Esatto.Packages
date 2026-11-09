using System.Text.Json;
using Esatto.Umbraco.Backoffice.Seo.Knowledge;
using Esatto.Umbraco.Backoffice.Seo.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Esatto.Umbraco.Backoffice.Seo.Settings;

/// <summary>
/// Reads and saves <see cref="SeoSettings"/>: database first, configuration (<c>Esatto:Seo</c>) wins.
/// Singleton with a one-minute cache, invalidated on save. Same approach as the Accessibility package.
/// </summary>
public sealed class SettingsService(ISeoStore store, IConfiguration configuration, TimeProvider time, ILogger<SettingsService>? logger = null)
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(1);
    private const string ExcludePathsKey = "ExcludePaths";

    /// <summary>Configuration-only keys under the same section (see <see cref="SeoEnvironment"/>): never settings, never "locked".</summary>
    private static readonly string[] ConfigOnly = [SeoEnvironment.IntentionallyHiddenKey, SeoEnvironment.RequestHeadersKey];

    private volatile CacheEntry? _cache;
    private int _generation;

    public async Task<EffectiveSettings> GetAsync()
    {
        var generation = Volatile.Read(ref _generation);
        if (_cache is { } cached && cached.Generation == generation && time.GetUtcNow() < cached.Until) return cached.Value;

        var effective = Apply(Deserialize(await store.GetSettingsJsonAsync()));
        _cache = new CacheEntry(effective, time.GetUtcNow() + CacheFor, generation);
        return effective;
    }

    /// <summary>Stores <paramref name="incoming"/> and returns what's now in force. Locked values in it are overridden.</summary>
    public async Task<EffectiveSettings> SaveAsync(SeoSettings incoming)
    {
        Normalize(incoming);
        await store.SaveSettingsJsonAsync(JsonSerializer.Serialize(incoming, KnowledgeBase.Json));
        Interlocked.Increment(ref _generation);
        return await GetAsync();
    }

    private EffectiveSettings Apply(SeoSettings settings)
    {
        var section = configuration.GetSection(SeoSettings.SectionName);
        var entries = section.AsEnumerable(makePathsRelative: true)
            .Where(kv => !ConfigOnly.Any(k => kv.Key.Equals(k, StringComparison.OrdinalIgnoreCase) || kv.Key.StartsWith(k + ":", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var leaves = entries.Where(kv => kv.Value is not null).Select(kv => kv.Key).ToList();

        // The binder appends configured array items to the existing array; a configured list must replace it.
        var excludeConfigured = entries.Any(kv => kv.Key.Equals(ExcludePathsKey, StringComparison.OrdinalIgnoreCase)
            || kv.Key.StartsWith(ExcludePathsKey + ":", StringComparison.OrdinalIgnoreCase));
        if (excludeConfigured)
        {
            settings.ExcludePaths = [];
            leaves.RemoveAll(k => k.Equals(ExcludePathsKey, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var key in leaves)
        {
            // One by one, so one bad value (e.g. "Parallelism": "lots") doesn't discard the valid ones.
            var single = new ConfigurationBuilder().AddInMemoryCollection([new(key, section[key])]).Build();
            try
            {
                single.Bind(settings);
            }
            catch (InvalidOperationException ex)
            {
                logger?.LogWarning(ex, "Ignoring invalid configuration value {Section}:{Key}", SeoSettings.SectionName, key);
            }
        }

        Normalize(settings);
        var locked = leaves.Append(excludeConfigured ? ExcludePathsKey : null).OfType<string>().Select(LockedPath).Distinct().ToList();
        return new EffectiveSettings(settings, locked);
    }

    /// <summary>"ExcludePaths:0" → "excludePaths"; "Schedule:Enabled" → "schedule.enabled".</summary>
    private static string LockedPath(string configKey)
        => string.Join('.', configKey.Split(':')
            .Where(segment => segment.Length > 0 && !segment.All(char.IsAsciiDigit))
            .Select(segment => char.ToLowerInvariant(segment[0]) + segment[1..]));

    private static SeoSettings Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new SeoSettings();
        try
        {
            return JsonSerializer.Deserialize<SeoSettings>(json, KnowledgeBase.Json) ?? new SeoSettings();
        }
        catch (JsonException)
        {
            return new SeoSettings(); // a corrupt row must never take the dashboard or the scan down
        }
    }

    private static void Normalize(SeoSettings s)
    {
        s.Schedule ??= new ScheduleSettings();
        s.Schedule.Hour = Math.Clamp(s.Schedule.Hour, 0, 23);
        s.Parallelism = Math.Clamp(s.Parallelism, 1, 8);
        s.DelayMs = Math.Clamp(s.DelayMs, 0, 5000);
        s.ExcludePaths = (s.ExcludePaths ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        s.BaseUrl = string.IsNullOrWhiteSpace(s.BaseUrl) ? null : s.BaseUrl.Trim();
    }

    private sealed record CacheEntry(EffectiveSettings Value, DateTimeOffset Until, int Generation);
}
