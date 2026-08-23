using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using static Esatto.Umbraco.Backoffice.CookieBanner.CookieBannerKeys;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Declares the six schema artefacts the cookie banner owns - two dropdowns, the
/// <c>cookieDefinition</c> element type, the <c>cookieRegistry</c> Block List, and the
/// <c>cookiePolicy</c> template plus document type - and creates whatever is missing. It runs
/// after boot on every start; because each step is create-if-missing it is cheap on an installed
/// site and self-healing on a fresh database.
/// </summary>
internal sealed class CookieBannerSchemaInstaller(
    CookieBannerContentTypeFactory factory,
    ILogger<CookieBannerSchemaInstaller> logger)
{
    /// <summary>
    /// Logical name of <c>Views/CookiePolicy.cshtml</c> embedded alongside the compiled RCL view.
    /// </summary>
    private const string TemplateResourceName =
        "Esatto.Umbraco.Backoffice.CookieBanner.Views.CookiePolicy.cshtml";

    /// <summary>
    /// The Cookie category dropdown's items. These are the wire names from
    /// <c>ConsentCategories.ToWireName</c>, not display labels: the policy page and the banner
    /// group declared cookies by the stored value, so it must match exactly. Display names come
    /// from the <c>Cookies.Category.*.Name</c> dictionary items. The stored value is a JSON array
    /// holding one string, e.g. <c>["necessary"]</c>.
    /// </summary>
    internal static readonly string[] CookieCategoryItems =
        ["necessary", "preferences", "statistics", "marketing"];

    /// <summary>
    /// The Storage type dropdown's items. Rendered verbatim in the policy table, so the casing is
    /// part of the contract. Stored, like every flexible dropdown, as <c>["Cookie"]</c>.
    /// </summary>
    internal static readonly string[] StorageTypeItems =
        ["Cookie", "localStorage", "sessionStorage", "Pixel"];

    public async Task InstallAsync()
    {
        // The built-in data types the cookie schema binds to.
        await factory.PreloadDataTypesAsync(
            BuiltInDataTypes.Textstring,
            BuiltInDataTypes.Textarea,
            BuiltInDataTypes.RichtextEditor);

        // Step 1. The cookie category / storage type dropdowns must exist - and be preloaded -
        // before the cookie definition element type is declared, because that element type binds
        // to them and factory.Property throws if a data type was not preloaded first.
        await InstallDropdownDataTypesAsync();
        await factory.PreloadDataTypesAsync(DataTypes.CookieCategory, DataTypes.StorageType);

        // Step 2. The element type. Nothing may reference it before this point.
        await InstallCookieDefinitionAsync();

        // Step 3. The Block List references the element type by key, so it can only be created
        // once the element type exists. Preloaded straight away for the document type below.
        await InstallCookieRegistryAsync();
        await factory.PreloadDataTypesAsync(DataTypes.CookieRegistry);

        // Step 4. Template before document type: UseTemplate needs a persisted ITemplate.
        ITemplate template = await factory.EnsureTemplateAsync(
            Templates.CookiePolicy,
            "Cookie policy",
            "CookiePolicy",
            ReadPackagedTemplate());

        await InstallCookiePolicyAsync(template);

        logger.LogInformation("Cookie banner schema is up to date.");
    }

    // ---------------------------------------------------------------- dropdowns

    private async Task InstallDropdownDataTypesAsync()
    {
        await factory.EnsureDataTypeAsync(
            DataTypes.CookieCategory,
            "Cookie category",
            Constants.PropertyEditors.Aliases.DropDownListFlexible,
            "Umb.PropertyEditorUi.Dropdown",
            new Dictionary<string, object>
            {
                ["multiple"] = false,
                ["items"] = CookieCategoryItems,
            });

        await factory.EnsureDataTypeAsync(
            DataTypes.StorageType,
            "Storage type",
            Constants.PropertyEditors.Aliases.DropDownListFlexible,
            "Umb.PropertyEditorUi.Dropdown",
            new Dictionary<string, object>
            {
                ["multiple"] = false,
                ["items"] = StorageTypeItems,
            });
    }

    // ------------------------------------------------------------- element type

    /// <remarks>
    /// Aliases and property aliases are identical to the NDSTK original so existing content is
    /// portable onto package-owned schema. Only the descriptions changed: NDSTK's were partly
    /// Swedish and named its own site as an example provider.
    /// </remarks>
    private Task InstallCookieDefinitionAsync()
        => factory.EnsureContentTypeAsync(
            ElementTypes.CookieDefinition, "cookieDefinition", "Cookie", "icon-lock", type =>
            {
                type.IsElement = true;
                type.Description = "One declared cookie, shown in the cookie policy table.";
                CookieBannerContentTypeFactory.AddGroup(
                    type, DeriveKey(ElementTypes.CookieDefinition, 1), "content", "Content", 0,
                    factory.Property(BuiltInDataTypes.Textstring, "cookieName", "Name",
                        "Literal name or pattern, e.g. _ga_*", 0),
                    factory.Property(BuiltInDataTypes.Textstring, "provider", "Provider",
                        "Who sets the cookie, e.g. this site, Google, YouTube.", 1),
                    factory.Property(DataTypes.CookieCategory, "category", "Category", sortOrder: 2),
                    factory.Property(BuiltInDataTypes.Textarea, "purpose", "Purpose", sortOrder: 3),
                    factory.Property(BuiltInDataTypes.Textstring, "duration", "Duration",
                        "How long it is stored, e.g. \"12 months\" or \"Session\".", 4),
                    factory.Property(DataTypes.StorageType, "storageType", "Storage type", sortOrder: 5));
            });

    // --------------------------------------------------------------- Block List

    private Task InstallCookieRegistryAsync()
        => factory.EnsureDataTypeAsync(
            DataTypes.CookieRegistry,
            "Cookie registry",
            Constants.PropertyEditors.Aliases.BlockList,
            "Umb.PropertyEditorUi.BlockList",
            CookieRegistryConfiguration());

    /// <summary>The Block List configuration: cookie definitions and nothing else.</summary>
    internal static Dictionary<string, object> CookieRegistryConfiguration() => new()
    {
        ["blocks"] = new object[] { Block(ElementTypes.CookieDefinition, "Cookie") },
    };

    private static Dictionary<string, object> Block(Guid elementTypeKey, string label) => new()
    {
        ["contentElementTypeKey"] = elementTypeKey,
        ["label"] = label,
        ["editorSize"] = "medium",
    };

    // ------------------------------------------------------------ document type

    private Task InstallCookiePolicyAsync(ITemplate template)
        => factory.EnsureContentTypeAsync(
            DocumentTypes.CookiePolicy, "cookiePolicy", "Cookie policy", "icon-lock", type =>
            {
                type.Description = "Lists the declared cookies and the visitor's current consent.";

                // A package cannot add itself to a consumer's document type structure, so the page
                // is allowed at root. CookiePolicyPageResolver finds it anywhere in the tree, and
                // an editor is free to allow it under their own page types instead.
                type.AllowedAsRoot = true;

                CookieBannerContentTypeFactory.UseTemplate(type, template);
                CookieBannerContentTypeFactory.AddGroup(
                    type, DeriveKey(DocumentTypes.CookiePolicy, 1), "content", "Content", 0,
                    factory.Property(BuiltInDataTypes.Textstring, "heading", "Heading",
                        "Falls back to a default heading if left blank.", 0),
                    factory.Property(BuiltInDataTypes.RichtextEditor, "introduction", "Introduction",
                        sortOrder: 1),
                    factory.Property(DataTypes.CookieRegistry, "cookies", "Declared cookies",
                        sortOrder: 2),
                    factory.Property(BuiltInDataTypes.RichtextEditor, "outro", "Closing text",
                        sortOrder: 3));
            });

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// Reads the packaged cookie policy view out of the assembly manifest. Umbraco's template
    /// service writes the content it is given to a physical <c>Views/CookiePolicy.cshtml</c>, and
    /// with Razor runtime compilation on that physical file shadows the compiled RCL view - so the
    /// content handed to it has to be the real markup, not a stub.
    /// </summary>
    internal static string ReadPackagedTemplate()
    {
        using Stream stream = typeof(CookieBannerSchemaInstaller).Assembly
                                  .GetManifestResourceStream(TemplateResourceName)
                              ?? throw new InvalidOperationException(
                                  $"Embedded resource '{TemplateResourceName}' is missing from the package.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Property groups need their own stable keys. Deriving them from the owning type's key keeps
    /// the key registry small while staying deterministic across installs.
    /// </summary>
    private static Guid DeriveKey(Guid owner, byte discriminator)
    {
        Span<byte> bytes = stackalloc byte[16];
        owner.TryWriteBytes(bytes);
        bytes[15] = (byte)(bytes[15] ^ 0x80 ^ discriminator);
        return new Guid(bytes);
    }
}
