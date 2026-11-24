using System.Text.Json.Serialization;
using Esatto.Umbraco.Backoffice.SeoReport.Google;

namespace Esatto.Umbraco.Backoffice.SeoReport.Settings;

/// <summary>Where the service-account key in force comes from.</summary>
public enum KeySourceKind
{
    /// <summary>There is no key.</summary>
    None,

    /// <summary>Configuration: <c>Esatto:SeoReport:ServiceAccountJson</c> or <c>Esatto:SeoReport:ServiceAccountKeyFile</c>. Read-only in the dashboard.</summary>
    Configuration,

    /// <summary>Pasted in the Settings tab and stored encrypted.</summary>
    Database,
}

/// <summary>
/// What the dashboard may know about the key: whether a usable one is in force, where it comes from, and the service
/// account's email (public: it is the address to grant access to in Google Analytics and Search Console). The key itself is
/// never a property, because this is a record and its generated <c>ToString()</c> prints every property.
/// </summary>
/// <param name="HasKey">Whether a usable key is in force.</param>
/// <param name="Source">Where the key in force comes from; <see cref="KeySourceKind.None"/> when there is none.</param>
/// <param name="ServiceAccountEmail">The service account's email when <paramref name="HasKey"/>, otherwise null.</param>
/// <param name="Unreadable">
/// Whether a key is present but unusable: it cannot be decrypted (a restored backup, a changed key ring), or it is not a valid
/// service-account key, or its file cannot be read. <paramref name="HasKey"/> is false then, and the person has to enter it again
/// (a stored key) or fix the configuration.
/// </param>
public sealed record KeyStatus(
    bool HasKey,
    [property: JsonConverter(typeof(CamelCaseEnumConverter<KeySourceKind>))] KeySourceKind Source,
    string? ServiceAccountEmail,
    bool Unreadable);

/// <summary>The codes that say why there is no usable key. They are fixed words, never text taken from the key or its file.</summary>
public static class KeyErrorCodes
{
    /// <summary>No key anywhere.</summary>
    public const string NotConfigured = "notConfigured";

    /// <summary>A key is there but cannot be read: a ciphertext this environment cannot decrypt, or a key file that cannot be opened.</summary>
    public const string KeyUnreadable = "keyUnreadable";

    /// <summary>The text was read but is not a usable service-account key.</summary>
    public const string KeyInvalid = ServiceAccountKey.InvalidCode;
}
