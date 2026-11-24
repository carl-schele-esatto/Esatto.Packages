using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;

namespace Esatto.Umbraco.Backoffice.SeoReport.Settings;

/// <summary>
/// Finds the service-account key in force and turns it into a <see cref="GoogleCredential"/>, or says why it cannot: the one
/// place that reads the key from configuration, a key file or the database, and the one place that writes it. Precedence is
/// the configured JSON, then the configured key file, then the stored (encrypted) key; the first one that is set decides,
/// even when it turns out to be unusable, so a broken configured key is reported instead of silently replaced by an old one.
/// <para>
/// The key text exists only inside the methods here: what leaves is a <see cref="GoogleCredential"/> (a
/// <see cref="ResolvedKey"/>), the ciphertext for the database, or a fixed code. Nothing is logged, and every exception from
/// the file system, the JSON parser, Data Protection and Google's loader is caught and dropped, because their messages can
/// quote the text they choked on.
/// </para>
/// </summary>
internal sealed class KeyVault(ISeoReportStore store, SecretProtector protector, IConfiguration configuration)
{
    public const string JsonSetting = SeoReportSettings.SectionName + ":ServiceAccountJson";
    public const string FileSetting = SeoReportSettings.SectionName + ":ServiceAccountKeyFile";

    private const int StoredFormatVersion = 1;

    // A key file is read only up to this many bytes (see ReadKeyFileAsync). A key is at most ServiceAccountKey.MaxBytes in UTF-8;
    // the allowance covers the same key saved as UTF-16 with a BOM and some whitespace.
    private const int MaxFileBytes = 4 * ServiceAccountKey.MaxBytes;

    // The last usable key and its credential. Building a credential parses the private key, and the Google services are cached per
    // credential instance, so the same key must give the same instance on every look: see FromTextAsync.
    private volatile LastGood? _lastGood;

    /// <summary>The key in force, with the credential built from it, or the code that says why there is none. Never throws for a bad key.</summary>
    public async Task<ResolvedKey> ResolveAsync(CancellationToken ct)
    {
        var resolved = await ResolveCoreAsync(ct);

        // The remembered credential is dropped when the key is gone or replaced by a broken one. It is kept when the key merely
        // could not be read this time (a file locked or briefly missing): nothing says the key changed, and the same key coming
        // back should not cost a new credential.
        if (!resolved.HasKey && resolved.ErrorCode != KeyErrorCodes.KeyUnreadable) _lastGood = null;

        return resolved;
    }

    private async Task<ResolvedKey> ResolveCoreAsync(CancellationToken ct)
    {
        var json = configuration[JsonSetting];
        if (!string.IsNullOrWhiteSpace(json)) return await FromTextAsync(KeySourceKind.Configuration, json, ct);

        // The key written in appsettings.json as an object (the downloaded file is JSON, so it is an easy mistake) leaves no
        // string here, only children (ServiceAccountJson:type, ...). It is set, so it decides, and it is not a usable key.
        if (configuration.GetSection(JsonSetting).GetChildren().Any())
        {
            return ResolvedKey.Failed(KeySourceKind.Configuration, KeyErrorCodes.KeyInvalid);
        }

        var file = configuration[FileSetting];
        if (!string.IsNullOrWhiteSpace(file)) return await FromFileAsync(file.Trim(), ct);

        var document = await store.GetDocumentAsync(DocumentKeys.ProtectedKey);
        return document is null ? ResolvedKey.NotConfigured : await FromDocumentAsync(document, ct);
    }

    /// <summary>
    /// Checks a pasted key and returns the <c>protectedKey</c> document to store: the normalized key, encrypted. The key must be a
    /// service-account key the loader accepts, so one with a damaged private key is refused here instead of being stored and
    /// failing later. Throws <see cref="SettingsValidationException"/> with <see cref="ServiceAccountKey.InvalidCode"/> and
    /// nothing else when it is not usable.
    /// </summary>
    public async Task<string> ProtectForStorageAsync(string pasted, CancellationToken ct)
    {
        var prepared = await PrepareAsync(pasted, ct) ?? throw new SettingsValidationException(KeyErrorCodes.KeyInvalid);
        return ToDocument(protector.Protect(prepared.Json));
    }

