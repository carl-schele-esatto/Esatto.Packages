using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Esatto.Umbraco.Backoffice.BlockPreview;

/// <summary>Runs after Umbraco's core registrations, so the manifest readers already exist to insert in front of.</summary>
public sealed class BlockPreviewComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) => builder.Services.AddBlockPreview();
}
