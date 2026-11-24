using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Umbraco.Cms.Api.Common.Serialization;
using Umbraco.Cms.Api.Management.Serialization;
using Umbraco.Cms.Core.Composing;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>
/// The JSON the Management API really writes. Every endpoint of the controller inherits <c>[JsonOptionsName("BackOffice")]</c> from
/// <c>ManagementApiControllerBase</c>, and Umbraco configures those named options with
/// <see cref="ConfigureUmbracoBackofficeJsonOptions"/>: this builds them the same way, from the real configurator, on top of MVC's own
/// defaults (<see cref="JsonOptions"/> starts from <c>JsonSerializerDefaults.Web</c>), so a test sees what the browser sees.
///
/// WHAT THEY DO, found by reading that class: camelCase property names; <c>JsonStringEnumConverter()</c> with NO naming policy, so an
/// enum is written by its C# name ("Database", not "database") unless a property carries a converter of its own; no
/// <c>DefaultIgnoreCondition</c>, so a null property is written as <c>null</c>.
/// </summary>
internal static class ManagementJson
{
    public static JsonSerializerOptions Options { get; } = Build();

    /// <summary>Serializes <paramref name="value"/> by its runtime type, as the output formatter does for <c>Ok(value)</c>.</summary>
    public static string Serialize(object? value)
        => value is null ? "null" : JsonSerializer.Serialize(value, value.GetType(), Options);

    /// <summary>The JSON a client would receive for an action result (<c>ObjectResult.Value</c>), or an empty string for a result without a body.</summary>
    public static string Body(IActionResult result)
        => result is ObjectResult { Value: { } value } ? Serialize(value) : "";

    public static JsonDocument Parse(object? value) => JsonDocument.Parse(Serialize(value));

    /// <summary>
    /// The same options over another type finder. Umbraco's resolver asks the type finder for the classes that implement each
    /// INTERFACE it serializes and makes that interface polymorphic over them (see <c>HostTypesJsonTests</c>); by default the
    /// finder knows none, as in a host with no such class.
    /// </summary>
    public static JsonSerializerOptions Build(ITypeFinder? typeFinder = null)
    {
        var resolver = new UmbracoJsonTypeInfoResolver(typeFinder ?? Substitute.For<ITypeFinder>());

        var mvc = new JsonOptions();
        new ConfigureUmbracoBackofficeJsonOptions(resolver).Configure(mvc);
        return mvc.JsonSerializerOptions;
    }
}
