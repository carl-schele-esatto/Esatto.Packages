using System.Text.Json.Serialization;

namespace Backoffice.CrossContent;

/// <summary>The cross-site teaser wire contract. Serialized by the producer and deserialized by the
/// consumer. camelCase: key, type, title, teaserText, teaserImage, ctaLabel, url (absolute URLs).</summary>
public sealed record CrossContentTeaser(
    Guid Key, string Type, string Title, string TeaserText,
    [property: JsonPropertyName("teaserImage")] string TeaserImageUrl,
    string CtaLabel, string Url);
