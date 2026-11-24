using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The list of target queries: the searches the site wants to be found for. Normalizing is a pure function, kept apart from the
/// store, because it carries the whole rulebook; and it runs on the way out as well as on the way in, so a list saved under older
/// rules follows today's. The rule that matters most: a blank term never comes out, because the analysis reads an empty term as
/// "contains" for every query and would report that the site performs on everything.
/// </summary>
public class TargetQueryServiceTests
{
    private static (TargetQueryService Service, InMemoryStore Store, CapturingLogger<TargetQueryService> Log) Create()
    {
        var store = new InMemoryStore();
        var log = new CapturingLogger<TargetQueryService>();
        return (new TargetQueryService(store, log), store, log);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Normalize
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Normalize_TrimsEveryTerm()
        => Assert.Equal(["web design"], TargetQueryService.Normalize(["  web design  "]));

    [Fact]
    public void Normalize_LowerCasesEveryTerm()
        => Assert.Equal(["umbraco developer"], TargetQueryService.Normalize(["Umbraco DEVELOPER"]));

    [Fact]
    public void Normalize_DropsBlankAndMissingTerms()
        => Assert.Equal(["umbraco", "seo"], TargetQueryService.Normalize(["umbraco", "", "   ", null, "\t", "seo"]));

    [Fact]
    public void Normalize_DropsDuplicatesAfterLowerCasing_AndKeepsTheFirst()
        => Assert.Equal(["umbraco", "seo"], TargetQueryService.Normalize(["Umbraco", "seo", "umbraco", " UMBRACO "]));

    [Fact]
    public void Normalize_KeepsTheEditorsOrder()
        => Assert.Equal(["seo", "web design", "umbraco"], TargetQueryService.Normalize(["seo", "web design", "umbraco"]));

    [Fact]
    public void Normalize_CollapsesWhiteSpaceInsideATerm_SoItCanMatchTheQueryItNames()
    {
        // Search Console's queries are single-spaced; a term pasted with a double space or a tab would never match one.
        Assert.Equal(["web design"], TargetQueryService.Normalize(["web   design", "web\tdesign", "Web " + (char)0xA0 + " Design"]));
    }

    [Fact]
    public void Normalize_DropsATermThatIsTooLong_InsteadOfCuttingIt()
    {
        // A cut term is a search nobody typed: it would quietly match queries nobody asked about.
        var tooLong = new string('a', ITargetQueryService.MaxTermLength + 1);

        Assert.Equal(["umbraco"], TargetQueryService.Normalize([tooLong, "umbraco"]));
    }

    [Fact]
    public void Normalize_KeepsATermOfExactlyTheLongestLength()
    {
        var exact = new string('a', ITargetQueryService.MaxTermLength);

        Assert.Equal([exact], TargetQueryService.Normalize([exact]));
    }

    [Fact]
    public void Normalize_MeasuresTheLengthAfterTrimmingAndCollapsing()
    {
        var padded = "  " + new string('a', ITargetQueryService.MaxTermLength) + "  ";

        Assert.Single(TargetQueryService.Normalize([padded]));
    }

    [Fact]
    public void Normalize_StopsAtTheTermLimit_KeepingTheFirstOnes()
    {
        var many = Enumerable.Range(1, ITargetQueryService.MaxTerms + 10).Select(i => $"term {i}").ToList();

        var result = TargetQueryService.Normalize(many);

        Assert.Equal(50, result.Count);
        Assert.Equal("term 1", result[0]);
        Assert.Equal("term 50", result[^1]);
    }

    [Fact]
    public void Normalize_CountsOnlyTermsThatSurvive_TowardsTheLimit()
    {
        var input = Enumerable.Range(1, 60).SelectMany(i => new[] { "", $"term {i}", $"TERM {i}" });

        Assert.Equal(50, TargetQueryService.Normalize(input).Count);
    }

    [Fact]
    public void Normalize_OfNull_IsEmpty() => Assert.Empty(TargetQueryService.Normalize(null));

    [Fact]
    public void TheLimits_AreFiftyTermsOfAHundredCharacters()
    {
        Assert.Equal(50, ITargetQueryService.MaxTerms);
        Assert.Equal(100, ITargetQueryService.MaxTermLength);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Save and Get
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Get_WithNothingSaved_IsEmpty()
    {
        var (service, _, _) = Create();

        Assert.Empty(await service.GetAsync());
    }

    [Fact]
    public async Task Save_StoresTheNormalizedList_AndReturnsIt()
    {
        var (service, store, _) = Create();

        var saved = await service.SaveAsync([" Web Design ", "", "web design", "SEO"]);

        Assert.Equal(["web design", "seo"], saved);
        Assert.Equal("""["web design","seo"]""", store.Documents[DocumentKeys.Targets]);
        Assert.Equal(["web design", "seo"], await service.GetAsync());
    }

    [Fact]
    public async Task Save_OfNullOrNothing_EmptiesTheList()
    {
        var (service, store, _) = Create();
        await service.SaveAsync(["seo"]);

        Assert.Empty(await service.SaveAsync(null));
        Assert.Empty(await service.GetAsync());
        Assert.Equal("[]", store.Documents[DocumentKeys.Targets]);
    }

    [Fact]
    public async Task Get_NormalizesAListThatWasSavedUnderOlderRules()
    {
        // What an earlier version may have stored: blanks, casing, a duplicate, a null.
        var (service, store, _) = Create();
        await store.SaveDocumentAsync(DocumentKeys.Targets, """["  UMBRACO ","","umbraco",null,"Web   Design"]""");

        Assert.Equal(["umbraco", "web design"], await service.GetAsync());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"a\":1}")]
    [InlineData("[1, 2]")]
    [InlineData("\"text\"")]
    public async Task Get_OfAnUnreadableDocument_IsEmpty_AndLogsNothingOfItsContent(string stored)
    {
        var (service, store, log) = Create();
        await store.SaveDocumentAsync(DocumentKeys.Targets, stored);

        Assert.Empty(await service.GetAsync());
        Assert.DoesNotContain(stored, log.AllText);
        Assert.Contains(log.Entries, entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Warning);
    }

    [Fact]
    public async Task Get_OfAStoredNullOrBlankDocument_IsEmpty()
    {
        var (service, store, _) = Create();

        await store.SaveDocumentAsync(DocumentKeys.Targets, "null");
        Assert.Empty(await service.GetAsync());

        await store.SaveDocumentAsync(DocumentKeys.Targets, "   ");
        Assert.Empty(await service.GetAsync());
    }

    [Fact]
    public async Task ABlankTerm_NeverComesOutOfGet_EvenFromAHandWrittenDocument()
    {
        var (service, store, _) = Create();
        await store.SaveDocumentAsync(DocumentKeys.Targets, """["", " ", "\t"]""");

        Assert.Empty(await service.GetAsync());
    }
}
