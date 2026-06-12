using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;

namespace Esatto.Umbraco.Backoffice.CustomEditors.EncryptedTextbox;

/// <summary>
/// Server-side schema for the Encrypted Textbox. Stores a string (ciphertext). The matching
/// Property Editor UI references this alias via `propertyEditorSchemaAlias`.
/// </summary>
[DataEditor(
    alias: EditorAlias,
    ValueType = ValueTypes.String)]
public sealed class EncryptedTextboxDataEditor : DataEditor
{
    /// <summary>
    /// The shared property-editor schema alias. Single source of truth for both the
    /// <see cref="DataEditorAttribute"/> here and the value converter's IsConverter check.
    /// </summary>
    public const string EditorAlias = "Esatto.Umbraco.Backoffice.CustomEditors.EncryptedTextbox";

    public EncryptedTextboxDataEditor(IDataValueEditorFactory dataValueEditorFactory)
        : base(dataValueEditorFactory)
    {
    }

    protected override IDataValueEditor CreateValueEditor()
        => DataValueEditorFactory.Create<EncryptedTextboxValueEditor>(Attribute!);
}
