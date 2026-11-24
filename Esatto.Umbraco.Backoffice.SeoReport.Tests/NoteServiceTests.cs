using System.Text;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The note an editor writes for a month: the one ingredient no data source can produce (a banner that went in, an outage), kept
/// beside the archive and not inside it. The store holds the text; what the service decides is how it is cleaned.
/// </summary>
public class NoteServiceTests
{
    private static readonly Period July = Period.Parse("2026-07");

    private static (NoteService Service, InMemoryStore Store) Create()
    {
        var store = new InMemoryStore();
        return (new NoteService(store), store);
    }

    [Fact]
    public async Task Get_WithNothingSaved_IsNull()
    {
        var (service, _) = Create();

        Assert.Null(await service.GetAsync(July));
    }

    [Fact]
    public async Task Save_TrimsTheTextAndReturnsWhatWasSaved()
    {
        var (service, store) = Create();

        var saved = await service.SaveAsync(July, "  The banner changed version.  \n");

        Assert.Equal("The banner changed version.", saved);
        Assert.Equal("The banner changed version.", await store.GetNoteAsync(July));
        Assert.Equal("The banner changed version.", await service.GetAsync(July));
    }

    [Fact]
    public async Task Save_AtTheLimit_KeepsEverything_AndOneOverIsCutNotRefused()
    {
        var (service, store) = Create();

        var exact = new string('a', INoteService.MaxLength);
        Assert.Equal(exact, await service.SaveAsync(July, exact));

        var cut = await service.SaveAsync(July, exact + "b");

        Assert.Equal(exact, cut);
        Assert.Equal(exact, await store.GetNoteAsync(July));
    }

    [Fact]
    public async Task Save_OfAVeryLongText_IsTruncatedToTheLimit_NotRejected()
    {
        var (service, store) = Create();

        var saved = await service.SaveAsync(July, new string('x', 5000));

        Assert.Equal(1000, saved!.Length);
        Assert.Equal(1000, (await store.GetNoteAsync(July))!.Length);
    }

    [Fact]
    public void TheLimit_IsTheColumnsLimit()
    {
        Assert.Equal(1000, INoteService.MaxLength);
        Assert.Equal(NoteEntity.MaxLength, INoteService.MaxLength);
    }

    [Fact]
    public async Task Save_NeverCutsBetweenTheHalvesOfASurrogatePair()
    {
        // 999 letters and then a character outside the BMP (two UTF-16 units): cutting at 1000 would keep only its first half,
        // which is not valid text and makes the JSON the note is sent in fail.
        var (service, _) = Create();

        var saved = await service.SaveAsync(July, new string('a', INoteService.MaxLength - 1) + "\U0001F600 and more");

        Assert.Equal(new string('a', INoteService.MaxLength - 1), saved);
        _ = new UTF8Encoding(false, true).GetBytes(saved!); // throws on a lone surrogate
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t \r\n")]
    public async Task Save_OfNothingOrWhiteSpace_DeletesTheNote(string? text)
    {
        var (service, store) = Create();
        await service.SaveAsync(July, "Something worth saying.");

        var saved = await service.SaveAsync(July, text);

        Assert.Null(saved);
        Assert.Null(await store.GetNoteAsync(July));
        Assert.Null(await service.GetAsync(July));
    }

    [Fact]
    public async Task Get_CleansAStoredTextTheSameWay()
    {
        var (service, store) = Create();

        await store.SaveNoteAsync(July, "  stored with spaces  ");
        Assert.Equal("stored with spaces", await service.GetAsync(July));

        await store.SaveNoteAsync(July, "   "); // the store keeps white space; the service reads it as no note
        Assert.Null(await service.GetAsync(July));
    }

    [Fact]
    public async Task Notes_BelongToTheirOwnMonth()
    {
        var (service, _) = Create();

        await service.SaveAsync(Period.Parse("2026-06"), "June note");
        await service.SaveAsync(July, "July note");

        Assert.Equal("June note", await service.GetAsync(Period.Parse("2026-06")));
        Assert.Equal("July note", await service.GetAsync(July));
        Assert.Null(await service.GetAsync(Period.Parse("2026-08")));
    }

    [Fact]
    public async Task ADefaultPeriod_NeverReachesTheStore()
    {
        // default(Period) is year 0, month 0: the one value of the type that skips its own validation.
        var (service, store) = Create();

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(default));
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(default, "text"));

        Assert.DoesNotContain("text", ex.Message);
        Assert.Null(await store.GetNoteAsync(default));
    }
}
