using Umbraco.Cms.Core.Models.PublishedContent;

namespace Esatto.Umbraco.Backoffice.CrossContent;

/// <summary>Default mapper for consumer-only installs: never exposes any content (every request 404s).</summary>
public sealed class NullCrossContentTeaserMapper : ICrossContentTeaserMapper
{
    public CrossContentTeaser? Map(IPublishedContent content, string? culture) => null;
}
