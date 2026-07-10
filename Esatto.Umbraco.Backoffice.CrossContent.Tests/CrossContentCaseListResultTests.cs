using Xunit;

namespace Esatto.Umbraco.Backoffice.CrossContent.Tests;

public class CrossContentCaseListResultTests
{
    [Fact]
    public void Create_UsesLabel_AndFallsBackToAlias()
    {
        var options = new[]
        {
            new CrossContentContentTypeOption { Alias = "casePage", Label = "Cases" },
            new CrossContentContentTypeOption { Alias = "articlePage" },            // no label
            new CrossContentContentTypeOption { Alias = "" },                       // skipped
        };

        var result = CrossContentCaseListResult.Create(options, []);

        Assert.Equal(2, result.Types.Count);
        Assert.Equal("Cases", result.Types[0].Label);
        Assert.Equal("articlePage", result.Types[1].Label);   // fallback to alias
    }
}
