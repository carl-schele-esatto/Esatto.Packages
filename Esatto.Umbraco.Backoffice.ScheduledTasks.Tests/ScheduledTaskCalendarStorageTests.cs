using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NPoco;
using NSubstitute;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

/// <summary>How the settings store keeps calendar schedules and the per-server occurrence records.</summary>
public class ScheduledTaskCalendarStorageTests
{
    private const string TaskId = "recurring:Some.Job";
    private const string ThisServer = "WEB-A";

    private static readonly DateTime SavedUtc = new(2026, 10, 18, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 18, 8, 0, 0, TimeSpan.Zero));
    private readonly ScheduledTasksSchemaState _schema = new();
    private readonly IUmbracoDatabase _db = Substitute.For<IUmbracoDatabase>();
    private readonly ISqlContext _sqlContext = TestSqlSyntax.CreateContext();
    private readonly ILogger<ScheduledTaskSettingsStore> _logger = Substitute.For<ILogger<ScheduledTaskSettingsStore>>();
    private readonly ScheduledTaskSettingsStore _store;

    public ScheduledTaskCalendarStorageTests()
    {
        var scope = Substitute.For<IScope>();
        scope.Database.Returns(_db);
        scope.SqlContext.Returns(_sqlContext);
        var scopes = Substitute.For<IScopeProvider>();
        scopes.CreateScope(default, default, default, default, default, default, default).ReturnsForAnyArgs(scope);
        scopes.SqlContext.Returns(_sqlContext);

        _db.Fetch<ScheduledTaskSettingEntity>().Returns(_ => []);
        _db.Fetch<ScheduledTaskOccurrenceEntity>(Arg.Any<Sql>()).Returns(_ => []);
        _db.Update(Arg.Any<object>()).Returns(1);
        _store = new ScheduledTaskSettingsStore(scopes, _schema, _time, TestSupport.Options(), _logger) { ServerName = ThisServer };
        _schema.MarkReady();
    }

    private static ScheduledTaskSettingEntity Row(string? scheduleJson, bool isPaused = true) => new()
    {
        Id = 1,
        TaskId = TaskId,
        IsPaused = isPaused,
        ScheduleJson = scheduleJson,
        UpdatedUtc = new DateTime(2026, 10, 1),
    };

    private void Stored(ScheduledTaskSettingEntity row)
    {
        _db.Fetch<ScheduledTaskSettingEntity>().Returns([row]);
        _db.FirstOrDefault<ScheduledTaskSettingEntity>(Arg.Any<Sql>()).Returns(row);
    }

    private ScheduledTaskSettingEntity Written()
        => (ScheduledTaskSettingEntity)_db.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(IDatabase.Update) && c.GetArguments()[0] is ScheduledTaskSettingEntity)
            .GetArguments()[0]!;

    [Theory]
    [InlineData("""{"kind":"Hourly","time":"07:00:00","timeZone":"UTC"}""")]          // a kind from a newer version
    [InlineData("""{"kind":7,"time":"07:00:00","timeZone":"UTC"}""")]                 // an undefined kind
    [InlineData("""{"kind":"Daily","time":"07:00:00","timeZone":null}""")]
    [InlineData("""{"kind":"Weekly","time":"07:00:00","weekdays":[9],"timeZone":"UTC"}""")]
    [InlineData("""{"kind":""")]
    public void An_unreadable_stored_schedule_reads_as_none_and_is_logged_once(string json)
    {
        Stored(Row(json));

        Assert.Null(_store.Get(TaskId)!.Calendar);
        _time.Advance(TimeSpan.FromMinutes(5)); // reloaded
        Assert.Null(_store.Get(TaskId)!.Calendar);

        Assert.Single(_logger.ReceivedCalls(), c => c.GetMethodInfo().Name == nameof(ILogger.Log) && (LogLevel)c.GetArguments()[0]! == LogLevel.Warning);
    }

    [Fact]
    public void A_null_weekday_list_doesnt_break_the_overview()
    {
        Stored(Row("""{"kind":"Weekly","time":"07:00:00","weekdays":null,"timeZone":"UTC"}"""));

        var calendar = _store.Get(TaskId)!.Calendar!;

        Assert.Empty(calendar.Weekdays);
        Assert.Empty(calendar.ToDto().Weekdays!);
        Assert.Null(TestSupport.Calendars.NextOccurrenceUtc(calendar, SavedUtc)); // never runs, doesn't throw
    }

    [Fact]
    public void An_unrelated_save_keeps_an_unreadable_schedule_as_it_is()
    {
        const string json = """{"kind":"Hourly","minute":15,"timeZone":"UTC"}""";
        Stored(Row(json, isPaused: true));

        _store.Update(TaskId, s => s with { IsPaused = false }, "Carl"); // back to all defaults but the schedule

        _db.DidNotReceiveWithAnyArgs().Delete(default!);
        Assert.Equal(json, Written().ScheduleJson);
        Assert.False(Written().IsPaused);
    }

    [Fact]
    public void Saving_a_new_schedule_replaces_an_unreadable_one()
    {
        Stored(Row("""{"kind":"Hourly"}"""));
        var calendar = Calendars.MondaysAtSeven();

        _store.Update(TaskId, s => s with { Calendar = calendar }, "Carl");

        Assert.Contains("\"Weekly\"", Written().ScheduleJson);
    }

    [Fact]
    public void An_unrelated_save_keeps_a_readable_schedule_verbatim()
    {
        // E.g. a property a newer version added, which this version doesn't know.
        const string json = """{"kind":"Daily","time":"07:00:00","timeZone":"UTC","savedUtc":"2026-10-18T08:00:00Z","jitterMinutes":5}""";
        Stored(Row(json, isPaused: false));

        _store.Update(TaskId, s => s with { IsPaused = true }, "Carl");

        Assert.Equal(json, Written().ScheduleJson);
        Assert.True(Written().IsPaused);
    }

    [Fact]
    public void An_occurrence_is_recorded_for_this_server_without_touching_the_settings_row()
    {
        var occurrence = new DateTime(2026, 10, 19, 5, 0, 0, DateTimeKind.Utc);
        _store.GetAll(); // loaded

        _store.RecordServerOccurrence(TaskId, occurrence, SavedUtc);

        _db.Received(1).Insert(Arg.Is<ScheduledTaskOccurrenceEntity>(e =>
            e.TaskId == TaskId && e.Server == ThisServer && e.OccurrenceUtc == occurrence && e.ScheduleSavedTicks == SavedUtc.Ticks));
        _db.DidNotReceiveWithAnyArgs().Update(default(object)!);
        _db.DidNotReceiveWithAnyArgs().Update(default(object)!, default(IEnumerable<string>)!);
        Assert.Equal(occurrence, _store.GetServerOccurrence(TaskId, SavedUtc));
        Assert.Null(_store.GetServerOccurrence(TaskId, SavedUtc.AddMinutes(1))); // another schedule
    }

    [Fact]
    public void This_servers_row_is_found_by_task_and_server()
    {
        var sql = ScheduledTaskSettingsStore.FindServerOccurrenceSql(_sqlContext, TaskId, ThisServer);

        Assert.Contains("server", sql.SQL);
        Assert.Contains("WITH (UPDLOCK)", sql.SQL);
        Assert.Contains(ThisServer, sql.Arguments);
        Assert.Contains(ThisServer, ScheduledTaskSettingsStore.ServerOccurrencesSql(_sqlContext, ThisServer).Arguments);
    }

    [Fact]
    public void A_recorded_occurrence_never_moves_back()
    {
        var later = new DateTime(2026, 10, 26, 6, 0, 0);
        _db.FirstOrDefault<ScheduledTaskOccurrenceEntity>(Arg.Any<Sql>()).Returns(new ScheduledTaskOccurrenceEntity
        {
            Id = 3, TaskId = TaskId, Server = ThisServer, OccurrenceUtc = later, ScheduleSavedTicks = SavedUtc.Ticks,
        });

        _store.RecordServerOccurrence(TaskId, later.AddDays(-7), SavedUtc);                 // older occurrence
        _store.RecordServerOccurrence(TaskId, later.AddDays(7), SavedUtc.AddDays(-1));      // older schedule

        _db.DidNotReceiveWithAnyArgs().Update(default(object)!, default(IEnumerable<string>)!);
        _db.DidNotReceiveWithAnyArgs().Insert<ScheduledTaskOccurrenceEntity>(default!);
    }

    [Fact]
    public void A_newer_occurrence_updates_only_the_occurrence_columns()
    {
        _db.FirstOrDefault<ScheduledTaskOccurrenceEntity>(Arg.Any<Sql>()).Returns(new ScheduledTaskOccurrenceEntity
        {
            Id = 3, TaskId = TaskId, Server = ThisServer, OccurrenceUtc = new DateTime(2026, 10, 19, 5, 0, 0), ScheduleSavedTicks = SavedUtc.Ticks,
        });
        var next = new DateTime(2026, 10, 26, 6, 0, 0, DateTimeKind.Utc);

        _store.RecordServerOccurrence(TaskId, next, SavedUtc);

        _db.Received(1).Update(
            Arg.Is<object>(e => ((ScheduledTaskOccurrenceEntity)e).OccurrenceUtc == next),
            Arg.Is<IEnumerable<string>>(c => c.SequenceEqual(new[] { "occurrenceUtc", "scheduleSavedTicks" })));
    }

    [Fact]
    public void This_servers_occurrences_are_read_with_the_settings()
    {
        _db.Fetch<ScheduledTaskOccurrenceEntity>(Arg.Any<Sql>()).Returns([new ScheduledTaskOccurrenceEntity
        {
            Id = 3, TaskId = TaskId, Server = ThisServer, OccurrenceUtc = new DateTime(2026, 10, 19, 5, 0, 0), ScheduleSavedTicks = SavedUtc.Ticks,
        }]);

        Assert.Equal(new DateTime(2026, 10, 19, 5, 0, 0, DateTimeKind.Utc), _store.GetServerOccurrence(TaskId, SavedUtc));
    }
}
