namespace Esatto.Umbraco.Backoffice.CustomEditors.EncryptedTextbox;

/// <summary>Encrypts/decrypts a string value for storage at rest.</summary>
public interface IValueEncryptor
{
    /// <summary>Encrypts plaintext for storage. Null/empty and already-encrypted values pass through.</summary>
    string? Encrypt(string? value);

    /// <summary>Decrypts a stored value. Null/empty and unmarked (legacy plaintext) values pass through.</summary>
    string? Decrypt(string? value);
}
