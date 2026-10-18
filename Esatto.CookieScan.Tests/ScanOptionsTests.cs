using Esatto.CookieScan.Engine;

namespace Esatto.CookieScan.Tests;

/// <summary>
/// The command line, for the flags whose absence changes what a scan reports rather than only how
/// much of it runs.
/// </summary>
public class ScanOptionsTests
{
    private static ScanOptions Parse(params string[] args) => ScanOptions.Parse(args);

    // The flag exists because the consent cookie's name is the one thing in the catalogue that is
    // per-site configuration. See CookieCatalogue.WithConsentCookieNamed for what a wrong value
    // costs.
    [Fact]
    public void The_consent_cookie_flag_is_read()
    {
        Assert.Equal(
            "ndstk-consent",
            Parse("--url", "https://example.com", "--consent-cookie", "ndstk-consent").ConsentCookie);
    }

    // Null, not "": the engine reads "nothing to say" as "use the shipped default", and an empty
    // string would have to be checked for separately everywhere the value is used.
    [Fact]
    public void An_omitted_consent_cookie_flag_is_null()
    {
        Assert.Null(Parse("--url", "https://example.com").ConsentCookie);
    }

    // A flag typed with no value after it - the shape `--consent-cookie --dry-run` produces - must
    // not be read as a consent cookie named "--dry-run", and must not silently swallow the flag
    // that follows it.
    [Fact]
    public void A_valueless_consent_cookie_flag_is_null_and_does_not_eat_the_next_flag()
    {
        ScanOptions options = Parse("--url", "https://example.com", "--consent-cookie", "--dry-run");

        Assert.Null(options.ConsentCookie);
        Assert.True(options.DryRun);
    }

    // The secret is never a flag, and the reason is worth a test rather than only a comment: a
    // --client-secret on the command line would end up in shell history and in any process listing.
    [Fact]
    public void The_client_secret_comes_from_the_environment_and_not_from_a_flag()
    {
        ScanOptions options = Parse("--url", "https://example.com", "--client-secret", "hunter2");

        Assert.NotEqual("hunter2", options.ClientSecret);
        Assert.Equal("ESATTO_COOKIESCAN_CLIENT_SECRET", ScanOptions.SecretVariable);
    }

    [Fact]
    public void A_missing_url_is_refused_with_a_message_naming_the_command()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => Parse("--dry-run"));

        Assert.Contains("esatto-cookiescan", error.Message, StringComparison.Ordinal);
    }

    // UriFormatException derives from FormatException rather than ArgumentException, so an
    // unvalidated parse here would greet the likeliest operator mistake there is - a URL pasted
    // without its scheme - with a stack trace instead of a sentence.
    [Fact]
    public void A_url_without_a_scheme_is_refused_as_an_argument_error()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(
            () => Parse("--url", "example.com"));

        Assert.Contains("absolute URL", error.Message, StringComparison.Ordinal);
    }
}
