using System.Text.Json;
using System.Text.Json.Serialization;

namespace Esatto.Umbraco.Backoffice.SeoReport;

/// <summary>
/// Writes an enum as a camelCase word (<c>keyUnreadable</c>, <c>configuration</c>), which is how the dashboard reads them.
///
/// ON THE PROPERTY, NOT ON THE ENUM. The Management API's options carry a <c>JsonStringEnumConverter()</c> with no naming policy
/// (Umbraco's <c>ConfigureUmbracoBackofficeJsonOptions</c>), which writes the C# name (<c>KeyUnreadable</c>), and a converter in the
/// options wins over one that sits on the enum type. Only a converter on the PROPERTY wins over the options, so every response
/// property of an enum type carries this one explicitly, whatever the host is configured with.
/// </summary>
internal sealed class CamelCaseEnumConverter<TEnum>() : JsonStringEnumConverter<TEnum>(JsonNamingPolicy.CamelCase)
    where TEnum : struct, Enum;
