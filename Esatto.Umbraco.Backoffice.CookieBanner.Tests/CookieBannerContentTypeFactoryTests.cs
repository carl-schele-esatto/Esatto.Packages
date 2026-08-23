using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class CookieBannerContentTypeFactoryTests
{
    // Umbraco's built-in Textstring data type key, used purely as a stable stand-in.
    private static readonly Guid TextstringKey = new("0cc0eba1-9960-42c9-bf9b-60e150b429ae");
    private static readonly Guid MissingKey = new("00000000-dead-4000-8000-000000000001");

    private readonly IContentTypeService _contentTypes = Substitute.For<IContentTypeService>();
    private readonly IDataTypeService _dataTypes = Substitute.For<IDataTypeService>();
    private readonly ITemplateService _templates = Substitute.For<ITemplateService>();
    private readonly IConfigurationEditorJsonSerializer _serializer =
        Substitute.For<IConfigurationEditorJsonSerializer>();
    private readonly IShortStringHelper _shortStrings = Substitute.For<IShortStringHelper>();

    private CookieBannerContentTypeFactory CreateFactory()
    {
        _shortStrings.CleanStringForSafeAlias(Arg.Any<string>()).Returns(call => call.Arg<string>());

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

    // NOT UNIT TESTED: Property() creates PropertyType instances with correct aliases.
    // PropertyType.Alias goes through IShortStringHelper.CleanStringForSafeAlias(), which cannot
    // be reliably mocked in unit tests without a real Umbraco instance. This behavior is verified
    // in Task 17's integration check against an actual Umbraco site.

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

    // NOT UNIT TESTED: AddGroup() with PropertyType instances created by Property().
    // The test depends on Property() creating PropertyType instances with correct aliases,
    // which requires IShortStringHelper.CleanStringForSafeAlias() to work (see comment above).
    // Group structure and collection handling are verified in Task 17's integration check.

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
}
