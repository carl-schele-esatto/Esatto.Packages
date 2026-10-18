using System.Text.Json;
using System.Text.Json.Serialization;

namespace Esatto.CookieScan.Core;

/// <summary>
/// The known-cookie catalogue: name patterns mapped to a provider, a category and the wording to
/// put on the policy page.
/// </summary>
/// <remarks>
/// Data rather than code because its <c>purpose</c> text becomes public legal wording, and that
/// must be editable without a rebuild. The embedded copy is the default; a
/// <c>cookie-catalogue.json</c> beside the exe replaces it wholesale.
/// </remarks>
public sealed class CookieCatalogue
{
    private const string EmbeddedName = "Esatto.CookieScan.Core.Resources.cookie-catalogue.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private CookieCatalogue(string unknownCategory, IReadOnlyList<CatalogueEntry> entries)
    {
        UnknownCategory = unknownCategory;
        Entries = entries;
        Expected = entries.Where(entry => entry.Expected).ToArray();
    }

    /// <summary>Category given to an unrecognised name that no pass could attribute.</summary>
    public string UnknownCategory { get; }

    public IReadOnlyList<CatalogueEntry> Entries { get; }

    /// <summary>
    /// Entries known to apply to this site's own stack, so their absence from a scan is itself
    /// worth reporting. Third-party entries are excluded: an absent Google cookie is normal.
    /// </summary>
    public IReadOnlyList<CatalogueEntry> Expected { get; }

    /// <summary>The catalogue compiled into the assembly.</summary>
    public static CookieCatalogue Default()
    {
        using Stream stream = typeof(CookieCatalogue).Assembly
            .GetManifestResourceStream(EmbeddedName)
            ?? throw new InvalidOperationException(
                $"The embedded catalogue '{EmbeddedName}' is missing. Check that "
                + "Resources\\cookie-catalogue.json is still an EmbeddedResource in the csproj.");

        using var reader = new StreamReader(stream);

        return Parse(reader.ReadToEnd());
    }

    public static CookieCatalogue Parse(string json)
    {
        Document? document = JsonSerializer.Deserialize<Document>(json, SerializerOptions)
            ?? throw new InvalidOperationException("The cookie catalogue is empty or not valid JSON.");

        return new CookieCatalogue(
            string.IsNullOrWhiteSpace(document.UnknownCategory) ? "marketing" : document.UnknownCategory,
            document.Entries ?? []);
    }

    /// <summary>
    /// The same catalogue with the banner's consent cookie renamed to what this site actually calls
    /// it. Returns this instance unchanged when there is nothing to do.
    /// </summary>
    /// <remarks>
    /// The consent cookie is the one catalogue entry whose name is configuration rather than a fact:
    /// <c>CookieBannerOptions.CookieName</c> defaults to <c>cookie-consent</c> and any site may
    /// change it. Leaving it wrong produces two loud false findings at once - the site's real consent
    /// cookie is unrecognised, so it takes <see cref="UnknownCategory"/>, is seen on the reject-all
    /// pass and is reported as a violation, while the catalogue's own entry is simultaneously
    /// reported as declared-but-never-found. On the one cookie that exists to record a refusal.
    /// <para>
    /// Renames by flag rather than by matching the default pattern, so a catalogue override that
    /// ships its own consent entry can say which one it is instead of having to keep the shipped
    /// name. Every entry marked <see cref="CatalogueEntry.ConsentCookie"/> is renamed; a catalogue
    /// with none is left alone, which is what an override with no consent entry gets.
    /// </para>
    /// </remarks>
    public CookieCatalogue WithConsentCookieNamed(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return this;
        }

        string wanted = name.Trim();

        if (Entries.Any(entry => entry.ConsentCookie && entry.Pattern != wanted) is false)
        {
            return this;
        }

        return new CookieCatalogue(
            UnknownCategory,
            [.. Entries.Select(entry => entry.ConsentCookie
                ? entry with { Pattern = wanted }
                : entry)]);
    }

    /// <summary>
    /// The best matching entry for <paramref name="name"/>, or null when nothing matches.
    /// </summary>
    /// <remarks>
    /// Most specific wins: fewest characters absorbed by wildcards, then the longer literal
    /// prefix. Returning null rather than a guess is what routes an unrecognised cookie into the
    /// needs-review path instead of a confident-looking wrong declaration.
    /// </remarks>
    public CatalogueEntry? Match(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return Entries
            .Where(entry => CookieNameMatcher.Matches(entry.Pattern, name))
            .OrderBy(entry => CookieNameMatcher.WildcardCharCount(entry.Pattern, name))
            .ThenByDescending(entry => CookieNameMatcher.LiteralPrefixLength(entry.Pattern))
            .FirstOrDefault();
    }

    private sealed record Document(
        [property: JsonPropertyName("unknownCategory")] string? UnknownCategory,
        [property: JsonPropertyName("entries")] IReadOnlyList<CatalogueEntry>? Entries);
}
