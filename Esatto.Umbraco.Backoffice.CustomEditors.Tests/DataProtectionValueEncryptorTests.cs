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
    public void Encrypt_encrypts_even_a_value_that_starts_with_the_marker()
    {
        var sut = CreateSut();
        const string input = "edp1:looks-like-ciphertext";

        var result = sut.Encrypt(input);

        Assert.NotNull(result);
        Assert.NotEqual(input, result);
        Assert.StartsWith("edp1:", result);
        Assert.Equal(input, sut.Decrypt(result));
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

    [Fact]
    public void IsUndecryptable_distinguishes_unreadable_ciphertext_from_everything_else()
    {
        var sut = CreateSut();

        // Marked but not a real protected payload -> can't decrypt -> true.
        Assert.True(sut.IsUndecryptable("edp1:AAAA"));

        // Unmarked plaintext -> false.
        Assert.False(sut.IsUndecryptable("plain"));

        // Null -> false.
        Assert.False(sut.IsUndecryptable(null));

        // A real, decryptable value -> false.
        var real = sut.Encrypt("super-secret-api-key-123");
        Assert.False(sut.IsUndecryptable(real));
    }
}
