using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Installs the package's content model once Umbraco is up.
/// </summary>
/// <remarks>
/// The order is load-bearing and must not be reshuffled:
/// <list type="number">
///   <item>schema - the two dropdown data types are created and preloaded before the
///     <c>cookieDefinition</c> element type binds to them, and <c>cookieRegistry</c> is created
///     after element types exist;</item>
///   <item>dictionary - the banner's text;</item>
///   <item>content - the policy page, which needs the document type from step 1.</item>
/// </list>
/// A failure here must not take the site down - the backoffice is the place to fix a broken
/// schema - so it is logged and swallowed. Everything downstream degrades gracefully: the
/// resolver returns null when the document type is missing, and the text provider falls back to
/// the embedded resx when the dictionary items are absent.
/// </remarks>
internal sealed class CookieBannerInstallHandler(
    IRuntimeState runtimeState,
    CookieBannerSchemaInstaller schemaInstaller,
    CookieBannerDictionaryInstaller dictionaryInstaller,
    CookieBannerContentSeeder contentSeeder,
    ILogger<CookieBannerInstallHandler> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public async Task HandleAsync(
        UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtimeState.Level is not RuntimeLevel.Run)
        {
            logger.LogInformation(
                "Skipping the cookie banner install; runtime level is {Level}.", runtimeState.Level);
            return;
        }

        try
        {
            await schemaInstaller.InstallAsync();
            await dictionaryInstaller.InstallAsync();
            contentSeeder.EnsurePolicyPage();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Installing the cookie banner content model failed.");
        }
    }
}
