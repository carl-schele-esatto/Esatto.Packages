using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Esatto.Umbraco.Backoffice.AICosts;

/// <summary>Registers the AI → Analytics → Costs services. The tab itself is in umbraco-package.json.</summary>
public sealed class AICostsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) => builder.Services.AddAICosts();
}
