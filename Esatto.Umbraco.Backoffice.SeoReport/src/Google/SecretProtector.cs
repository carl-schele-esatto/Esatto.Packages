using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Esatto.Umbraco.Backoffice.SeoReport.Google;

/// <summary>
/// Encrypts the service-account key for storage with ASP.NET Core Data Protection, so the database only ever holds
/// ciphertext that is useless without the site's key ring. A ciphertext that cannot be read (a restored backup, a key ring
/// that was lost or rotated away) is reported as <see langword="false"/>, never thrown, and neither method logs or puts
/// the text in a message.
/// </summary>
public sealed class SecretProtector(IDataProtectionProvider provider)
{
    /// <summary>
    /// The Data Protection purpose. It is part of the encryption key, so changing it makes every stored key unreadable;
    /// bump the <c>v1</c> only together with a migration.
    /// </summary>
    public const string Purpose = "Esatto.Umbraco.Backoffice.SeoReport.ServiceAccountKey.v1";

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    /// <summary>Encrypts <paramref name="plain"/> to URL-safe text. Two calls on the same text give different results.</summary>
    public string Protect(string plain)
    {
        ArgumentNullException.ThrowIfNull(plain);
        return _protector.Protect(plain);
    }

    /// <summary>
    /// Decrypts <paramref name="cipher"/>. Returns <see langword="false"/> with <c>""</c> when it is null or empty, is not
    /// a ciphertext, was tampered with, or was produced by another key ring or purpose.
    /// </summary>
    public bool TryUnprotect([NotNullWhen(true)] string? cipher, out string plain)
    {
        plain = "";
        if (string.IsNullOrEmpty(cipher)) return false;

        try
        {
            plain = _protector.Unprotect(cipher);
            return true;
        }
        catch (CryptographicException)
        {
            // Data Protection's text API reports every failure (bad base64, a payload that is not ours, another key ring or purpose,
            // tampering, and key storage that cannot be reached or read) as a CryptographicException: it wraps whatever else was
            // thrown inside. The tests pin that, over the real stack with unreachable key storage too, so a broken key ring is
            // keyUnreadable and never an error. Its message is dropped: it is not needed.
            return false;
        }
    }
}
