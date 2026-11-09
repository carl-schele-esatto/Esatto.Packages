namespace Esatto.Umbraco.Backoffice.Seo.Facts;

public sealed record HreflangLink(string Lang, string Href);

/// <summary>
/// Everything the checks need from one fetched page. Stored as JSON per scanned page, so checks can be
/// re-run (after an ignore, a re-check or a package update) without fetching again.
/// </summary>
public sealed record PageFacts
{
    public string FinalUrl { get; init; } = string.Empty;
    public int StatusCode { get; init; }
    public IReadOnlyList<string> RedirectChain { get; init; } = [];
    public long ElapsedMs { get; init; }
    public long Bytes { get; init; }
    public string? Error { get; init; }

    /// <summary>The response was an HTML document (the rest of the facts are only filled for HTML).</summary>
    public bool IsHtml { get; init; }

    public string? Title { get; init; }
    public string? MetaDescription { get; init; }

    /// <summary>Every h1's text, whitespace collapsed (empty ones included as "").</summary>
    public IReadOnlyList<string> H1s { get; init; } = [];

    public int WordCount { get; init; }
    public string? Canonical { get; init; }
    public IReadOnlyList<HreflangLink> Hreflangs { get; init; } = [];
    public string? HtmlLang { get; init; }
    public bool MetaNoindex { get; init; }
    public bool HeaderNoindex { get; init; }
    public string? OgTitle { get; init; }
    public string? OgImage { get; init; }
    public int JsonLdTotal { get; init; }
    public int JsonLdInvalid { get; init; }

    /// <summary>Links to the site's own hosts, normalised (<see cref="UrlNormalizer"/>) and distinct.</summary>
    public IReadOnlyList<string> InternalLinks { get; init; } = [];

    public bool Noindex => MetaNoindex || HeaderNoindex;
}
