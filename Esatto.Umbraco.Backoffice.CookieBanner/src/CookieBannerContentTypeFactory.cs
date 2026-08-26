using System.Text.Json.Nodes;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Thin wrapper over the Umbraco services that turns the declarative descriptions in
/// <see cref="CookieBannerSchemaInstaller"/> into persisted schema. Every Ensure* method is
/// create-if-missing: an entity that already exists is returned untouched, so changes made in
/// the backoffice survive an app restart.
/// </summary>
/// <remarks>
/// This is a deliberate copy of NDSTK's <c>NdstkContentTypeFactory</c> rather than a shared
/// dependency. The <see cref="_dataTypes"/> cache is mutable per instance and
/// <see cref="Property"/> throws for a key that was never preloaded, so one singleton shared
/// between two independent installers would turn either installer's ordering mistake into the
/// other's runtime failure. Duplicating 200 generic lines is cheaper than that coupling.
/// </remarks>
internal sealed class CookieBannerContentTypeFactory(
    IContentTypeService contentTypeService,
    IDataTypeService dataTypeService,
    ITemplateService templateService,
    PropertyEditorCollection propertyEditors,
    IConfigurationEditorJsonSerializer configurationSerializer,
    IShortStringHelper shortStringHelper)
{
    private const int RootParentId = -1;

    /// <summary>The Block List configuration keys <see cref="ReplaceBlockLabelAsync"/> reads.</summary>
    private const string BlocksField = "blocks";
    private const string BlockElementField = "contentElementTypeKey";
    private const string LabelField = "label";

    private static readonly Guid UserKey = Constants.Security.SuperUserKey;

    private readonly Dictionary<Guid, IDataType> _dataTypes = [];

    public async Task<ITemplate> EnsureTemplateAsync(Guid key, string name, string alias, string content)
    {
        ITemplate? existing = await templateService.GetAsync(key);
        if (existing is not null)
        {
            return existing;
        }

        var template = new Template(shortStringHelper, name, alias)
        {
            Key = key,
            Content = content,
        };

        var attempt = await templateService.CreateAsync(template, UserKey);
        if (attempt.Success is false)
        {
            throw new InvalidOperationException($"Could not create template '{alias}': {attempt.Status}.");
        }

        return attempt.Result!;
    }

    public async Task<IDataType> EnsureDataTypeAsync(
        Guid key,
        string name,
        string editorAlias,
        string editorUiAlias,
        IDictionary<string, object>? configuration = null)
    {
        IDataType? existing = await dataTypeService.GetAsync(key);
        if (existing is not null)
        {
            return existing;
        }

        if (propertyEditors.TryGet(editorAlias, out IDataEditor? editor) is false)
        {
            throw new InvalidOperationException($"No property editor is registered for alias '{editorAlias}'.");
        }

        var dataType = new DataType(editor, configurationSerializer, RootParentId)
        {
            Key = key,
            Name = name,
            EditorUiAlias = editorUiAlias,
        };

        dataType.SetConfigurationData(configuration ?? new Dictionary<string, object>());

        var attempt = await dataTypeService.CreateAsync(dataType, UserKey);
        if (attempt.Success is false)
        {
            throw new InvalidOperationException($"Could not create data type '{name}': {attempt.Status}.");
        }

        return attempt.Result;
    }

    /// <summary>
    /// Loads the data types that <see cref="Property"/> will bind to. Doing it up front keeps the
    /// schema declarations synchronous and therefore readable.
    /// </summary>
    public async Task PreloadDataTypesAsync(params Guid[] keys)
    {
        foreach (Guid key in keys.Distinct().Where(key => _dataTypes.ContainsKey(key) is false))
        {
            _dataTypes[key] = await dataTypeService.GetAsync(key)
                              ?? throw new InvalidOperationException($"Data type {key} was not found.");
        }
    }

    /// <summary>Builds a property type bound to one of the preloaded data types.</summary>
    public IPropertyType Property(
        Guid dataTypeKey,
        string alias,
        string name,
        string? description = null,
        int sortOrder = 0)
    {
        if (_dataTypes.TryGetValue(dataTypeKey, out IDataType? dataType) is false)
        {
            throw new InvalidOperationException($"Data type {dataTypeKey} was not preloaded.");
        }

        return new PropertyType(shortStringHelper, dataType, alias)
        {
            Name = name,
            Description = description,
            SortOrder = sortOrder,
            Variations = ContentVariation.Nothing,
        };
    }

    /// <summary>
    /// Creates a document type or element type when it is missing. <paramref name="configure"/>
    /// only runs for a brand new type, so existing schema is never rewritten.
    /// </summary>
    public async Task<IContentType> EnsureContentTypeAsync(
        Guid key,
        string alias,
        string name,
        string icon,
        Action<IContentType> configure)
    {
        IContentType? existing = contentTypeService.Get(key);
        if (existing is not null)
        {
            return existing;
        }

        var contentType = new ContentType(shortStringHelper, RootParentId)
        {
            Key = key,
            Alias = alias,
            Name = name,
            Icon = icon,
            Variations = ContentVariation.Nothing,
        };

        configure(contentType);

        var attempt = await contentTypeService.CreateAsync(contentType, UserKey);
        if (attempt.Success is false)
        {
            throw new InvalidOperationException($"Could not create content type '{alias}': {attempt.Result}.");
        }

        return contentTypeService.Get(key)
               ?? throw new InvalidOperationException($"Content type '{alias}' was created but could not be read back.");
    }

    public static void AddGroup(
        IContentType contentType,
        Guid key,
        string alias,
        string caption,
        int sortOrder,
        params IPropertyType[] properties)
        => contentType.PropertyGroups.Add(new PropertyGroup(true)
        {
            Key = key,
            Alias = alias,
            Name = caption,
            Type = PropertyGroupType.Tab,
            SortOrder = sortOrder,
            PropertyTypes = new PropertyTypeCollection(true, properties),
        });

    public static void UseTemplate(IContentType contentType, ITemplate template)
    {
        contentType.AllowedTemplates = [template];
        contentType.SetDefaultTemplate(template);
    }

    /// <summary>
    /// Changes the label of one block in a Block List data type, but only where that label still
    /// reads as the value this package originally shipped.
    /// </summary>
    /// <remarks>
    /// The one method here that is not create-if-missing, and the guard is what makes that safe.
    /// <see cref="EnsureDataTypeAsync"/> returns an existing data type untouched, so a label improved
    /// in a later version of this package would otherwise reach new installs only, and every site
    /// already running it would keep the old one for ever.
    ///
    /// Matching on <paramref name="from"/> instead of overwriting outright is the whole design. A
    /// block label is editable in the backoffice, so a site that has chosen its own wording keeps it
    /// and a site still on the shipped default is upgraded. Naturally idempotent: after the first run
    /// the label no longer equals <paramref name="from"/>, so there is nothing left to do and the
    /// call can sit in the install path on every boot.
    ///
    /// The configuration is carried through the serializer Umbraco stores it with rather than
    /// through <c>BlockListConfiguration</c>, so every setting this method knows nothing about -
    /// editorSize, a settings element type, the validation limits - crosses untouched. Assigned
    /// through the <c>ConfigurationData</c> property rather than <c>SetConfigurationData</c>: that
    /// method is documented as being for building entities out of the database and deliberately
    /// leaves the entity clean, which would hand UpdateAsync nothing to save.
    /// </remarks>
    /// <returns>True when the label was the old default and has been replaced.</returns>
    public async Task<bool> ReplaceBlockLabelAsync(Guid dataTypeKey, Guid elementTypeKey, string from, string to)
    {
        IDataType? dataType = await dataTypeService.GetAsync(dataTypeKey);
        if (dataType is null)
        {
            return false;
        }

        JsonObject? configuration =
            JsonNode.Parse(configurationSerializer.Serialize(dataType.ConfigurationData))?.AsObject();

        JsonArray? blocks = configuration?[BlocksField] as JsonArray;
        if (blocks is null)
        {
            return false;
        }

        var changed = false;

        foreach (JsonNode? block in blocks)
        {
            if (block is null
                || Guid.TryParse(block[BlockElementField]?.GetValue<string>(), out Guid key) is false
                || key != elementTypeKey
                || block[LabelField]?.GetValue<string>() != from)
            {
                continue;
            }

            block[LabelField] = to;
            changed = true;
        }

        if (changed is false)
        {
            return false;
        }

        dataType.ConfigurationData =
            configurationSerializer.Deserialize<Dictionary<string, object>>(configuration!.ToJsonString())
            ?? throw new InvalidOperationException(
                $"Could not rebuild the configuration of data type {dataTypeKey}.");

        var attempt = await dataTypeService.UpdateAsync(dataType, UserKey);
        if (attempt.Success is false)
        {
            throw new InvalidOperationException(
                $"Could not relabel the blocks on data type '{dataType.Name}': {attempt.Status}.");
        }

        return true;
    }
}
