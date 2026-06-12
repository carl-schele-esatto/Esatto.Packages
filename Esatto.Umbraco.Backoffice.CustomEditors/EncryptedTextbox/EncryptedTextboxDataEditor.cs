using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;

namespace Esatto.Umbraco.Backoffice.CustomEditors.EncryptedTextbox;

/// <summary>
/// Server-side schema for the Encrypted Textbox. Stores a string (ciphertext). The matching
/// Property Editor UI references this alias via `propertyEditorSchemaAlias`.
/// </summary>
[DataEditor(
    alias: "Esatto.Umbraco.Backoffice.CustomEditors.EncryptedTextbox",
    ValueType = ValueTypes.String)]
public class EncryptedTextboxDataEditor : DataEditor
{
    private readonly IDataValueEditorFactory _dataValueEditorFactory;

    public EncryptedTextboxDataEditor(IDataValueEditorFactory dataValueEditorFactory)
        : base(dataValueEditorFactory)
    {
        _dataValueEditorFactory = dataValueEditorFactory;
    }

    protected override IDataValueEditor CreateValueEditor()
        => _dataValueEditorFactory.Create<EncryptedTextboxValueEditor>(Attribute!);
}
