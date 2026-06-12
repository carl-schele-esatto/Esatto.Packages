using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;

namespace Esatto.Umbraco.Backoffice.CustomEditors.EncryptedTextbox;

/// <summary>
/// Decrypts the stored ciphertext for the published value, so `Model.Value(alias)` returns
/// plaintext on the front-end (transparent decryption).
/// </summary>
public sealed class EncryptedTextboxValueConverter : PropertyValueConverterBase
{
    private readonly IValueEncryptor _encryptor;

    public EncryptedTextboxValueConverter(IValueEncryptor encryptor) => _encryptor = encryptor;

    public override bool IsConverter(IPublishedPropertyType propertyType)
        => propertyType.EditorAlias == EncryptedTextboxDataEditor.EditorAlias;

    public override Type GetPropertyValueType(IPublishedPropertyType propertyType)
        => typeof(string);

    // Snapshot (not Element): decrypted plaintext is scoped to the request rather than held in the
    // long-lived element cache. The intermediate (raw stored ciphertext) uses the base passthrough.
    public override PropertyCacheLevel GetPropertyCacheLevel(IPublishedPropertyType propertyType)
        => PropertyCacheLevel.Snapshot;

    public override object? ConvertIntermediateToObject(
        IPublishedElement owner,
        IPublishedPropertyType propertyType,
        PropertyCacheLevel referenceCacheLevel,
        object? inter,
        bool preview)
        => _encryptor.Decrypt(inter as string);
}
