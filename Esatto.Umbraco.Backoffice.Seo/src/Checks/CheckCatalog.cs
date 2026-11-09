namespace Esatto.Umbraco.Backoffice.Seo.Checks;

/// <summary>Every check the package runs. Checks are stateless, so one instance each is shared.</summary>
public static class CheckCatalog
{
    public static IReadOnlyList<ICheck> All { get; } =
    [
        new TitleMissing(), new TitleLength(), new TitleDuplicate(),
        new DescriptionMissing(), new DescriptionLength(), new DescriptionDuplicate(),
        new H1Missing(), new H1Multiple(), new ThinContent(),
        new BrokenInternalLink(), new RedirectedInternalLink(), new OgMissing(), new TranslationMissing(),
        new StatusError(), new HiddenButBlocked(), new CanonicalMissing(), new CanonicalMismatch(),
        new HreflangMissing(), new HreflangInvalid(), new HreflangNoXDefault(), new LangMismatch(),
        new StructuredDataInvalid(), new SlowResponse(), new HeavyHtml(), new SitemapMissingPage(), new SitemapBadEntry(),
        new RobotsMissing(), new SitemapMissing(), new HttpsRedirect(), new HostDuplicate(), new HomeHidden(), new SiteHidden(),
    ];

    public static IReadOnlyDictionary<string, CheckInfo> Info { get; } = All.ToDictionary(c => c.Info.Id, c => c.Info);
}
