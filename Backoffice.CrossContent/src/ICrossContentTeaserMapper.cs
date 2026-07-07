using Umbraco.Cms.Core.Models.PublishedContent;

namespace Backoffice.CrossContent;

/// <summary>Implemented by a producer site: maps one of its published nodes to a cross-content teaser.
/// Return null if the node is not a "case" (the endpoint then 404s). The site decides which content
/// types qualify and how to build the absolute image + url.</summary>
public interface ICrossContentTeaserMapper
{
    CrossContentTeaser? Map(IPublishedContent content, string? culture);
}
