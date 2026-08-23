using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Wires Esatto.Umbraco.Backoffice.CookieBanner into Umbraco's container.
/// </summary>
/// <remarks>
/// Composers are auto-discovered by Umbraco from any referenced assembly, so the service graph is
/// registered with no consumer-side wiring: a consumer's only code change is
/// <c>app.UseCookieConsent()</c> plus the two tag helpers in their layout. The registrations use
/// <c>TryAdd*</c>, so calling <c>AddCookieConsent()</c> explicitly as well is harmless.
/// </remarks>
public sealed class CookieBannerComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) => builder.Services.AddCookieConsent();
}
