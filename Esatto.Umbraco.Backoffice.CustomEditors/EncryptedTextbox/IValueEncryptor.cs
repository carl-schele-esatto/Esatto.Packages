namespace Esatto.Umbraco.Backoffice.CustomEditors.EncryptedTextbox;

/// <summary>Encrypts/decrypts a string value for storage at rest.</summary>
public interface IValueEncryptor
{
    /// <summary>Encrypts plaintext for storage. Null/empty values pass through; everything else is always encrypted.</summary>
    string? Encrypt(string? value);

    /// <summary>Decrypts a stored value. Null/empty and unmarked (legacy plaintext) values pass through.</summary>
    string? Decrypt(string? value);

    /// <summary>
    /// True iff the value is marked ciphertext that cannot currently be decrypted (e.g. the Data
    /// Protection key ring was lost or rotated, or the payload is corrupt). False for null/empty,
    /// unmarked plaintext, and ciphertext that decrypts successfully. Used to avoid destroying
    /// recoverable-once-keys-return ciphertext on save.
    /// </summary>
    bool IsUndecryptable(string? value);
}
