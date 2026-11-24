using System.Text.Json.Serialization;
using Esatto.Umbraco.Backoffice.SeoReport.Google;

namespace Esatto.Umbraco.Backoffice.SeoReport.Settings;

/// <summary>
/// The Settings tab's data: the settings in force, the camelCase names of the fields configuration sets (shown read-only), and what
/// is known about the service-account key. The key itself is not here and is not anywhere a response can reach (see <see cref="KeyStatus"/>).
/// </summary>
/// <param name="Settings">The settings in force: the database's values with configuration on top.</param>
/// <param name="Locked">The fields configuration sets, by their camelCase names.</param>
/// <param name="Key">Whether a usable key is in force, where it comes from and whose it is.</param>
public sealed record SettingsResponse(SeoReportSettings Settings, string[] Locked, KeyStatus Key);

/// <summary>
/// What the Settings tab saves. <paramref name="ServiceAccountJson"/> is a SECRET and this is a record, so two things keep it from
/// leaving by accident: <see cref="ToString"/> is written by hand and prints neither the key nor the settings, and the key's JSON
/// converter reads it from a request body but writes <c>null</c>, so serializing a request cannot echo it into anything.
/// </summary>
/// <param name="Settings">Every editable setting. A field that configuration sets is ignored by the save.</param>
/// <param name="ServiceAccountJson">A new key, replacing the stored one; blank keeps the key as it is (see <paramref name="RemoveKey"/>).</param>
/// <param name="RemoveKey">Deletes the stored key when no new one is given.</param>
public sealed record PutSettingsRequest(
    SeoReportSettings Settings,
    [property: JsonConverter(typeof(InputOnlyStringConverter))] string? ServiceAccountJson,
    bool RemoveKey)
{
    public override string ToString()
        => $"{nameof(PutSettingsRequest)} {{ KeyGiven = {!string.IsNullOrWhiteSpace(ServiceAccountJson)}, RemoveKey = {RemoveKey} }}";
}
