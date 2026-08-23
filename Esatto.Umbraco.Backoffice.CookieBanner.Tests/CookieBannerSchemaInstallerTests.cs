using System.Linq;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class CookieBannerSchemaInstallerTests
{
    // Pins that the Cookie category dropdown stores ConsentCategories WIRE names in All order.
    // The policy page and the banner group declared cookies by this stored string, so a display
    // label here (or a reordering) silently renders every category empty.
    [Fact]
    public void Cookie_category_dropdown_items_are_the_consent_wire_names_in_order()
    {
        Assert.Equal(
            ConsentCategories.All.Select(ConsentCategories.ToWireName),
            CookieBannerSchemaInstaller.CookieCategoryItems);

        Assert.Equal(
            new[] { "necessary", "preferences", "statistics", "marketing" },
            CookieBannerSchemaInstaller.CookieCategoryItems);
    }

    // Pins the four storage kinds the policy table renders, and their casing: these are shown to
    // visitors verbatim and the deferred scanner package maps its findings onto exactly these.
    [Fact]
    public void Storage_type_dropdown_items_match_the_four_supported_storage_kinds()
        => Assert.Equal(
            new[] { "Cookie", "localStorage", "sessionStorage", "Pixel" },
            CookieBannerSchemaInstaller.StorageTypeItems);

    // Pins that the Cookie registry Block List allows ONLY cookieDefinition. Any other allowed
    // block would put content into the registry that the policy table cannot render.
    [Fact]
    public void Cookie_registry_block_list_allows_only_the_cookie_definition_element_type()
    {
        Dictionary<string, object> configuration = CookieBannerSchemaInstaller.CookieRegistryConfiguration();

        object[] blocks = Assert.IsType<object[]>(configuration["blocks"]);
        Dictionary<string, object> block = Assert.IsType<Dictionary<string, object>>(Assert.Single(blocks));

        Assert.Equal(CookieBannerKeys.ElementTypes.CookieDefinition, block["contentElementTypeKey"]);
        Assert.Equal("Cookie", block["label"]);
    }

    // Pins that the template row is seeded with the packaged view's real markup. ITemplateService
    // writes a physical Views/CookiePolicy.cshtml that shadows the RCL-compiled view, so seeding a
    // bare @inherits stub would blank the policy page on every consumer site.
    [Fact]
    public void Packaged_cookie_policy_template_is_embedded_and_carries_real_markup()
    {
        string markup = CookieBannerSchemaInstaller.ReadPackagedTemplate();

        Assert.Contains("@inherits", markup);
        Assert.Contains("cookies", markup);
        Assert.True(markup.Length > 200, "the embedded view looks like a stub, not the real template");
    }

    // Pins that the packaged template never hardcodes a host layout. NDSTK's original view set
    // Layout = "Root.cshtml" at line 6; a package doing that breaks every other consumer.
    [Fact]
    public void Packaged_cookie_policy_template_leaves_the_layout_to_the_consumer()
    {
        string markup = CookieBannerSchemaInstaller.ReadPackagedTemplate();

        Assert.DoesNotContain("Root.cshtml", markup);
        Assert.DoesNotContain("Layout =", markup);
    }
}
