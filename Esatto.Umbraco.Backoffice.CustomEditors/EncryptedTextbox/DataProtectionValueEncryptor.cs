using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Esatto.Umbraco.Backoffice.CustomEditors.EncryptedTextbox;

/// <summary>
/// Encrypts values using ASP.NET Core Data Protection. Stored values are prefixed with a marker
/// so unmarked (legacy/plaintext) values can be detected and passed through unchanged, and so a
/// value is never double-encrypted.
/// </summary>
public sealed class DataProtectionValueEncryptor : IValueEncryptor
{
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
        if (value.StartsWith(Marker, StringComparison.Ordinal)) return value; // already encrypted
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
}
