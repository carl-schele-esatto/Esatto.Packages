using Xunit;

namespace Backoffice.CrossContent.Tests;

public class CrossContentTextTests
{
    [Fact]
    public void StripHtml_RemovesTags_DecodesEntities_CollapsesWhitespace()
        => Assert.Equal("A & B C", CrossContentText.StripHtml("<p>A &amp; B</p>\n<p>C</p>"));

    [Fact]
    public void StripHtml_ReturnsEmpty_WhenNullOrWhitespace()
    {
        Assert.Equal(string.Empty, CrossContentText.StripHtml(null));
        Assert.Equal(string.Empty, CrossContentText.StripHtml("   "));
    }
}
