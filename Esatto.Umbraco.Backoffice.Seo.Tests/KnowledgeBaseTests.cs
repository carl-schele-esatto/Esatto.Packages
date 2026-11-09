using System.Reflection;
using Esatto.Umbraco.Backoffice.Seo.Checks;
using Esatto.Umbraco.Backoffice.Seo.Knowledge;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Seo.Tests;

public class KnowledgeBaseTests
{
    private static IEnumerable<string> AllIds() => typeof(CheckIds)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral)
        .Select(f => (string)f.GetRawConstantValue()!);

    [Fact]
    public void There_are_32_check_ids()
        => Assert.Equal(32, AllIds().Distinct().Count());

    [Fact]
    public void Every_check_id_has_complete_english_text()
    {
        var kb = new KnowledgeBase();
        foreach (var id in AllIds())
        {
            Assert.True(kb.All.ContainsKey(id), $"missing text for {id}");
            var text = kb.Get(id);
            Assert.False(string.IsNullOrWhiteSpace(text.Title), id);
            Assert.False(string.IsNullOrWhiteSpace(text.Why), id);
            Assert.False(string.IsNullOrWhiteSpace(text.Fix), id);
        }
    }

    [Fact]
    public void Unknown_id_returns_generic_text()
    {
        var text = new KnowledgeBase().Get("no-such-check");
        Assert.Equal("no-such-check", text.Title);
        Assert.False(string.IsNullOrWhiteSpace(text.Fix));
    }
}
