using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Esatto.Umbraco.Backoffice.TreeDragAndDrop;

/// <summary>
/// Runs after Umbraco's core registrations (composers run in AddComposers()), so the manifest
/// readers are already in the collection when the reader is inserted in front of them.
/// </summary>
public sealed class TreeDragAndDropComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) => builder.Services.AddTreeDragAndDrop();
}
