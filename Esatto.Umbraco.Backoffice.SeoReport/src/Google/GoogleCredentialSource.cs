using System.Text;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Google.Apis.AnalyticsData.v1beta;
using Google.Apis.Auth.OAuth2;
using Google.Apis.SearchConsole.v1;

namespace Esatto.Umbraco.Backoffice.SeoReport.Google;

/// <summary>
/// Where the Google calls get their credential. It hands back a <see cref="GoogleCredential"/> and never the key as text, so
/// the secret does not travel through signatures.
/// </summary>
public interface IGoogleCredentialSource
{
    /// <summary>The credential for the key in force, or the reason there is none. A bad key is a result, not an exception.</summary>
    Task<CredentialResult> GetAsync(CancellationToken ct = default);
}

/// <summary>
/// The outcome of asking for the credential. It carries the credential, the service account's email (public: it is what the
/// person adds in Google Analytics and Search Console), where the key came from and, when there is no credential, one of the
/// fixed codes <c>notConfigured</c>, <c>keyUnreadable</c> or <c>keyInvalid</c> (see <see cref="KeyErrorCodes"/>). Nothing
/// of the key is a property, and the credential's own <c>ToString()</c> prints only its type name.
/// </summary>
/// <param name="Credential">The scoped credential, or null.</param>
/// <param name="ClientEmail">The service account's email when there is a credential, otherwise null.</param>
/// <param name="Source">Where the key in force comes from.</param>
/// <param name="ErrorCode">Null when there is a credential, otherwise why not.</param>
public sealed record CredentialResult(GoogleCredential? Credential, string? ClientEmail, KeySourceKind Source, string? ErrorCode);

/// <summary>
/// Builds the credential from the key in force, in this order: configuration <c>ServiceAccountJson</c>, configuration
/// <c>ServiceAccountKeyFile</c>, then the encrypted key saved in the Settings tab (see <see cref="KeyVault"/>). The result is
/// cached for a minute, and saving or removing a key (or any setting) makes the next call resolve it again at once, so a new
/// key works with no restart. Resolving again does not mean a new credential: while the key and where it comes from stay the same,
/// the very same <see cref="CredentialResult"/> and <see cref="GoogleCredential"/> come back (the key is fingerprinted, see
/// <see cref="KeyVault"/>), because the Google services are cached per credential instance and should be replaced only when the
/// key really changes. A failure result is never kept: it is built fresh each time.
/// </summary>
public sealed class GoogleCredentialSource(SettingsService settings, TimeProvider time) : IGoogleCredentialSource
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(1);

    private volatile CacheEntry? _cache;

    // The last result that carried a credential. It is handed out again when the key resolves to that same credential.
    private volatile CredentialResult? _lastUsable;

    public async Task<CredentialResult> GetAsync(CancellationToken ct = default)
    {
        var generation = settings.Generation;
        if (_cache is { } cached && cached.Generation == generation && time.GetUtcNow() < cached.Until) return cached.Value;

        var key = await settings.Keys.ResolveAsync(ct);
        var result = _lastUsable is { } previous && key.Credential is not null && ReferenceEquals(previous.Credential, key.Credential)
            ? previous
            : new CredentialResult(key.Credential, key.ClientEmail, key.Source, key.ErrorCode);
        _lastUsable = result.Credential is null ? null : result;
        _cache = new CacheEntry(result, time.GetUtcNow() + CacheFor, generation);
        return result;
    }

    private sealed record CacheEntry(CredentialResult Value, DateTimeOffset Until, int Generation);
}

/// <summary>Builds the scoped credential from a normalized service-account key.</summary>
internal static class ServiceAccountCredentials
{
    /// <summary>
    /// The two read-only scopes the package needs: the Google Analytics Data API (<c>analytics.readonly</c>) and Search
    /// Console (<c>webmasters.readonly</c>). One credential serves both clients.
    /// </summary>
    public static readonly string[] Scopes =
    [
        AnalyticsDataService.Scope.AnalyticsReadonly,
        SearchConsoleService.Scope.WebmastersReadonly,
    ];

    /// <summary>
    /// The credential for <paramref name="normalizedJson"/>, or null when Google's loader refuses it (for example a private key
    /// that is not RSA). Nothing is requested from Google here: the key is parsed locally and a token is fetched on first use.
    /// </summary>
    public static async Task<GoogleCredential?> TryCreateAsync(string normalizedJson, CancellationToken ct)
    {
        try
        {
            // CredentialFactory through a stream, not the obsolete GoogleCredential.FromJson (CS0618). The JSON is the normalized
            // key, so a private key pasted with escaped newlines has already been repaired: a PEM with literal "\n" cannot be parsed.
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(normalizedJson));
            var account = await CredentialFactory.FromStreamAsync<ServiceAccountCredential>(stream, ct).ConfigureAwait(false);
            return account.ToGoogleCredential().CreateScoped(Scopes);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The loader throws a mix of types (JSON, format, cryptography, invalid data) depending on what is wrong, and their
            // messages can quote the text they choked on. All of them mean "not a usable key"; none is kept or logged.
            return null;
        }
    }
}
