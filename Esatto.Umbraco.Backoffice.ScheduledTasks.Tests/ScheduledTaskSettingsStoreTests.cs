using System.Data;
using System.Diagnostics.CodeAnalysis;
using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NPoco;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseModelDefinitions;
using Umbraco.Cms.Infrastructure.Persistence.SqlSyntax;
using Umbraco.Cms.Infrastructure.Scoping;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

public class ScheduledTaskSettingsStoreTests
{
    private const string TaskId = "distributed:NightlyExport";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 18, 8, 0, 0, TimeSpan.Zero));
    private readonly ScheduledTasksSchemaState _schema = new();
    private readonly IUmbracoDatabase _db = Substitute.For<IUmbracoDatabase>();
    private readonly ISqlContext _sqlContext = TestSqlSyntax.CreateContext();
    private readonly ScheduledTaskSettingsStore _store;

    public ScheduledTaskSettingsStoreTests()
    {
        var scope = Substitute.For<IScope>();
        scope.Database.Returns(_db);
        scope.SqlContext.Returns(_sqlContext);
        var scopes = Substitute.For<IScopeProvider>();
        scopes.CreateScope(default, default, default, default, default, default, default).ReturnsForAnyArgs(scope);
        scopes.SqlContext.Returns(_sqlContext);

        _db.Fetch<ScheduledTaskSettingEntity>().Returns(_ => []);
        _store = new ScheduledTaskSettingsStore(scopes, _schema, _time, TestSupport.Options(), NullLogger<ScheduledTaskSettingsStore>.Instance);
    }

    private static ScheduledTaskSettingEntity Row(bool isPaused = true, string? scheduleJson = null, DateTime? lastScheduledUtc = null) => new()
    {
        Id = 1,
        TaskId = TaskId,
        IsPaused = isPaused,
        ScheduleJson = scheduleJson,
        LastScheduledUtc = lastScheduledUtc,
        UpdatedUtc = new DateTime(2026, 10, 1),
    };

    private const string CalendarJson = """{"kind":"Daily","time":"07:00:00","timeZone":"UTC"}""";

    [Fact]
    public void Nothing_is_read_before_the_package_migration_has_run()
    {
        // At runtime level Run, but the table may still lack columns the store selects.
        Assert.Empty(_store.GetAll());
        Assert.True(_store.IsWaitingForDatabase);
        _db.DidNotReceive().Fetch<ScheduledTaskSettingEntity>();

        _schema.MarkReady();
        _db.Fetch<ScheduledTaskSettingEntity>().Returns([Row()]);

        Assert.True(_store.Get(TaskId)!.IsPaused);
        Assert.False(_store.IsWaitingForDatabase);
    }

    [Fact]
    public void A_failed_first_read_is_not_cached_as_no_settings_and_is_retried_soon()
    {
        _schema.MarkReady();
        var columnsAdded = false;
        _db.Fetch<ScheduledTaskSettingEntity>().Returns(_ => columnsAdded
            ? [Row()]
            : throw new InvalidOperationException("Invalid column name 'scheduleJson'."));

        Assert.Empty(_store.GetAll());
        Assert.True(_store.IsWaitingForDatabase);

        columnsAdded = true;
        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Empty(_store.GetAll()); // backing off
        Assert.True(_store.IsWaitingForDatabase);

        _time.Advance(ScheduledTaskSettingsStore.FirstLoadRetryDelay);
        Assert.True(_store.Get(TaskId)!.IsPaused); // well before the 30 s refresh interval
        Assert.False(_store.IsWaitingForDatabase);
    }

    [Fact]
    public void A_failed_reload_keeps_the_last_good_settings()
    {
        _schema.MarkReady();
        _db.Fetch<ScheduledTaskSettingEntity>().Returns([Row()]);
        Assert.NotNull(_store.Get(TaskId));

        _db.Fetch<ScheduledTaskSettingEntity>().Throws(new InvalidOperationException("Database down"));
        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.True(_store.Get(TaskId)!.IsPaused);
        Assert.False(_store.IsWaitingForDatabase);
    }

    [Fact]
    public void Refresh_reads_the_database_even_when_the_cache_is_fresh()
    {
        _schema.MarkReady();
        Assert.Empty(_store.GetAll());

        _db.Fetch<ScheduledTaskSettingEntity>().Returns([Row()]);
        Assert.Empty(_store.GetAll()); // cached
        _store.Refresh();

        Assert.NotNull(_store.Get(TaskId));
    }

    // Every job tick asks for its settings; a reload stuck on the database (a lock held elsewhere) must
    // not hold them all up. On 2026-10-30 that queued 15 job threads, Umbraco's cache sync among them.
    private static readonly TimeSpan NotHeldUp = TimeSpan.FromSeconds(5);

    /// <summary>Makes the next settings read wait for <c>release</c>, returning <paramref name="rows"/>.</summary>
    private (ManualResetEventSlim Entered, ManualResetEventSlim Release) BlockNextRead(params ScheduledTaskSettingEntity[] rows)
    {
        var entered = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        _db.Fetch<ScheduledTaskSettingEntity>().Returns(_ =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(30));
            return rows.ToList();
        });
        return (entered, release);
    }

    [Fact]
    public void A_reload_stuck_on_the_database_does_not_hold_up_other_callers()
    {
        _schema.MarkReady();
        _db.Fetch<ScheduledTaskSettingEntity>().Returns([Row()]);
        Assert.True(_store.Get(TaskId)!.IsPaused);
        _time.Advance(TimeSpan.FromMinutes(1));
        var (entered, release) = BlockNextRead(Row(isPaused: false));

        var reload = Task.Run(() => _store.GetAll());
        Assert.True(entered.Wait(NotHeldUp));
        var other = Task.Run(() => _store.Get(TaskId));

        Assert.True(other.Wait(NotHeldUp), "A second caller waited for the reload.");
        Assert.True(other.Result!.IsPaused); // the settings it had
        release.Set();
        Assert.True(reload.Wait(NotHeldUp));
        Assert.False(_store.Get(TaskId)!.IsPaused);
    }

    [Fact]
    public void While_the_first_read_is_stuck_other_callers_hear_the_settings_are_not_read_yet()
    {
        _schema.MarkReady();
        var (entered, release) = BlockNextRead(Row());

        var first = Task.Run(() => _store.GetAll());
        Assert.True(entered.Wait(NotHeldUp));
        var other = Task.Run(() => _store.GetAll());

        Assert.True(other.Wait(NotHeldUp), "A second caller waited for the first read.");
        Assert.Empty(other.Result);
        Assert.True(_store.IsWaitingForDatabase);
        release.Set();
        Assert.True(first.Wait(NotHeldUp));
        Assert.False(_store.IsWaitingForDatabase);
    }

    [Fact]
    public void A_save_does_not_wait_for_a_reload_and_is_not_overwritten_by_it()
    {
        _schema.MarkReady();
        _db.Fetch<ScheduledTaskSettingEntity>().Returns([Row()]);
        Assert.Null(_store.Get(TaskId)!.PeriodOverride);
        _time.Advance(TimeSpan.FromMinutes(1));
        // The reload read the row before the save below changed it.
        var (entered, release) = BlockNextRead(Row());
        _db.FirstOrDefault<ScheduledTaskSettingEntity>(Arg.Any<Sql>()).Returns(Row());
        _db.Update(Arg.Any<ScheduledTaskSettingEntity>()).Returns(1);
        var hourly = TimeSpan.FromHours(1);

        var reload = Task.Run(() => _store.GetAll());
        Assert.True(entered.Wait(NotHeldUp));
        var save = Task.Run(() => _store.Update(TaskId, s => s with { PeriodOverride = hourly }, "Carl"));

        Assert.True(save.Wait(NotHeldUp), "The save waited for the reload.");
        release.Set();
        Assert.True(reload.Wait(NotHeldUp));
        var savedRow = Row();
        savedRow.PeriodTicks = hourly.Ticks;
        _db.Fetch<ScheduledTaskSettingEntity>().Returns([savedRow]); // the database has the save by now
        Assert.Equal(hourly, _store.Get(TaskId)!.PeriodOverride);
    }

    [Fact]
    public void Saving_before_the_migration_has_run_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() => _store.Update(TaskId, s => s with { IsPaused = true }, "Carl"));
        _db.DidNotReceiveWithAnyArgs().Insert<ScheduledTaskSettingEntity>(default!);
    }

    [Fact]
    public void The_row_is_read_with_an_update_lock()
    {
        // So a read-modify-write on another server waits for this one and then sees its result.
        var sql = ScheduledTaskSettingsStore.FindForUpdateSql(_sqlContext, TaskId).SQL;

        Assert.Contains("WITH (UPDLOCK)", sql);
        Assert.Contains("taskId", sql);
    }

    [Fact]
    public void An_update_of_a_row_deleted_meanwhile_is_reapplied_rather_than_reported_as_saved()
    {
        _schema.MarkReady();
        _db.FirstOrDefault<ScheduledTaskSettingEntity>(Arg.Any<Sql>()).Returns(Row(), (ScheduledTaskSettingEntity?)null);
        _db.Update(Arg.Any<object>()).Returns(0); // the row was gone by the time of the UPDATE

        var saved = _store.Update(TaskId, s => s with { PeriodOverride = TimeSpan.FromHours(1) }, "Carl");

        _db.Received(1).Insert(Arg.Is<ScheduledTaskSettingEntity>(e => e.PeriodTicks == TimeSpan.FromHours(1).Ticks && !e.IsPaused));
        Assert.False(saved!.IsPaused);
    }

    [Fact]
    public void Recording_an_occurrence_does_not_bring_back_a_row_deleted_meanwhile()
    {
        _schema.MarkReady();
        _db.Fetch<ScheduledTaskSettingEntity>().Returns([Row(scheduleJson: CalendarJson)]);
        Assert.NotNull(_store.Get(TaskId));
        _db.FirstOrDefault<ScheduledTaskSettingEntity>(Arg.Any<Sql>()).Returns((ScheduledTaskSettingEntity?)null);

        _store.RecordScheduledOccurrence(TaskId, new DateTime(2026, 10, 18, 7, 0, 0, DateTimeKind.Utc));

        _db.DidNotReceiveWithAnyArgs().Update(default(object)!, default(IEnumerable<string>)!);
        _db.DidNotReceiveWithAnyArgs().Insert<ScheduledTaskSettingEntity>(default!);
        Assert.Null(_store.Get(TaskId));
    }

    [Fact]
    public void Recording_an_occurrence_never_moves_back_to_an_earlier_one()
    {
        _schema.MarkReady();
        var later = new DateTime(2026, 10, 18, 7, 0, 0);
        _db.FirstOrDefault<ScheduledTaskSettingEntity>(Arg.Any<Sql>()).Returns(Row(scheduleJson: CalendarJson, lastScheduledUtc: later));

        _store.RecordScheduledOccurrence(TaskId, later.AddDays(-1));

        _db.DidNotReceiveWithAnyArgs().Update(default(object)!, default(IEnumerable<string>)!);
    }

    [Fact]
    public void Recording_an_occurrence_writes_only_that_column()
    {
        _schema.MarkReady();
        _db.FirstOrDefault<ScheduledTaskSettingEntity>(Arg.Any<Sql>()).Returns(Row(scheduleJson: CalendarJson));
        _db.Update(Arg.Any<object>(), Arg.Any<IEnumerable<string>>()).Returns(1);
        var occurrence = new DateTime(2026, 10, 18, 7, 0, 0, DateTimeKind.Utc);

        _store.RecordScheduledOccurrence(TaskId, occurrence);

        _db.Received(1).Update(
            Arg.Is<object>(e => ((ScheduledTaskSettingEntity)e).LastScheduledUtc == occurrence),
            Arg.Is<IEnumerable<string>>(c => c.SequenceEqual(new[] { ScheduledTaskSettingEntity.LastScheduledColumn })));
        _db.DidNotReceive().Update(Arg.Any<object>());
    }
}

