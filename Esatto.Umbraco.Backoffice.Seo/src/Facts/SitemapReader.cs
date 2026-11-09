using System.Xml;
using System.Xml.Linq;

namespace Esatto.Umbraco.Backoffice.Seo.Facts;

/// <summary>A sitemap file. <see cref="IsValid"/> is false for anything that isn't one (an empty urlset is valid).</summary>
public sealed record SitemapDocument(bool IsValid, bool IsIndex, IReadOnlyList<string> Locations)
{
    public static readonly SitemapDocument Empty = new(false, false, []);
}

public static class SitemapReader
{
    private static readonly XmlReaderSettings Settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit, // no entity expansion from a hostile file
        XmlResolver = null,
        IgnoreComments = true,
    };

    /// <summary>Parses a sitemap. Anything that isn't a sitemap (HTML error page, broken XML) is empty.</summary>
    public static SitemapDocument Parse(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return SitemapDocument.Empty;
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), Settings);
            var root = XDocument.Load(reader).Root;
            if (root is null) return SitemapDocument.Empty;

            var isIndex = root.Name.LocalName == "sitemapindex";
            if (!isIndex && root.Name.LocalName != "urlset") return SitemapDocument.Empty;

            var locations = root.Descendants()
                .Where(e => e.Name.LocalName == "loc")
                .Select(e => e.Value.Trim())
                .Where(v => v.Length > 0)
                .ToList();
            return new SitemapDocument(true, isIndex, locations);
        }
        catch (XmlException)
        {
            return SitemapDocument.Empty;
        }
    }
}
