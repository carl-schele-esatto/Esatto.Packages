namespace Esatto.Umbraco.Backoffice.SeoReport.Persistence;

/// <summary>
/// The keys of the package's JSON documents (see <see cref="DocumentEntity"/>). Each document's shape belongs to the
/// service that owns it; the names live here so no two services can pick the same one by accident.
/// </summary>
public static class DocumentKeys
{
    /// <summary>The editable settings (everything in <c>SeoReportSettings</c>; no secret).</summary>
    public const string Settings = "settings";

    /// <summary>The service-account key, as Data Protection ciphertext only.</summary>
    public const string ProtectedKey = "protectedKey";

    /// <summary>The follow-up targets.</summary>
    public const string Targets = "targets";

    /// <summary>What the last fetches did, so a restart does not lose it.</summary>
    public const string FetchState = "fetchState";
}