/// <summary>Just enough SQL syntax to build the store's queries; the update lock hint is SQL Server's.</summary>
internal sealed class TestSqlSyntax : SqlSyntaxProviderBase<TestSqlSyntax>
{
    public static ISqlContext CreateContext()
    {
        var mappers = new MapperCollection();
        var factory = new FluentPocoDataFactory((type, _) => new PocoDataBuilder(type, mappers).Init(), mappers);
        return new SqlContext(new TestSqlSyntax(), DatabaseType.SQLite, factory);
    }

    public override string ProviderName => "Test";

    public override IsolationLevel DefaultIsolationLevel => IsolationLevel.ReadCommitted;

    public override string DbProvider => "Test";

    public override Sql<ISqlContext> InsertForUpdateHint(Sql<ISqlContext> sql) => sql.Append(" WITH (UPDLOCK)");

    public override IEnumerable<Tuple<string, string, string, bool>> GetDefinedIndexes(IDatabase db) => throw new NotSupportedException();

    public override bool TryGetDefaultConstraint(IDatabase db, string? tableName, string columnName, [MaybeNullWhen(false)] out string constraintName)
        => throw new NotSupportedException();

    public override Sql<ISqlContext>.SqlJoinClause<ISqlContext> LeftJoinWithNestedJoin<TDto>(
        Sql<ISqlContext> sql, Func<Sql<ISqlContext>, Sql<ISqlContext>> nestedJoin, string? alias = null)
        => throw new NotSupportedException();

    public override Sql<ISqlContext> SelectTop(Sql<ISqlContext> sql, int top) => throw new NotSupportedException();

    public override void HandleCreateTable(IDatabase database, TableDefinition tableDefinition, bool skipKeysAndIndexes = false)
        => throw new NotSupportedException();

    protected override string? FormatSystemMethods(SystemMethods systemMethod) => throw new NotSupportedException();

    protected override string FormatIdentity(ColumnDefinition column) => throw new NotSupportedException();
}
