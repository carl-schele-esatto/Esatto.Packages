using System.Text.Json;
using System.Text.Json.Serialization;

namespace Esatto.Umbraco.Backoffice.Seo.Knowledge;

/// <summary>What the dashboard says about a check: why it matters, how to fix it, caveats.</summary>
public sealed record CheckText(string Title, string Why, string Fix, string? Caveats, string? LearnMore);

/// <summary>
/// The explanatory texts for every check, shipped as embedded JSON (<c>Knowledge/checks.en.json</c>).
/// Keyed by culture in the file name so other languages can be added without code changes.
/// </summary>
public sealed class KnowledgeBase
{
    /// <summary>Serializer options shared by the package: camelCase, enums as camelCase strings.</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private const string ResourceName = "Esatto.Umbraco.Backoffice.Seo.Knowledge.checks.en.json";

    public KnowledgeBase()
    {
        using var stream = typeof(KnowledgeBase).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} is missing.");
        All = JsonSerializer.Deserialize<Dictionary<string, CheckText>>(stream, Json) ?? [];
    }

    public IReadOnlyDictionary<string, CheckText> All { get; }

    /// <summary>The text for <paramref name="checkId"/>; a generic text when there is none, never throws.</summary>
    public CheckText Get(string checkId)
        => All.TryGetValue(checkId, out var text)
            ? text
            : new CheckText(checkId, "This check found something worth a look.", "Open the page and review the finding.", null, null);
}
