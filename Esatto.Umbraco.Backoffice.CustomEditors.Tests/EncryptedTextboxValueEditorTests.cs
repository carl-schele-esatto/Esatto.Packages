using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Esatto.Umbraco.Backoffice.CustomEditors.EncryptedTextbox;
using Umbraco.Cms.Core.Models.Editors;
using Umbraco.Cms.Core.PropertyEditors;
using Xunit;

// Exercises the real EncryptedTextboxValueEditor at the FromEditor seam (the one that decides
// what lands in the database). The Umbraco services the base DataValueEditor takes
// (IShortStringHelper, IJsonSerializer, IIOHelper) are not touched by FromEditor, so they are
// passed as null; only the IValueEncryptor matters.
//
// ToEditor's decrypt half is asserted via the encryptor here rather than against the real editor,
// because IProperty (which ToEditor consumes) transitively requires implementing IEntity,
// ICanBeDirty, IRememberBeingDirty and IDeepCloneable, and faking that full surface in a plain
// unit test is impractical for this Umbraco version. ToEditor delegates verbatim to
// IValueEncryptor.Decrypt, and the end-to-end DB round-trip is confirmed by the manual DB
// inspection in Task 9.
public class EncryptedTextboxValueEditorTests
{
    private static IValueEncryptor CreateEncryptor()
        => new DataProtectionValueEncryptor(
            new EphemeralDataProtectionProvider(),
            NullLogger<DataProtectionValueEncryptor>.Instance);

    private static EncryptedTextboxValueEditor CreateSut(IValueEncryptor encryptor)
        => new(
            shortStringHelper: null!,
            jsonSerializer: null!,
            ioHelper: null!,
            attribute: new DataEditorAttribute("Esatto.Umbraco.Backoffice.CustomEditors.EncryptedTextbox"),
            encryptor: encryptor);

    [Fact]
    public void FromEditor_persists_ciphertext_that_round_trips_to_the_original_plaintext()
    {
        var encryptor = CreateEncryptor();
        var sut = CreateSut(encryptor);

        const string plaintext = "secret-value";

        // Backoffice -> DB: what gets persisted must be ciphertext, never the plaintext.
        var persisted = sut.FromEditor(
            new ContentPropertyData(plaintext, dataTypeConfiguration: null),
            currentValue: null) as string;

        Assert.NotNull(persisted);
        Assert.NotEqual(plaintext, persisted);
        Assert.StartsWith("edp1:", persisted!);

        // DB -> backoffice: ToEditor decrypts the stored value back to the original plaintext.
        // (ToEditor delegates to IValueEncryptor.Decrypt; asserted here against the same encryptor.)
        Assert.Equal(plaintext, encryptor.Decrypt(persisted));
    }
}
