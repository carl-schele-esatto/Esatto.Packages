using System.Data.Common;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Persistence.SqlServer.Services;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The control flow of the store's lock and upserts, against a scripted database (no real one): which statements run, in
/// what order, and what an error means. The ordinary refusal of a held lock must not throw, and a failed INSERT may only be
/// read as "lost the race" when the row is really there; anything else (a timeout, a permissions or connection failure) has
/// to surface instead of looking like "lock held" or a saved row.
/// </summary>
public class StoreFlowTests
{
    private static readonly SqlServerSyntaxProvider Syntax = new(Options.Create(new GlobalSettings()));
    private static readonly DateTime Now = new(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc);

    private sealed class FakeDbException(string message) : DbException(message);

    /// <summary>
    /// A substituted database whose UPDATE, INSERT and "does the row exist" calls come from queues, in the order the code
    /// makes them. <see cref="Calls"/> records which ran, so a test also pins what must not run.
    /// </summary>
    private sealed class ScriptedDatabase
    {
        private readonly Queue<Func<int>> _updates = new();
        private readonly Queue<Func<int>> _inserts = new();
        private bool _rowExists;

        public ScriptedDatabase()
        {
            Db = Substitute.For<IUmbracoDatabase>();
            Db.SqlContext.SqlSyntax.Returns(Syntax);

            Db.Execute(Arg.Any<string>(), Arg.Any<object[]>()).Returns(call => Run(call.Arg<string>()));
            Db.Insert(Arg.Any<ReportEntity>()).Returns(_ => (object)Run("INSERT"));
            Db.Insert(Arg.Any<NoteEntity>()).Returns(_ => (object)Run("INSERT"));
            Db.Insert(Arg.Any<DocumentEntity>()).Returns(_ => (object)Run("INSERT"));
            Db.SingleOrDefaultById<LockEntity>(Arg.Any<object>()).Returns(_ =>
            {
                Calls.Add("EXISTS");
                return _rowExists ? new LockEntity() : null;
            });
        }

        public IUmbracoDatabase Db { get; }

        public List<string> Calls { get; } = [];

        public ScriptedDatabase Update(int rows) => Update(() => rows);

        public ScriptedDatabase Update(Func<int> result) { _updates.Enqueue(result); return this; }

        public ScriptedDatabase Insert(int rows) => Insert(() => rows);

        public ScriptedDatabase Insert(Func<int> result) { _inserts.Enqueue(result); return this; }

        public ScriptedDatabase InsertThrows(Exception failure) => Insert(() => throw failure);

        public ScriptedDatabase RowExists(bool exists) { _rowExists = exists; return this; }

        private int Run(string sql)
        {
            var kind = sql.StartsWith("UPDATE", StringComparison.Ordinal) ? "UPDATE"
                : sql.StartsWith("INSERT", StringComparison.Ordinal) ? "INSERT"
                : throw new InvalidOperationException("Unexpected statement: " + sql);
            Calls.Add(kind);

            var queue = kind == "UPDATE" ? _updates : _inserts;
            return queue.Count > 0 ? queue.Dequeue()() : throw new InvalidOperationException($"Unscripted {kind} (call {Calls.Count}).");
        }
    }

    private static bool Acquire(ScriptedDatabase script)
        => NPocoSeoReportStore.TryAcquireLock(script.Db, "fetch", "a", Now, Now.AddMinutes(10));

    // --- The lock -----------------------------------------------------------------------------------------------------

    [Fact]
    public void A_lock_that_is_free_or_ours_is_taken_by_the_update_alone()
    {
        var script = new ScriptedDatabase().Update(1);

        Assert.True(Acquire(script));
        Assert.Equal(["UPDATE"], script.Calls);
    }

    [Fact]
    public void A_lock_with_no_row_is_taken_by_inserting_it()
    {
        var script = new ScriptedDatabase().Update(0).Insert(1);

        Assert.True(Acquire(script));
        Assert.Equal(["UPDATE", "INSERT"], script.Calls);
    }

    [Fact]
    public void A_lock_held_by_someone_else_is_refused_without_an_exception()
    {
        // The row exists and is live: the update misses, the conditional insert adds nothing, the retry update misses.
        var script = new ScriptedDatabase().Update(0).Insert(0).Update(0);

        Assert.False(Acquire(script));
        Assert.Equal(["UPDATE", "INSERT", "UPDATE"], script.Calls);
    }

    [Fact]
    public void A_lock_that_lapsed_between_the_two_statements_is_taken_by_the_retry()
    {
        var script = new ScriptedDatabase().Update(0).Insert(0).Update(1);

        Assert.True(Acquire(script));
    }

