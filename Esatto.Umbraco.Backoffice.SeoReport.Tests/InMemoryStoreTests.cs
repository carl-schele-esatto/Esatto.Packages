using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The in-memory store the service tests run against must behave like the tables, above all for the fetch lock:
/// a double that is more forgiving than the database would let a locking bug pass.
/// </summary>
public class InMemoryStoreTests
{
    private const string Fetch = "fetch";
    private static readonly TimeSpan TenMinutes = TimeSpan.FromMinutes(10);

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.Zero));

    private InMemoryStore NewStore() => new(_clock);

    [Fact]
    public async Task A_free_lock_is_acquired()
        => Assert.True(await NewStore().TryAcquireLockAsync(Fetch, "a", TenMinutes));

    [Fact]
    public async Task A_second_owner_is_refused_while_the_lock_is_held()
    {
        var store = NewStore();
        Assert.True(await store.TryAcquireLockAsync(Fetch, "a", TenMinutes));

        Assert.False(await store.TryAcquireLockAsync(Fetch, "b", TenMinutes));
        _clock.Advance(TimeSpan.FromMinutes(9));
        Assert.False(await store.TryAcquireLockAsync(Fetch, "b", TenMinutes));
    }

    [Fact]
    public async Task A_second_owner_gets_the_lock_once_it_has_expired()
    {
        var store = NewStore();
        await store.TryAcquireLockAsync(Fetch, "a", TenMinutes);

        _clock.Advance(TenMinutes); // the expiry instant itself counts as expired
        Assert.True(await store.TryAcquireLockAsync(Fetch, "b", TenMinutes));
        Assert.False(await store.TryAcquireLockAsync(Fetch, "a", TenMinutes)); // and now b holds it
    }

    [Fact]
    public async Task The_owner_acquiring_again_extends_the_expiry()
    {
        var store = NewStore();
        await store.TryAcquireLockAsync(Fetch, "a", TenMinutes);

        _clock.Advance(TimeSpan.FromMinutes(6));
        Assert.True(await store.TryAcquireLockAsync(Fetch, "a", TenMinutes)); // now runs to minute 16

        _clock.Advance(TimeSpan.FromMinutes(6)); // minute 12: past the original expiry, inside the extended one
        Assert.False(await store.TryAcquireLockAsync(Fetch, "b", TenMinutes));

        _clock.Advance(TimeSpan.FromMinutes(4)); // minute 16
        Assert.True(await store.TryAcquireLockAsync(Fetch, "b", TenMinutes));
    }

    [Fact]
    public async Task Release_by_a_different_owner_is_ignored()
    {
        var store = NewStore();
        await store.TryAcquireLockAsync(Fetch, "a", TenMinutes);

        await store.ReleaseLockAsync(Fetch, "b");

        Assert.False(await store.TryAcquireLockAsync(Fetch, "b", TenMinutes));
        Assert.True(await store.TryAcquireLockAsync(Fetch, "a", TenMinutes)); // still a's
    }

    [Fact]
    public async Task Release_by_the_owner_frees_the_lock()
    {
        var store = NewStore();
        await store.TryAcquireLockAsync(Fetch, "a", TenMinutes);

        await store.ReleaseLockAsync(Fetch, "a");

        Assert.True(await store.TryAcquireLockAsync(Fetch, "b", TenMinutes));
    }

    [Fact]
    public async Task Releasing_a_lock_nobody_holds_is_harmless()
    {
        var store = NewStore();
        await store.ReleaseLockAsync(Fetch, "a");
        Assert.True(await store.TryAcquireLockAsync(Fetch, "a", TenMinutes));
    }

    [Fact]
    public async Task Locks_with_different_names_are_independent()
    {
        var store = NewStore();
        await store.TryAcquireLockAsync(Fetch, "a", TenMinutes);

        Assert.True(await store.TryAcquireLockAsync("other", "b", TenMinutes));
    }

    [Fact]
    public async Task The_real_clock_is_the_default()
    {
        var store = new InMemoryStore();
        Assert.True(await store.TryAcquireLockAsync(Fetch, "a", TimeSpan.FromHours(1)));
        Assert.False(await store.TryAcquireLockAsync(Fetch, "b", TimeSpan.FromHours(1)));
    }

    [Theory]
    [InlineData("", "a", 10)]
    [InlineData(" ", "a", 10)]
    [InlineData(Fetch, "", 10)]
    [InlineData(Fetch, "a", 0)]
    [InlineData(Fetch, "a", -5)]
    public async Task A_lock_needs_a_name_an_owner_and_a_positive_lifetime(string name, string owner, int minutes)
        => await Assert.ThrowsAnyAsync<ArgumentException>(() => NewStore().TryAcquireLockAsync(name, owner, TimeSpan.FromMinutes(minutes)));

    [Fact]
    public async Task A_lock_name_or_owner_longer_than_its_column_is_refused()
    {
        var store = NewStore();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.TryAcquireLockAsync(new string('n', LockEntity.NameLength + 1), "a", TenMinutes));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.TryAcquireLockAsync(Fetch, new string('o', LockEntity.OwnerLength + 1), TenMinutes));
    }

    [Fact]
    public async Task A_report_is_stored_per_period_and_replaced_on_save()
    {
        var store = NewStore();
        var may = Period.Parse("2026-05");
        var june = Period.Parse("2026-06");

        Assert.Null(await store.GetReportAsync(may));

        await store.SaveReportAsync(ReportEntity.From(may, """{"v":1}""", new DateTime(2026, 6, 1, 6, 0, 0, DateTimeKind.Utc), 1));
        await store.SaveReportAsync(ReportEntity.From(june, """{"v":2}""", new DateTime(2026, 7, 1, 6, 0, 0, DateTimeKind.Utc), 1));
        await store.SaveReportAsync(ReportEntity.From(may, """{"v":3}""", new DateTime(2026, 6, 2, 6, 0, 0, DateTimeKind.Utc), 2));

        var storedMay = await store.GetReportAsync(may);
        Assert.NotNull(storedMay);
        Assert.Equal("2026-05", storedMay.Period);
        Assert.Equal("""{"v":3}""", storedMay.Json);
        Assert.Equal(2, storedMay.ContractVersion);
        Assert.Equal(new DateTime(2026, 6, 2, 6, 0, 0, DateTimeKind.Utc), storedMay.FetchedUtc);
        Assert.Equal("""{"v":2}""", (await store.GetReportAsync(june))!.Json);
    }

    [Fact]
    public async Task Changing_a_report_after_saving_it_does_not_change_the_stored_one()
    {
        var store = NewStore();
        var may = Period.Parse("2026-05");
        var entity = ReportEntity.From(may, """{"v":1}""", DateTime.UtcNow, 1);
        await store.SaveReportAsync(entity);

        entity.Json = "tampered";
        (await store.GetReportAsync(may))!.Json = "tampered too";

        Assert.Equal("""{"v":1}""", (await store.GetReportAsync(may))!.Json);
    }

    [Fact]
    public async Task A_report_whose_period_is_not_YYYY_MM_is_refused()
    {
        var store = NewStore();
        var entity = new ReportEntity { Period = "2026-5", Json = "{}", FetchedUtc = DateTime.UtcNow, ContractVersion = 1 };

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveReportAsync(entity));
        Assert.Empty(await store.ListPeriodsAsync());
    }

    [Fact]
    public async Task Periods_are_listed_oldest_first()
    {
        var store = NewStore();
        foreach (var text in new[] { "2026-06", "2025-12", "2026-01" })
            await store.SaveReportAsync(ReportEntity.From(Period.Parse(text), "{}", DateTime.UtcNow, 1));

        Assert.Equal([Period.Parse("2025-12"), Period.Parse("2026-01"), Period.Parse("2026-06")], await store.ListPeriodsAsync());
    }

    [Fact]
    public async Task A_note_is_stored_per_period_and_replaced_on_save()
    {
        var store = NewStore();
        var may = Period.Parse("2026-05");
        Assert.Null(await store.GetNoteAsync(may));

        await store.SaveNoteAsync(may, "campaign");
        await store.SaveNoteAsync(Period.Parse("2026-06"), "other month");
        await store.SaveNoteAsync(may, "campaign, revised");

        Assert.Equal("campaign, revised", await store.GetNoteAsync(may));
        Assert.Equal("other month", await store.GetNoteAsync(Period.Parse("2026-06")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task A_null_or_empty_note_deletes_the_row(string? text)
    {
        var store = NewStore();
        var may = Period.Parse("2026-05");
        await store.SaveNoteAsync(may, "campaign");

        await store.SaveNoteAsync(may, text);

        Assert.Null(await store.GetNoteAsync(may));
    }

    [Fact]
    public async Task A_note_longer_than_its_column_is_refused()
    {
        var store = NewStore();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.SaveNoteAsync(Period.Parse("2026-05"), new string('x', NoteEntity.MaxLength + 1)));
        await store.SaveNoteAsync(Period.Parse("2026-05"), new string('x', NoteEntity.MaxLength)); // the limit itself fits
    }

    [Fact]
    public async Task A_document_is_stored_by_key_replaced_on_save_and_deleted()
    {
        var store = NewStore();
        Assert.Null(await store.GetDocumentAsync("settings"));

        await store.SaveDocumentAsync("settings", """{"a":1}""");
        await store.SaveDocumentAsync("targets", """{"t":1}""");
        await store.SaveDocumentAsync("settings", """{"a":2}""");

        Assert.Equal("""{"a":2}""", await store.GetDocumentAsync("settings"));
        Assert.Equal("""{"t":1}""", await store.GetDocumentAsync("targets"));

        await store.DeleteDocumentAsync("settings");

        Assert.Null(await store.GetDocumentAsync("settings"));
        Assert.Equal("""{"t":1}""", await store.GetDocumentAsync("targets"));
        await store.DeleteDocumentAsync("settings"); // deleting a missing document is harmless
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task A_document_needs_a_key(string key)
        => await Assert.ThrowsAnyAsync<ArgumentException>(() => NewStore().SaveDocumentAsync(key, "{}"));

    [Fact]
    public async Task A_document_key_longer_than_its_column_is_refused()
        => await Assert.ThrowsAnyAsync<ArgumentException>(() => NewStore().SaveDocumentAsync(new string('k', DocumentEntity.KeyLength + 1), "{}"));
}
