using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Editors;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Strings;

namespace Esatto.Umbraco.Backoffice.CustomEditors.EncryptedTextbox;

/// <summary>
/// Value editor that encrypts the value on its way to storage and decrypts it on its way back
/// into the backoffice editor. The database always holds ciphertext.
/// </summary>
public class EncryptedTextboxValueEditor : DataValueEditor
{
    private readonly IValueEncryptor _encryptor;

    public EncryptedTextboxValueEditor(
        IShortStringHelper shortStringHelper,
        IJsonSerializer jsonSerializer,
        IIOHelper ioHelper,
        DataEditorAttribute attribute,
        IValueEncryptor encryptor)
        : base(shortStringHelper, jsonSerializer, ioHelper, attribute)
    {
        _encryptor = encryptor;
    }

    /// <summary>DB -> backoffice: decrypt so the editor shows plaintext.</summary>
    public override object? ToEditor(IProperty property, string? culture = null, string? segment = null)
    {
        var stored = property.GetValue(culture, segment) as string;
        return _encryptor.Decrypt(stored);
    }

    /// <summary>Backoffice -> DB: encrypt the submitted plaintext.</summary>
    public override object? FromEditor(ContentPropertyData editorValue, object? currentValue)
    {
        var plaintext = editorValue.Value as string;
        return _encryptor.Encrypt(plaintext);
    }
}