    /// <summary>The <c>protectedKey</c> document for <paramref name="cipher"/>: JSON with a format version, so the format can change.</summary>
    internal static string ToDocument(string cipher)
        => JsonSerializer.Serialize(new StoredKey { Version = StoredFormatVersion, Cipher = cipher }, JsonSerializerOptions.Web);

    private async Task<ResolvedKey> FromDocumentAsync(string document, CancellationToken ct)
    {
        if (!TryReadCipher(document, out var cipher) || !protector.TryUnprotect(cipher, out var plain))
        {
            return ResolvedKey.Failed(KeySourceKind.Database, KeyErrorCodes.KeyUnreadable);
        }

        return await FromTextAsync(KeySourceKind.Database, plain, ct);
    }

    private async Task<ResolvedKey> FromFileAsync(string path, CancellationToken ct)
    {
        string? text;
        try
        {
            text = await ReadKeyFileAsync(path, MaxFileBytes, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
        {
            // Missing file or folder, a directory, a sharing violation, no permission, a malformed path: all "cannot be read".
            // The message names the path, which is not worth echoing.
            return ResolvedKey.Failed(KeySourceKind.Configuration, KeyErrorCodes.KeyUnreadable);
        }

        return text is null
            ? ResolvedKey.Failed(KeySourceKind.Configuration, KeyErrorCodes.KeyInvalid)
            : await FromTextAsync(KeySourceKind.Configuration, text, ct);
    }

    /// <summary>
    /// The text of the file at <paramref name="path"/>, or null when it is longer than <paramref name="maxBytes"/>. The file is opened
    /// once and read through that one stream, never more than <paramref name="maxBytes"/> + 1 bytes of it, so nothing is checked first
    /// and read later. A UTF-8 or UTF-16 BOM decides the encoding (UTF-8 otherwise), as <see cref="File.ReadAllTextAsync(string, CancellationToken)"/>
    /// does. The bytes read are wiped before returning. File-system failures are thrown for the caller to report.
    /// </summary>
    internal static async Task<string?> ReadKeyFileAsync(string path, int maxBytes, CancellationToken ct)
    {
        var buffer = new byte[maxBytes + 1];
        try
        {
            var length = 0;
            await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, useAsync: true))
            {
                int read;
                while (length < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(length), ct)) > 0) length += read;
            }

            if (length > maxBytes) return null;

