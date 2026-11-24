using System.Text.Json.Serialization;

namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

/// <summary>
/// A reference to a translated text. The server never sends prose: it sends a base id (for example
/// <c>noKeyEvents</c>) and the numbers or names the text needs. The client derives the translation key from the base id
/// and where the reference appears (area and part, see <see cref="ReportKeys"/>) and resolves it through Umbraco's
/// localization.
/// </summary>
/// <param name="Key">The text's base id, in the form <see cref="ReportKeys.Lookup"/> accepts.</param>
/// <param name="Args">
/// Values for the text's placeholders; omitted from the JSON when there are none. A concrete type, like every collection in an answer:
/// the Management API makes an interface-typed member polymorphic over whatever classes the site has that implement it, and a plain
/// dictionary then cannot be written (see <c>HostTypesJsonTests</c>).
/// </param>
public sealed record TextRef(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("args"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Dictionary<string, object?>? Args = null)
{
    /// <summary>A reference with its arguments as name/value pairs; none when there are none, so the JSON omits an empty "args".</summary>
    internal static TextRef Of(string key, params (string Name, object? Value)[] args)
        => new(key, args.Length == 0 ? null : args.ToDictionary(a => a.Name, a => a.Value, StringComparer.Ordinal));
}
