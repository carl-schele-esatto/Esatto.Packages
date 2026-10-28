using System.Data.Common;
using Esatto.Umbraco.Backoffice.Accessibility.Persistence;
using NPoco;
using NSubstitute;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Accessibility.Tests;

/// <summary>
/// Umbraco runs SQLite in WAL mode with deferred transactions: one that reads first, then writes after another
/// transaction committed, fails with SQLITE_BUSY_SNAPSHOT. Every store write must therefore start with a write.
/// </summary>
public class StoreWriteTests
{
    private sealed class UniqueKeyViolation : DbException;

    private static void AssertStartsWithAWrite(IDatabase db)
    {
        var first = db.ReceivedCalls().First();
        var name = first.GetMethodInfo().Name;
        Assert.True(name is "Execute" or "Insert" or "Update", $"The first database call was {name}, a read.");
        if (first.GetArguments().FirstOrDefault() is string sql) Assert.DoesNotMatch(@"^\s*SELECT", sql);
    }

    [Fact]
    public void Saving_a_page_starts_with_a_write()
    {
        var db = Substitute.For<IDatabase>();
        NPocoAccessibilityStore.SavePage(db, new ScanPageEntity { ScanId = 1, ContentKey = Guid.NewGuid(), Viewport = 1280 }, [new FindingEntity()]);
        AssertStartsWithAWrite(db);
    }

    [Fact]
    public void Saving_a_manual_result_starts_with_a_write()
    {
        var db = Substitute.For<IDatabase>();
        NPocoAccessibilityStore.UpsertManualResult(db, new ManualResultEntity { SiteKey = Guid.NewGuid(), CriterionId = "1.1.1", Status = "pass" });
        AssertStartsWithAWrite(db);
    }

    [Fact]
    public void Saving_settings_starts_with_a_write()
    {
        var db = Substitute.For<IDatabase>();
        NPocoAccessibilityStore.SaveSettings(db, new SettingsEntity { Id = 1, Json = "{}" });
        AssertStartsWithAWrite(db);
    }

    [Fact]
    public void A_manual_result_that_loses_the_insert_race_updates_the_winner()
    {
        var db = Substitute.For<IDatabase>();
        db.When(d => d.Insert(Arg.Any<ManualResultEntity>())).Do(_ => throw new UniqueKeyViolation());

        NPocoAccessibilityStore.UpsertManualResult(db, new ManualResultEntity { SiteKey = Guid.NewGuid(), CriterionId = "1.1.1", Status = "pass" });

        Assert.Equal(2, db.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "Execute" && ((string)c.GetArguments()[0]!).StartsWith("UPDATE")));
    }
}
