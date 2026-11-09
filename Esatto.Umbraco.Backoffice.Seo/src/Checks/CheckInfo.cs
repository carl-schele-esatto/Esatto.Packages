namespace Esatto.Umbraco.Backoffice.Seo.Checks;

/// <summary>Critical = the page can't be found or indexed; Warning = hurts how it shows or ranks; Notice = worth doing.</summary>
public enum Severity { Notice, Warning, Critical }

/// <summary>Who usually fixes it: editors (page content) or developers (templates and setup).</summary>
public enum Owner { Editor, Developer }

/// <summary>Page: judged from one page. CrossPage: needs several pages. Site: one result for the whole site.</summary>
public enum CheckScope { Page, CrossPage, Site }

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
