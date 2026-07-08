namespace Esatto.Umbraco.Backoffice.CrossContent;

/// <summary>Configuration for reading another site's content over HTTP. Bound from "CrossContent".</summary>
public sealed class CrossContentOptions
{
    public const string SectionName = "CrossContent";

    /// <summary>Target site base URL, e.g. https://othersite.example.</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Shared API key = the target site's Umbraco:CMS:DeliveryApi:ApiKey. Never committed.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Relative path (+ query) to list cases. Default = the target's Delivery API.</summary>
    public string CaseListPath { get; set; } = "umbraco/delivery/api/v2/content?filter=contentType:casePage&sort=name:asc&take=100";

    /// <summary>Base path of the target's teaser endpoint; the fetcher appends /{key}. Default = this
    /// package's producer route. Override to match a site still serving a legacy route.</summary>
    public string TeaserPath { get; set; } = "api/crosscontent/teaser";

    public int TimeoutSeconds { get; set; } = 3;
    public int FreshWindowMinutes { get; set; } = 5;
    public int LastKnownGoodHours { get; set; } = 48;
    public int NegativeCacheSeconds { get; set; } = 60;
}
