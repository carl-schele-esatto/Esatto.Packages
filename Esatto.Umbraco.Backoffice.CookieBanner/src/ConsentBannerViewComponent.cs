using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Renders the consent dialog. Invoked by <c>&lt;consent-banner /&gt;</c>.
/// </summary>
/// <remarks>
/// View components must be public types, and MVC activates them through
/// <see cref="ActivatorUtilities"/>, which only considers public constructors. A public constructor
/// cannot name this assembly's internal service interfaces (CS0051), so the policy-page resolver and
/// the text provider are pulled from <see cref="IServiceProvider"/> instead of the signature.
/// </remarks>
public sealed class ConsentBannerViewComponent : ViewComponent
{
    private readonly IConsentState _consent;
    private readonly IOptions<CookieBannerOptions> _options;
    private readonly IPublishedValueFallback _publishedValueFallback;
    private readonly ICookiePolicyPageResolver _policyPageResolver;
    private readonly IConsentTextProvider _text;

    // IConsentTextProvider is public, so it is injected normally. ICookiePolicyPageResolver is
    // internal, and a public constructor cannot declare an internal parameter type (CS0051), so
    // that one is resolved from the container instead. Make the interface public if this bothers
    // you - it is also a reasonable extension point for a consumer with its own lookup rule.
    public ConsentBannerViewComponent(
        IConsentState consent,
        IOptions<CookieBannerOptions> options,
        IPublishedValueFallback publishedValueFallback,
        IConsentTextProvider text,
        IServiceProvider services)
    {
        _consent = consent;
        _options = options;
        _publishedValueFallback = publishedValueFallback;
        _text = text;
        _policyPageResolver = services.GetRequiredService<ICookiePolicyPageResolver>();
    }

    public IViewComponentResult Invoke() => View(BuildModel());

    /// <summary>
    /// The whole of the component's behaviour, separated from <see cref="Invoke"/> so it can be
    /// tested without a ViewContext, view engine or temp-data provider.
    /// </summary>
    internal ConsentBannerViewModel BuildModel()
    {
        CookieBannerOptions settings = _options.Value;

        // Every step of this chain can be absent - no published cookiePolicy page, no cookies block
        // on it, an unparsable category on a block - so it must degrade to "no cookies declared for
        // this category" rather than throw or log on a visitor's first request.
        BlockListModel? blocks = _policyPageResolver.Resolve()
            ?.Value<BlockListModel>(_publishedValueFallback, "cookies");

        return new ConsentBannerViewModel(
            NeedsDecision: _consent.NeedsDecision,
            // Read through HasGranted, not Decision.Granted: a decision made against an older
            // PolicyVersion grants nothing, and pre-ticking its boxes would misreport the state.
            Granted: ConsentCategories.All.Where(_consent.HasGranted).ToHashSet(),
            CookiesByCategory: CookieRegistry.Group(
                CookieDeclarationMapper.FromBlockList(blocks, _publishedValueFallback)),
            CookieName: settings.CookieName,
            PolicyVersion: settings.PolicyVersion,
            EndpointPath: settings.EndpointPath,
            ConsentModeEnabled: string.IsNullOrWhiteSpace(settings.GoogleMeasurementId) is false,
            Text: _text.Get);
    }
}
