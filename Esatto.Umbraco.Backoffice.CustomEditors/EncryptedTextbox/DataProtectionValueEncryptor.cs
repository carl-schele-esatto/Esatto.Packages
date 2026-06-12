using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Esatto.Umbraco.Backoffice.CustomEditors.EncryptedTextbox;

/// <summary>
/// Encrypts values using ASP.NET Core Data Protection. Stored values are prefixed with a marker
/// so unmarked (legacy/plaintext) values can be detected and passed through unchanged on read.
/// </summary>
public sealed class DataProtectionValueEncryptor : IValueEncryptor
{
    // Do NOT change this value — it is the Data Protection purpose; changing it makes ALL
    // previously-stored ciphertext permanently undecryptable. Intentionally decoupled from the
    // editor alias.
    internal const string Purpose = "Esatto.Umbraco.Backoffice.CustomEditors.EncryptedTextbox";
    internal const string Marker = "edp1:";

    private readonly IDataProtector _protector;
    private readonly ILogger<DataProtectionValueEncryptor> _logger;

    public DataProtectionValueEncryptor(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<DataProtectionValueEncryptor> logger)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
        _logger = logger;
    }

    public string? Encrypt(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        // No passthrough on the marker: FromEditor only ever receives decrypted plaintext, so a
        // value starting with the marker is user input and MUST be encrypted, not stored as-is.
        return Marker + _protector.Protect(value);
    }

    public string? Decrypt(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (!value.StartsWith(Marker, StringComparison.Ordinal)) return value; // legacy/plaintext

        var payload = value.Substring(Marker.Length);
        try
        {
            return _protector.Unprotect(payload);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            // CryptographicException: tampered/wrong-key/lost-key-ring payload.
            // FormatException: a marked value whose payload isn't valid base64url (e.g. manual DB edit).
            // Either way the value is unrecoverable: log and return null (never throw, never leak ciphertext).
            _logger.LogWarning(ex,
                "Failed to decrypt an EncryptedTextbox value; the Data Protection key ring may have changed or been lost.");
            return null;
        }
    }

    public bool IsUndecryptable(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        if (!value.StartsWith(Marker, StringComparison.Ordinal)) return false; // unmarked/legacy plaintext

        var payload = value.Substring(Marker.Length);
        try
        {
            _protector.Unprotect(payload);
            return false;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            // Marked ciphertext we can't currently read (lost/rotated key ring or corrupt payload).
            return true;
        }
    }
}
