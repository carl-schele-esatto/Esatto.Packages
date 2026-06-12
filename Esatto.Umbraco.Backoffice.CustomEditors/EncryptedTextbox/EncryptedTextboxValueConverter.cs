using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;

namespace Esatto.Umbraco.Backoffice.CustomEditors.EncryptedTextbox;

/// <summary>
/// Decrypts the stored ciphertext for the published value, so `Model.Value(alias)` returns
/// plaintext on the front-end (transparent decryption).
/// </summary>
public class EncryptedTextboxValueConverter : PropertyValueConverterBase
{
    private readonly IValueEncryptor _encryptor;

    public EncryptedTextboxValueConverter(IValueEncryptor encryptor) => _encryptor = encryptor;

    public override bool IsConverter(IPublishedPropertyType propertyType)
        => propertyType.EditorAlias == "Esatto.Umbraco.Backoffice.CustomEditors.EncryptedTextbox";

    public override Type GetPropertyValueType(IPublishedPropertyType propertyType)
        => typeof(string);

    public override PropertyCacheLevel GetPropertyCacheLevel(IPublishedPropertyType propertyType)
        => PropertyCacheLevel.Element;

    public override object? ConvertSourceToIntermediate(
        IPublishedElement owner,
        IPublishedPropertyType propertyType,
        object? source,
        bool preview)
        => _encryptor.Decrypt(source as string);
}
