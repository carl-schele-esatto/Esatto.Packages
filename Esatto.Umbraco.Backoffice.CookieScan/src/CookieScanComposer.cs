using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Esatto.Umbraco.Backoffice.CookieScan;

/// <summary>
/// Wires the cookie scanner's merge endpoint into a site with no consumer-side registration:
/// <list type="bullet">
///   <item><see cref="CookieScanWriter"/>, scoped, because it uses <c>IContentService</c>.</item>
///   <item><see cref="CookieScanApiUserOptions"/>, bound from
///   <see cref="CookieScanApiUserOptions.SectionName"/>.</item>
///   <item><see cref="CookieScanApiUserSeeder"/>, scoped, for the host to run once after boot.</item>
/// </list>
/// Umbraco discovers <see cref="IComposer"/> implementations in every referenced assembly, so
/// installing the package is enough. The controller needs no registration of its own - it is found
/// by MVC's own assembly scan, the same way every other management-API controller is.
/// </summary>
/// <remarks>
/// The seeder is registered here but deliberately not <em>run</em> here. Creating the API user needs
/// a booted site - the user service, and OpenIddict's application store - and it swallows its own
/// failures so that a missing scanner credential can never take a site down. Those two facts
/// together are why the trigger stays in the host's hands as
/// <c>app.SeedCookieScanApiUserAsync()</c>: run from a boot notification instead, a seeder that ran
/// a moment too early would fail silently and leave an operator with a token endpoint that rejects
/// a client id nobody can see is missing. Moving it onto
/// <c>UmbracoApplicationStartedNotification</c> is the obvious simplification, and wants verifying
/// against a running site before it is worth the one line it saves.
/// </remarks>
public sealed class CookieScanComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddScoped<CookieScanWriter>();
        builder.Services.AddScoped<CookieScanApiUserSeeder>();

        // Bound from the package's own section. A site that keeps the scanner's settings somewhere
        // else calls ConfigureCookieScanApiUser with its own section afterwards; because
        // IServiceCollection.Configure is additive and runs in registration order, that later
        // binding wins for every value it actually sets and leaves the rest at its default.
        builder.Services.Configure<CookieScanApiUserOptions>(
            builder.Config.GetSection(CookieScanApiUserOptions.SectionName));
    }
}
