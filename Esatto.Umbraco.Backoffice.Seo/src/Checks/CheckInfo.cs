using System.Text.Json.Serialization;

namespace Esatto.Umbraco.Backoffice.Seo.Checks;

// The JSON names are spelled out so the API's contract doesn't depend on the host's JSON settings: Umbraco's management
// API writes enum names as declared ("Warning"), and with no enum converter at all they would be numbers. The
// dashboard expects camelCase. The converter attribute covers hosts without an enum converter; a host's own
// JsonStringEnumConverter honours the member names too.

/// <summary>Critical = the page can't be found or indexed; Warning = hurts how it shows or ranks; Notice = worth doing.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Severity>))]
public enum Severity
{
    [JsonStringEnumMemberName("notice")] Notice,
    [JsonStringEnumMemberName("warning")] Warning,
    [JsonStringEnumMemberName("critical")] Critical,
}

/// <summary>Who usually fixes it: editors (page content) or developers (templates and setup).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Owner>))]
public enum Owner
{
    [JsonStringEnumMemberName("editor")] Editor,
    [JsonStringEnumMemberName("developer")] Developer,
}

/// <summary>Page: judged from one page. CrossPage: needs several pages. Site: one result for the whole site.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CheckScope>))]
public enum CheckScope
{
    [JsonStringEnumMemberName("page")] Page,
    [JsonStringEnumMemberName("crossPage")] CrossPage,
    [JsonStringEnumMemberName("site")] Site,
}

public sealed record CheckInfo(string Id, Severity Severity, Owner Owner, CheckScope Scope);

/// <summary>Every check's stable id. Used in the database, the API and the knowledge base: never rename.</summary>
public static class CheckIds
{
    // Page content (editor)
    public const string TitleMissing = "title-missing";
    public const string TitleLength = "title-length";
    public const string TitleDuplicate = "title-duplicate";
    public const string DescriptionMissing = "description-missing";
    public const string DescriptionLength = "description-length";
    public const string DescriptionDuplicate = "description-duplicate";
    public const string H1Missing = "h1-missing";
    public const string H1Multiple = "h1-multiple";
    public const string ThinContent = "thin-content";
    public const string BrokenInternalLink = "broken-internal-link";
    public const string RedirectedInternalLink = "redirected-internal-link";
    public const string OgMissing = "og-missing";
    public const string TranslationMissing = "translation-missing";

    // Templates and setup (developer)
    public const string StatusError = "status-error";
    public const string HomeHidden = "home-hidden";
    public const string HiddenButBlocked = "hidden-but-blocked";
    public const string CanonicalMissing = "canonical-missing";
    public const string CanonicalMismatch = "canonical-mismatch";
    public const string HreflangMissing = "hreflang-missing";
    public const string HreflangInvalid = "hreflang-invalid";
    public const string HreflangNoXDefault = "hreflang-no-x-default";
    public const string LangMismatch = "lang-mismatch";
    public const string StructuredDataInvalid = "structured-data-invalid";
    public const string SlowResponse = "slow-response";
    public const string HeavyHtml = "heavy-html";
    public const string SitemapMissingPage = "sitemap-missing-page";
    public const string SitemapBadEntry = "sitemap-bad-entry";

    // Whole site (developer)
    public const string RobotsMissing = "robots-missing";
    public const string SitemapMissing = "sitemap-missing";
    public const string HttpsRedirect = "https-redirect";
    public const string HostDuplicate = "host-duplicate";
    public const string SiteHidden = "site-hidden";
}