            using var reader = new StreamReader(new MemoryStream(buffer, 0, length, writable: false), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return await reader.ReadToEndAsync(ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    /// <summary>
    /// The result for key text found in <paramref name="source"/>. The key is normalized first (so the same key pasted in another
    /// shape is the same key) and fingerprinted; when it matches the last usable key from the same source, that key's result is
    /// returned as it is, the very same <see cref="GoogleCredential"/> instance, and no new credential is built. Only the 32-byte
    /// fingerprint is remembered, never the key text.
    /// </summary>
    private async Task<ResolvedKey> FromTextAsync(KeySourceKind source, string text, CancellationToken ct)
    {
        if (Normalize(text) is not { } key) return ResolvedKey.Failed(source, KeyErrorCodes.KeyInvalid);

        var fingerprint = Fingerprint(key.Json);
        if (_lastGood is { } last && last.Source == source && CryptographicOperations.FixedTimeEquals(last.Fingerprint, fingerprint))
        {
            return last.Key;
        }

        var credential = await ServiceAccountCredentials.TryCreateAsync(key.Json, ct);
        if (credential is null) return ResolvedKey.Failed(source, KeyErrorCodes.KeyInvalid);

        var resolved = ResolvedKey.Ok(source, key.ClientEmail, credential);
        _lastGood = new LastGood(source, fingerprint, resolved);
        return resolved;
    }

    /// <summary>Validates and normalizes <paramref name="text"/> and builds the credential from the normalized JSON; null when any step refuses it.</summary>
    private static async Task<PreparedKey?> PrepareAsync(string text, CancellationToken ct)
    {
        if (Normalize(text) is not { } key) return null;

        var credential = await ServiceAccountCredentials.TryCreateAsync(key.Json, ct);
        return credential is null ? null : new PreparedKey(key.Json, key.ClientEmail, credential);
    }

    /// <summary>The normalized key and its service account's email, or null when <paramref name="text"/> is not a usable service-account key.</summary>
    private static (string Json, string ClientEmail)? Normalize(string text)
    {
        var check = ServiceAccountKey.Validate(text);
        return check.IsValid && check.ClientEmail is { } email && ServiceAccountKey.TryNormalize(text, out var normalized)
            ? (normalized, email)
            : null;
    }

    /// <summary>SHA-256 of the key's UTF-8 bytes. The byte copy of the secret is wiped as soon as it has been hashed.</summary>
    private static byte[] Fingerprint(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        try
        {
            return SHA256.HashData(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static bool TryReadCipher(string document, out string cipher)
    {
        cipher = "";
        try
        {
            var stored = JsonSerializer.Deserialize<StoredKey>(document, JsonSerializerOptions.Web);
            if (stored is not { Version: StoredFormatVersion, Cipher: { Length: > 0 } text }) return false;

            cipher = text;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // A plain class, not a record: a record's generated ToString would print the key. This one prints its type name.
    private sealed class PreparedKey(string json, string clientEmail, GoogleCredential credential)
    {
        /// <summary>The normalized key. A secret.</summary>
        public string Json { get; } = json;

        public string ClientEmail { get; } = clientEmail;

        public GoogleCredential Credential { get; } = credential;
    }

    // Only the hash of the key is kept, with the result built from it; a plain class so no generated ToString prints anything.
    private sealed class LastGood(KeySourceKind source, byte[] fingerprint, ResolvedKey key)
    {
        public KeySourceKind Source { get; } = source;

        public byte[] Fingerprint { get; } = fingerprint;

        public ResolvedKey Key { get; } = key;
    }

    private sealed class StoredKey
    {
        public int Version { get; set; }

        public string? Cipher { get; set; }
    }
}

/// <summary>
/// The outcome of looking for the key: the credential and the service account's email when there is a usable key, otherwise a
/// <see cref="KeyErrorCodes"/> code. A class with its own <c>ToString()</c>, so nothing prints the credential.
/// </summary>
internal sealed class ResolvedKey
{
    public static ResolvedKey NotConfigured { get; } = new(KeySourceKind.None, null, null, KeyErrorCodes.NotConfigured);

    private ResolvedKey(KeySourceKind source, string? clientEmail, GoogleCredential? credential, string? errorCode)
    {
        Source = source;
        ClientEmail = clientEmail;
        Credential = credential;
        ErrorCode = errorCode;
    }

    public KeySourceKind Source { get; }

    public string? ClientEmail { get; }

    public GoogleCredential? Credential { get; }

    /// <summary>Null when there is a usable key.</summary>
    public string? ErrorCode { get; }

    public bool HasKey => Credential is not null;

    /// <summary>A key is present but cannot be used.</summary>
    public bool Unreadable => ErrorCode is KeyErrorCodes.KeyUnreadable or KeyErrorCodes.KeyInvalid;

    public static ResolvedKey Ok(KeySourceKind source, string clientEmail, GoogleCredential credential)
        => new(source, clientEmail, credential, null);

    public static ResolvedKey Failed(KeySourceKind source, string errorCode) => new(source, null, null, errorCode);

    public override string ToString() => $"{Source}: {ErrorCode ?? "ok"}";
}
