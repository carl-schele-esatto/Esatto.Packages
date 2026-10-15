using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Editors;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Serialization;

namespace Esatto.Umbraco.Backoffice.BlockPreview.Rendering;

/// <summary>
/// The backoffice sends a block's values in the editors' format (a dropdown: <c>["SQL"]</c>), but
/// value converters read the stored format (<c>"[\"SQL\"]"</c>), which saving produces with each
/// editor's <c>FromEditor</c>. This runs that same step, so unsaved values convert like saved ones.
/// </summary>
public sealed class EditorValueConverter
{
    // Their FromEditor moves newly uploaded temporary files into media (directly or in nested
    // blocks): running it for a preview would consume an upload the editor hasn't saved yet. Their
    // editor format is already what their value converters read.
    private static readonly HashSet<string> SkippedEditors = new(StringComparer.OrdinalIgnoreCase)
    {
        Constants.PropertyEditors.Aliases.UploadField,
        Constants.PropertyEditors.Aliases.ImageCropper,
        Constants.PropertyEditors.Aliases.RichText,
        Constants.PropertyEditors.Aliases.BlockList,
        Constants.PropertyEditors.Aliases.BlockGrid,
    };

    private readonly IPublishedContentTypeCache _contentTypes;
    private readonly PropertyEditorCollection _editors;
    private readonly IJsonSerializer _json;
    private readonly ILogger<EditorValueConverter> _logger;

    public EditorValueConverter(IPublishedContentTypeCache contentTypes, PropertyEditorCollection editors,
        IJsonSerializer json, ILogger<EditorValueConverter> logger)
    {
        _contentTypes = contentTypes;
        _editors = editors;
        _json = json;
        _logger = logger;
    }

    /// <summary>Replaces each value in <paramref name="data"/> with its stored format, in place.</summary>
    public void ToStoredFormat(BlockDataDto data)
    {
        IPublishedContentType? elementType = _contentTypes.Get(PublishedItemType.Element, data.ContentTypeKey);
        foreach (BlockValueDto value in data.Values)
        {
            IPublishedPropertyType? propertyType = elementType?.GetPropertyType(value.Alias);
            if (value.Value is not { } editorValue
                || propertyType is null
                || SkippedEditors.Contains(propertyType.EditorAlias)
                || _editors[propertyType.EditorAlias] is not { } editor)
            {
                continue;
            }

            try
            {
                // Deserialized like Umbraco deserializes block values before FromEditor on save.
                object? configuration = propertyType.DataType.ConfigurationObject;
                object? deserialized = _json.Deserialize<object>(editorValue.GetRawText());
                object? stored = editor.GetValueEditor(configuration)
                    .FromEditor(new ContentPropertyData(deserialized, configuration), currentValue: null);
                value.Value = JsonSerializer.SerializeToElement(stored);
            }
            catch (Exception ex)
            {
                // The editor value may still convert; if not, the preview reports that error.
                _logger.LogDebug(ex, "Couldn't convert {Alias} ({EditorAlias}) to its stored format; using the editor value.",
                    value.Alias, propertyType.EditorAlias);
            }
        }
    }
}