    [Fact]
    public void Losing_the_insert_race_is_a_refusal_when_the_winners_row_is_there()
    {
        var script = new ScriptedDatabase().Update(0).InsertThrows(new FakeDbException("duplicate key")).RowExists(true).Update(0);

        Assert.False(Acquire(script));
        Assert.Equal(["UPDATE", "INSERT", "EXISTS", "UPDATE"], script.Calls);
    }

    [Fact]
    public void Losing_the_insert_race_to_the_same_owner_still_holds_the_lock()
    {
        var script = new ScriptedDatabase().Update(0).InsertThrows(new FakeDbException("duplicate key")).RowExists(true).Update(1);

        Assert.True(Acquire(script));
    }

    [Fact]
    public void An_insert_that_fails_with_no_row_behind_it_is_not_a_held_lock()
    {
        var failure = new FakeDbException("lock request time out period exceeded");
        var script = new ScriptedDatabase().Update(0).InsertThrows(failure).RowExists(false);

        var thrown = Assert.Throws<FakeDbException>(() => Acquire(script));

        Assert.Same(failure, thrown);
        Assert.Equal(["UPDATE", "INSERT", "EXISTS"], script.Calls); // no retry, nothing reported as "held"
    }

    [Fact]
    public void An_insert_that_fails_for_another_kind_of_reason_propagates_unprobed()
    {
        var failure = new InvalidOperationException("connection closed");
        var script = new ScriptedDatabase().Update(0).InsertThrows(failure);

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => Acquire(script)));
        Assert.Equal(["UPDATE", "INSERT"], script.Calls);
    }

    [Fact]
    public void A_failing_first_update_propagates()
    {
        var failure = new FakeDbException("permission denied");
        var script = new ScriptedDatabase().Update(() => throw failure);

        Assert.Same(failure, Assert.Throws<FakeDbException>(() => Acquire(script)));
        Assert.Equal(["UPDATE"], script.Calls);
    }

    // --- The upserts (report, note, document) -------------------------------------------------------------------------

    public static TheoryData<string> Upserts => new() { "report", "note", "document" };

    private static void Upsert(string which, IUmbracoDatabase db)
    {
        switch (which)
        {
            case "report":
                NPocoSeoReportStore.UpsertReport(db, ReportEntity.From(new Period(2026, 5), "{}", Now, 1));
                break;
            case "note":
                NPocoSeoReportStore.UpsertNote(db, new NoteEntity { Period = "2026-05", Text = "x" });
                break;
            default:
                NPocoSeoReportStore.UpsertDocument(db, new DocumentEntity { Key = "settings", Json = "{}", UpdatedUtc = Now });
                break;
        }
    }

    [Theory]
    [MemberData(nameof(Upserts))]
    public void A_save_over_an_existing_row_only_updates(string which)
    {
        var script = new ScriptedDatabase().Update(1);

        Upsert(which, script.Db);

        Assert.Equal(["UPDATE"], script.Calls);
    }

    [Theory]
    [MemberData(nameof(Upserts))]
    public void A_save_with_no_row_inserts_after_the_update_finds_nothing(string which)
    {
        var script = new ScriptedDatabase().Update(0).Insert(1);

        Upsert(which, script.Db);

        Assert.Equal(["UPDATE", "INSERT"], script.Calls);
    }

    [Theory]
    [MemberData(nameof(Upserts))]
    public void Losing_the_insert_race_updates_the_winners_row(string which)
    {
        var script = new ScriptedDatabase().Update(0).InsertThrows(new FakeDbException("duplicate key")).Update(1);

        Upsert(which, script.Db);

        Assert.Equal(["UPDATE", "INSERT", "UPDATE"], script.Calls);
    }

    [Theory]
    [MemberData(nameof(Upserts))]
    public void A_failed_insert_that_the_retry_cannot_explain_propagates_instead_of_reporting_a_save(string which)
    {
        var failure = new FakeDbException("lock request time out period exceeded");
        var script = new ScriptedDatabase().Update(0).InsertThrows(failure).Update(0);

        var thrown = Assert.Throws<FakeDbException>(() => Upsert(which, script.Db));

        Assert.Same(failure, thrown);
        Assert.Equal(["UPDATE", "INSERT", "UPDATE"], script.Calls);
    }

    [Theory]
    [MemberData(nameof(Upserts))]
    public void An_insert_failing_for_another_kind_of_reason_propagates_without_a_retry(string which)
    {
        var failure = new InvalidOperationException("connection closed");
        var script = new ScriptedDatabase().Update(0).InsertThrows(failure);

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => Upsert(which, script.Db)));
        Assert.Equal(["UPDATE", "INSERT"], script.Calls);
    }
}
