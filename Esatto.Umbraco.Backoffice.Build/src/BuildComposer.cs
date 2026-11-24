using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Esatto.Umbraco.Backoffice.Build;

/// <summary>
/// Wires the package into Umbraco. Composers are auto-discovered from any referenced assembly, so the API needs no
/// consumer-side wiring; the dashboard itself is in umbraco-package.json.
/// </summary>
public sealed class BuildComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.Configure<BuildOptions>(builder.Config.GetSection(BuildOptions.SectionName));
        builder.Services.AddSingleton<BuildInfoService>();
    }
}
