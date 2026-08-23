using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class CookieBannerOptionsTests
{
    // Pins the config section name: it is the repo's first IOptions section, so this string is the
    // naming precedent and appears verbatim in every consumer's appsettings.json.
    [Fact]
    public void Section_name_is_the_published_config_path()
        => Assert.Equal("Esatto:CookieBanner", CookieBannerOptions.SectionName);

    // Pins the package-neutral defaults: a package must work with an empty config section, and must
    // not default to any one site's cookie name.
    [Fact]
    public void Defaults_are_package_neutral()
    {
        CookieBannerOptions options = new();

        Assert.Equal(1, options.PolicyVersion);
        Assert.Equal("cookie-consent", options.CookieName);
        Assert.Equal(365, options.CookieLifetimeDays);
        Assert.Null(options.GoogleMeasurementId);
        Assert.Null(options.PolicyPageKey);
        Assert.Equal("/api/cookie-consent", options.EndpointPath);
        Assert.Equal(10, options.ThrottleRequestsPerMinute);
    }
}
