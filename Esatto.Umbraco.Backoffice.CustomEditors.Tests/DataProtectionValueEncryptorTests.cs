using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Esatto.Umbraco.Backoffice.CustomEditors.EncryptedTextbox;
using Xunit;

public class DataProtectionValueEncryptorTests
{
    private static DataProtectionValueEncryptor CreateSut()
        => new(new EphemeralDataProtectionProvider(),
               NullLogger<DataProtectionValueEncryptor>.Instance);

    [Fact]
    public void Encrypt_produces_ciphertext_that_is_not_the_plaintext()
    {
        var sut = CreateSut();
        const string plaintext = "super-secret-api-key-123";

        var encrypted = sut.Encrypt(plaintext);

        Assert.NotNull(encrypted);
        Assert.NotEqual(plaintext, encrypted);
        Assert.DoesNotContain(plaintext, encrypted!);
        Assert.StartsWith("edp1:", encrypted);
    }

    [Fact]
    public void Decrypt_round_trips_to_original_plaintext()
    {
        var sut = CreateSut();
        const string plaintext = "super-secret-api-key-123";
        var restored = sut.Decrypt(sut.Encrypt(plaintext));
        Assert.Equal(plaintext, restored);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Null_or_empty_passes_through_unchanged(string? value)
    {
        var sut = CreateSut();
        Assert.Equal(value, sut.Encrypt(value));
        Assert.Equal(value, sut.Decrypt(value));
    }

    [Fact]
    public void Decrypt_of_unmarked_value_passes_through_unchanged()
    {
        var sut = CreateSut();
        const string legacyPlaintext = "not-encrypted-legacy-value";
        Assert.Equal(legacyPlaintext, sut.Decrypt(legacyPlaintext));
    }

    [Fact]
    public void Encrypting_an_already_encrypted_value_does_not_double_encrypt()
    {
        var sut = CreateSut();
        var once = sut.Encrypt("secret");
        var twice = sut.Encrypt(once);
        Assert.Equal(once, twice);
    }

    [Theory]
    [InlineData("edp1:not-valid-base64!!!")]   // marked but garbage payload -> FormatException
    [InlineData("edp1:AAAA")]                    // marked but not a real protected payload -> CryptographicException
    public void Decrypt_of_a_corrupt_marked_value_returns_null_and_does_not_throw(string corrupt)
    {
        var sut = CreateSut();
        var result = sut.Decrypt(corrupt);
        Assert.Null(result);
    }
}
