using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Extensions;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Wires Esatto.Umbraco.Backoffice.CookieBanner into Umbraco.
/// </summary>
/// <remarks>
/// Composers are auto-discovered by Umbraco from any referenced assembly that has
/// <see cref="IComposer" /> implementations, so the request-time consent surface and the content
/// model install both work with NO consumer-side wiring. Only the two Razor tag helpers and
/// <c>app.UseCookieConsent()</c> are the consumer's job.
/// <para>
/// This type MUST stay public: Umbraco's TypeLoader only scans public composers, and an internal
/// one installs nothing while reporting nothing.
/// </para>
/// </remarks>
public sealed class CookieBannerComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // ---- request-time consent surface (added in the options/services task) ----
        builder.Services.AddCookieConsent();

        // ---- install-time content model (added here) ----
        // Singletons, mirroring the shape this was extracted from. The content type factory
        // carries a mutable data type cache populated by PreloadDataTypesAsync, so the schema
        // installer and the factory must be the same pair of instances for the whole boot.
        builder.Services.AddSingleton<CookieBannerContentTypeFactory>();
        builder.Services.AddSingleton<CookieBannerSchemaInstaller>();
        builder.Services.AddSingleton<CookieBannerDictionaryInstaller>();
        builder.Services.AddSingleton<CookieBannerContentSeeder>();

        // Scoped: the resolver memoises its answer for one request, so <consent-banner /> and the
        // policy template share a single lookup. TryAdd keeps it idempotent alongside
        // AddCookieConsent(), which a consumer may also call explicitly.
        builder.Services.TryAddScoped<ICookiePolicyPageResolver, CookiePolicyPageResolver>();

        // Started, not Starting: the content, content type, dictionary and language services this
        // handler drives are not usable during Starting.
        builder.AddNotificationAsyncHandler<
            UmbracoApplicationStartedNotification, CookieBannerInstallHandler>();

        // Keeps CookiePolicyPageResolver's runtime-cached page key from outliving the content it
        // describes - see that type's remarks.
        builder.AddNotificationHandler<
            ContentPublishedNotification, CookiePolicyPageCacheInvalidator>();
        builder.AddNotificationHandler<
            ContentUnpublishedNotification, CookiePolicyPageCacheInvalidator>();
        builder.AddNotificationHandler<
            ContentDeletedNotification, CookiePolicyPageCacheInvalidator>();
    }
}
