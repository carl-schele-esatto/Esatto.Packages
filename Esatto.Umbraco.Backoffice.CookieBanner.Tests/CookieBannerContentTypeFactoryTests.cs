using System.Text.Json;
using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Strings.Css;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class CookieBannerContentTypeFactoryTests
{
    // Umbraco's built-in Textstring data type key, used purely as a stable stand-in.
    private static readonly Guid TextstringKey = new("0cc0eba1-9960-42c9-bf9b-60e150b429ae");
    private static readonly Guid MissingKey = new("00000000-dead-4000-8000-000000000001");

    private static readonly Guid RegistryKey = CookieBannerKeys.DataTypes.CookieRegistry;
    private static readonly Guid DefinitionKey = CookieBannerKeys.ElementTypes.CookieDefinition;

    private readonly IContentTypeService _contentTypes = Substitute.For<IContentTypeService>();
    private readonly IDataTypeService _dataTypes = Substitute.For<IDataTypeService>();
    private readonly ITemplateService _templates = Substitute.For<ITemplateService>();
    private readonly IConfigurationEditorJsonSerializer _serializer =
        Substitute.For<IConfigurationEditorJsonSerializer>();
    private readonly IShortStringHelper _shortStrings = Substitute.For<IShortStringHelper>();

    private CookieBannerContentTypeFactory CreateFactory()
    {
        // PropertyType.Alias runs the alias through IShortStringHelper.CleanString(string, CleanStringType)
        // (via SanitizeAlias -> ToCleanString). Stub it as identity: it is a pure string function, so this
        // keeps the alias assertions meaningful without a real Umbraco instance.
        _shortStrings
            .CleanString(Arg.Any<string>(), Arg.Any<CleanStringType>())
            .Returns(call => call.Arg<string>());

        // propertyEditors is read only by EnsureDataTypeAsync, which needs a booted Umbraco and is
        // covered by the Task 17 integration check; null! keeps these tests off that object graph.
        return new CookieBannerContentTypeFactory(
            _contentTypes, _dataTypes, _templates, null!, _serializer, _shortStrings);
    }

    private static IDataType FakeDataType(Guid key)
    {
        var dataType = Substitute.For<IDataType>();
        dataType.Key.Returns(key);
        dataType.EditorAlias.Returns("Umbraco.TextBox");
        dataType.EditorUiAlias.Returns("Umb.PropertyEditorUi.TextBox");

        // A real IDataType from IDataTypeService.GetAsync() is persisted, so HasIdentity is true.
        // PropertyType's constructor only copies DataTypeId/DataTypeKey across when it is —
        // without this the double is unlike any data type the factory actually sees.
        dataType.HasIdentity.Returns(true);

        return dataType;
    }

    // Pins the cache contract that is the whole reason this factory is COPIED rather than shared:
    // Property() must fail loudly when the install order forgot to preload the data type.
    [Fact]
    public void Property_throws_when_the_data_type_was_not_preloaded()
    {
        CookieBannerContentTypeFactory factory = CreateFactory();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => factory.Property(MissingKey, "category", "Category"));

        Assert.Contains("was not preloaded", error.Message);
    }

    // Pins that PreloadDataTypesAsync fills the cache and that Property() copies every declared
    // field through, including Variations.Nothing (an invariant property on an invariant type).
    [Fact]
    public async Task Property_returns_a_property_type_bound_to_the_preloaded_data_type()
    {
        var fakeDataType = FakeDataType(TextstringKey);
        _dataTypes.GetAsync(TextstringKey).Returns(fakeDataType);
        CookieBannerContentTypeFactory factory = CreateFactory();

        await factory.PreloadDataTypesAsync(TextstringKey);
        IPropertyType property = factory.Property(
            TextstringKey, "cookieName", "Name", "Literal name or pattern, e.g. _ga_*", 4);

        Assert.Equal("cookieName", property.Alias);
        Assert.Equal("Name", property.Name);
        Assert.Equal("Literal name or pattern, e.g. _ga_*", property.Description);
        Assert.Equal(4, property.SortOrder);
        Assert.Equal(ContentVariation.Nothing, property.Variations);
        Assert.Equal(TextstringKey, property.DataTypeKey);
    }

    // Pins the fail-fast: a missing built-in data type must abort the install with the key in the
    // message, not silently produce an element type with no properties.
    [Fact]
    public async Task PreloadDataTypesAsync_throws_when_the_data_type_does_not_exist()
    {
        _dataTypes.GetAsync(MissingKey).Returns((IDataType?)null);
        CookieBannerContentTypeFactory factory = CreateFactory();

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => factory.PreloadDataTypesAsync(MissingKey));

        Assert.Contains(MissingKey.ToString(), error.Message);
    }

    // Pins create-if-missing: an existing template is returned untouched so backoffice edits to
    // the cookie policy view survive an app restart.
    [Fact]
    public async Task EnsureTemplateAsync_returns_the_existing_template_without_creating_it()
    {
        Guid key = new("c00c1e00-0004-4000-8000-000000000001");
        var existing = Substitute.For<ITemplate>();
        _templates.GetAsync(key).Returns(existing);
        CookieBannerContentTypeFactory factory = CreateFactory();

        ITemplate result = await factory.EnsureTemplateAsync(key, "Cookie policy", "CookiePolicy", "@* x *@");

        Assert.Same(existing, result);
        await _templates.DidNotReceive().CreateAsync(Arg.Any<ITemplate>(), Arg.Any<Guid>());
    }

    // Pins that a group is added as a Tab with the caption and sort order given, and that the
    // property list is carried into it in declaration order.
    [Fact]
    public async Task AddGroup_adds_one_tab_carrying_the_declared_properties()
    {
        var fakeDataType = FakeDataType(TextstringKey);
        _dataTypes.GetAsync(TextstringKey).Returns(fakeDataType);
        CookieBannerContentTypeFactory factory = CreateFactory();
        await factory.PreloadDataTypesAsync(TextstringKey);

        var contentType = Substitute.For<IContentType>();
        contentType.PropertyGroups.Returns(new PropertyGroupCollection());
        Guid groupKey = new("c00c1e00-0002-4000-8000-000000000081");

        CookieBannerContentTypeFactory.AddGroup(
            contentType, groupKey, "content", "Content", 0,
            factory.Property(TextstringKey, "cookieName", "Name", sortOrder: 0),
            factory.Property(TextstringKey, "provider", "Provider", sortOrder: 1));

        PropertyGroup group = Assert.Single(contentType.PropertyGroups);
        Assert.Equal(groupKey, group.Key);
        Assert.Equal("content", group.Alias);
        Assert.Equal("Content", group.Name);
        Assert.Equal(PropertyGroupType.Tab, group.Type);
        Assert.Equal(0, group.SortOrder);
        Assert.Equal(
            new[] { "cookieName", "provider" },
            group.PropertyTypes!.Select(property => property.Alias));
    }

    // Pins that UseTemplate does BOTH halves: allowing a template without setting it as the
    // default leaves the cookie policy page rendering the host's fallback view.
    [Fact]
    public void UseTemplate_allows_the_template_and_makes_it_the_default()
    {
        var contentType = Substitute.For<IContentType>();
        var template = Substitute.For<ITemplate>();

        CookieBannerContentTypeFactory.UseTemplate(contentType, template);

        contentType.Received().AllowedTemplates =
            Arg.Is<IEnumerable<ITemplate>>(templates => templates.Single() == template);
        contentType.Received().SetDefaultTemplate(template);
    }

    // ------------------------------------------------- ReplaceBlockLabelAsync

    /// <summary>
    /// Makes the serializer double behave like the real one: these tests are about which JSON the
    /// factory rewrites, so a stub returning null would test nothing. System.Text.Json rather than
    /// Umbraco's own implementation because the factory's contract is only that it round-trips
    /// through whatever serializer it was handed.
    /// </summary>
    private void UseRealJson()
    {
        _serializer
            .Serialize(Arg.Any<object>())
            .Returns(call => JsonSerializer.Serialize(call.Arg<object>()));

        _serializer
            .Deserialize<Dictionary<string, object>>(Arg.Any<string>())
            .Returns(call => JsonSerializer.Deserialize<Dictionary<string, object>>(call.Arg<string>()));
    }

    private IDataType BlockListDataType(Guid key, Guid elementTypeKey, string label)
    {
        IDataType dataType = FakeDataType(key);
        dataType.Name.Returns("Cookie registry");

        // The shape Umbraco stores a Block List's configuration in, including one key this method
        // knows nothing about - editorSize - so the tests can prove it survives.
        dataType.ConfigurationData.Returns(new Dictionary<string, object>
        {
            ["blocks"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["contentElementTypeKey"] = elementTypeKey,
                    ["label"] = label,
                    ["editorSize"] = "medium",
                },
            },
        });

        _dataTypes.GetAsync(key).Returns(dataType);
        _dataTypes
            .UpdateAsync(Arg.Any<IDataType>(), Arg.Any<Guid>())
            .Returns(Attempt.SucceedWithStatus(DataTypeOperationStatus.Success, dataType));

        return dataType;
    }

    // The upgrade this method exists for: a site created before the UFM label carries the bare word
    // "Cookie" on every row, and create-if-missing can never reach it.
    [Fact]
    public async Task ReplaceBlockLabelAsync_replaces_the_shipped_default()
    {
        UseRealJson();
        IDataType dataType = BlockListDataType(RegistryKey, DefinitionKey, "Cookie");
        CookieBannerContentTypeFactory factory = CreateFactory();

        var replaced = await factory.ReplaceBlockLabelAsync(
            RegistryKey, DefinitionKey, "Cookie", "{=cookieName}");

        Assert.True(replaced);

        Dictionary<string, object> written = LastWrittenConfiguration(dataType);
        JsonElement block = Assert.IsType<JsonElement>(written["blocks"])[0];

        Assert.Equal("{=cookieName}", block.GetProperty("label").GetString());

        // The setting the method knows nothing about has to survive: rewriting the configuration
        // must not amount to replacing it with only the keys this code happens to name.
        Assert.Equal("medium", block.GetProperty("editorSize").GetString());
    }

    // A site whose editor has renamed the block keeps their wording. This is the guard that lets the
    // call sit in the install path and run on every boot instead of needing a run-once marker.
    [Fact]
    public async Task ReplaceBlockLabelAsync_leaves_a_label_somebody_else_chose()
    {
        UseRealJson();
        BlockListDataType(RegistryKey, DefinitionKey, "Kaka");
        CookieBannerContentTypeFactory factory = CreateFactory();

        var replaced = await factory.ReplaceBlockLabelAsync(
            RegistryKey, DefinitionKey, "Cookie", "{=cookieName}");

        Assert.False(replaced);
        await _dataTypes.DidNotReceive().UpdateAsync(Arg.Any<IDataType>(), Arg.Any<Guid>());
    }

    // Idempotence, stated as its own test because the install path calls this on every start: the
    // second run finds the new label, not the old one, and must not write again.
    [Fact]
    public async Task ReplaceBlockLabelAsync_does_nothing_once_already_upgraded()
    {
        UseRealJson();
        BlockListDataType(RegistryKey, DefinitionKey, "{=cookieName}");
        CookieBannerContentTypeFactory factory = CreateFactory();

        var replaced = await factory.ReplaceBlockLabelAsync(
            RegistryKey, DefinitionKey, "Cookie", "{=cookieName}");

        Assert.False(replaced);
        await _dataTypes.DidNotReceive().UpdateAsync(Arg.Any<IDataType>(), Arg.Any<Guid>());
    }

    // A block for some other element type is not this method's business, even when its label happens
    // to read the same. The registry allows only cookieDefinition today, but the guard is what keeps
    // that from being an assumption baked into an upgrade.
    [Fact]
    public async Task ReplaceBlockLabelAsync_ignores_a_different_element_type()
    {
        UseRealJson();
        BlockListDataType(RegistryKey, MissingKey, "Cookie");
        CookieBannerContentTypeFactory factory = CreateFactory();

        var replaced = await factory.ReplaceBlockLabelAsync(
            RegistryKey, DefinitionKey, "Cookie", "{=cookieName}");

        Assert.False(replaced);
    }

    // A data type that is not there at all is not an error: on a fresh database the install path runs
    // this in the same pass that creates the Block List, and ordering should not make it throw.
    [Fact]
    public async Task ReplaceBlockLabelAsync_returns_false_when_the_data_type_is_missing()
    {
        UseRealJson();
        _dataTypes.GetAsync(RegistryKey).Returns((IDataType?)null);
        CookieBannerContentTypeFactory factory = CreateFactory();

        Assert.False(await factory.ReplaceBlockLabelAsync(
            RegistryKey, DefinitionKey, "Cookie", "{=cookieName}"));
    }

    /// <summary>The configuration the factory assigned back onto the data type.</summary>
    private static Dictionary<string, object> LastWrittenConfiguration(IDataType dataType)
    {
        // NSubstitute records the property setter; the last received value is what was written.
        var written = (Dictionary<string, object>?)dataType
            .ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == "set_ConfigurationData")
            .Select(call => call.GetArguments()[0])
            .LastOrDefault();

        Assert.NotNull(written);
        return written;
    }
}
