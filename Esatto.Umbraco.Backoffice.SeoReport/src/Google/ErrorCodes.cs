using System.Text;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;

namespace Esatto.Umbraco.Backoffice.SeoReport.Google;

/// <summary>
/// The fixed words that say what went wrong, for the report, the fetch progress and Test connection. The server sends a code
/// (and a few fixed arguments, see <see cref="GoogleErrorMapper"/>), never prose: the English wording of each is in
/// <c>Client/src/lang/en.ts</c>, one title and one text per code, registered in <c>ReportKeys</c> as area <c>error</c>.
///
/// The key of a code is <c>error</c> + the code with its first letter capitalised + <c>Title</c> or <c>Text</c>
/// (<c>apiDisabled</c> gives <c>errorApiDisabledTitle</c>), which is exactly what <see cref="BaseId"/> and
/// <see cref="Reporting.ReportKeys.Lookup"/> make of it, so the client can build the key from the code alone.
/// </summary>
public static class ErrorCodes
{
    /// <summary>No key anywhere (see <see cref="KeyErrorCodes"/>).</summary>
    public const string NotConfigured = KeyErrorCodes.NotConfigured;

    /// <summary>A key is saved but cannot be read in this environment.</summary>
    public const string KeyUnreadable = KeyErrorCodes.KeyUnreadable;

    /// <summary>The text is not a usable service-account key.</summary>
    public const string KeyInvalid = KeyErrorCodes.KeyInvalid;

    /// <summary>Google refused because the API is not enabled in the service account's project. Carries <c>api</c>.</summary>
    public const string ApiDisabled = "apiDisabled";

    /// <summary>The service account has not been given access to the source. Carries <c>source</c>.</summary>
    public const string NoAccess = "noAccess";

    /// <summary>The GA4 property or the Search Console property does not exist as written. Carries <c>source</c>.</summary>
    public const string PropertyNotFound = "propertyNotFound";

    /// <summary>Connected, but the source has nothing yet. Not a failure. Carries <c>source</c>.</summary>
    public const string NoData = "noData";

    /// <summary>Google's request limit was reached.</summary>
    public const string Quota = "quota";

    /// <summary>Anything else. The detail goes to the log, never to the screen.</summary>
    public const string Unknown = "unknown";

    /// <summary>The GA4 property ID is missing or not in a form Google accepts (a settings refusal, and the same code in Test connection).</summary>
    public const string Ga4PropertyInvalid = "ga4PropertyInvalid";

    /// <summary>The Search Console property is missing or not in a form Search Console accepts.</summary>
    public const string SearchConsoleInvalid = "searchConsoleInvalid";

    /// <summary>Every code, in the order the texts are written. Each one has a title and a text in en.ts.</summary>
    public static IReadOnlyList<string> All { get; } =
        [NotConfigured, KeyUnreadable, KeyInvalid, ApiDisabled, NoAccess, PropertyNotFound, NoData, Quota, Unknown, Ga4PropertyInvalid, SearchConsoleInvalid];

    /// <summary>Whether <paramref name="code"/> is one of <see cref="All"/>.</summary>
    public static bool IsKnown(string? code) => code is not null && All.Contains(code, StringComparer.Ordinal);

    /// <summary>
    /// The id a code is registered under in <c>ReportKeys</c>: the code in snake_case (<c>apiDisabled</c> gives
    /// <c>api_disabled</c>, <c>ga4PropertyInvalid</c> gives <c>ga4_property_invalid</c>).
    /// </summary>
    public static string BaseId(string code)
    {
        var id = new StringBuilder(code.Length + 4);
        for (var i = 0; i < code.Length; i++)
        {
            if (char.IsAsciiLetterUpper(code[i]) && i > 0) id.Append('_');
            id.Append(char.ToLowerInvariant(code[i]));
        }

        return id.ToString();
    }
}
