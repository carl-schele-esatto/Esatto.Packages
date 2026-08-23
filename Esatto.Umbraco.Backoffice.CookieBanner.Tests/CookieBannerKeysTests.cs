using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class CookieBannerKeysTests
{
    // Pins the six schema GUIDs to the published contract. These are written into a consumer's
    // database on install, so changing one orphans the artefact and the next boot creates a
    // duplicate alongside it.
    [Fact]
    public void Schema_keys_match_the_published_contract_guids()
    {
        Assert.Equal(new Guid("c00c1e00-0001-4000-8000-000000000001"), CookieBannerKeys.DataTypes.CookieCategory);
        Assert.Equal(new Guid("c00c1e00-0001-4000-8000-000000000002"), CookieBannerKeys.DataTypes.StorageType);
        Assert.Equal(new Guid("c00c1e00-0001-4000-8000-000000000003"), CookieBannerKeys.DataTypes.CookieRegistry);
        Assert.Equal(new Guid("c00c1e00-0002-4000-8000-000000000001"), CookieBannerKeys.ElementTypes.CookieDefinition);
        Assert.Equal(new Guid("c00c1e00-0003-4000-8000-000000000001"), CookieBannerKeys.DocumentTypes.CookiePolicy);
        Assert.Equal(new Guid("c00c1e00-0004-4000-8000-000000000001"), CookieBannerKeys.Templates.CookiePolicy);
    }

    // Pins that the package's own keys are distinct from each other and from the NDSTK series they
    // replaced: reusing an NDSTK GUID would make the package adopt (and then rewrite) site schema.
    [Fact]
    public void Schema_keys_are_distinct_and_share_no_ground_with_the_ndstk_series()
    {
        Guid[] keys =
        [
            CookieBannerKeys.DataTypes.CookieCategory,
            CookieBannerKeys.DataTypes.StorageType,
            CookieBannerKeys.DataTypes.CookieRegistry,
            CookieBannerKeys.ElementTypes.CookieDefinition,
            CookieBannerKeys.DocumentTypes.CookiePolicy,
            CookieBannerKeys.Templates.CookiePolicy,
        ];

        Assert.Equal(keys.Length, keys.Distinct().Count());
        Assert.All(keys, key => Assert.StartsWith("c00c1e00-", key.ToString()));
    }

    // Pins the three Umbraco built-in data type keys the cookie schema binds to. A wrong key here
    // fails the install with "Data type ... was not found" rather than producing bad schema.
    [Fact]
    public void Built_in_data_type_keys_match_the_umbraco_defaults()
    {
        Assert.Equal(new Guid("0cc0eba1-9960-42c9-bf9b-60e150b429ae"), CookieBannerKeys.BuiltInDataTypes.Textstring);
        Assert.Equal(new Guid("c6bac0dd-4ab9-45b1-8e30-e4b619ee5da3"), CookieBannerKeys.BuiltInDataTypes.Textarea);
        Assert.Equal(new Guid("ca90c950-0aff-4e72-b976-a30b1ac57dad"), CookieBannerKeys.BuiltInDataTypes.RichtextEditor);
    }
}
