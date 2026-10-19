using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

public class ScheduledTaskLogCaptureTests
{
    private const string TaskId = "recurring:My.Job";

    private static readonly DateTimeOffset At = new(2026, 10, 18, 7, 0, 3, 120, TimeSpan.Zero);

    private readonly ScheduledTaskRunLog _runLog = new(new FakeTimeProvider(At), TestSupport.Options());
    private readonly ScheduledTaskLogCapture _sink = new();

    private static LogEvent Event(
        LogEventLevel level, string template, string? sourceContext = null, Exception? exception = null, params LogEventProperty[] properties)
    {
        var all = properties.ToList();
        if (sourceContext is not null) all.Add(new LogEventProperty("SourceContext", new ScalarValue(sourceContext)));
        return new LogEvent(At, level, exception, new MessageTemplateParser().Parse(template), all);
    }

    private Guid StartRun() => _runLog.Start(TaskId, ScheduledTaskTrigger.Schedule, null);

    private string? LogOf(Guid runId) => _runLog.GetRuns(TaskId).Single(r => r.Id == runId).Log;

    [Fact]
    public void A_line_has_the_utc_time_level_short_source_and_message()
    {
        var line = ScheduledTaskLogCapture.Format(Event(
            LogEventLevel.Information,
            "Imported {Count} products",
            "My.Jobs.ProductFeedImportJob",
            null,
            new LogEventProperty("Count", new ScalarValue(120))));

        Assert.Equal("2026-10-18T07:00:03.120Z INF ProductFeedImportJob: Imported 120 products", line);
    }

    [Fact]
    public void A_line_without_a_source_has_just_the_message()
        => Assert.Equal(
            "2026-10-18T07:00:03.120Z WRN Low on disk",
            ScheduledTaskLogCapture.Format(Event(LogEventLevel.Warning, "Low on disk")));

    [Fact]
    public void A_logged_exception_is_summed_up_on_the_line()
        => Assert.Equal(
            "2026-10-18T07:00:03.120Z ERR Feed: Import failed — HttpRequestException: 503",
            ScheduledTaskLogCapture.Format(Event(
                LogEventLevel.Error, "Import failed", "Feed", new HttpRequestException("503"))));

    [Theory]
    [InlineData(LogEventLevel.Verbose, "VRB")]
    [InlineData(LogEventLevel.Debug, "DBG")]
    [InlineData(LogEventLevel.Error, "ERR")]
    [InlineData(LogEventLevel.Fatal, "FTL")]
    public void Levels_are_written_as_three_letters(LogEventLevel level, string expected)
        => Assert.Contains($" {expected} ", ScheduledTaskLogCapture.Format(Event(level, "x")));

    [Fact]
    public void Events_inside_a_capture_go_to_its_run()
    {
        var runId = StartRun();

        using (ScheduledTaskLogCapture.Begin(_runLog, TaskId, runId, LogLevel.Information))
        {
            _sink.Emit(Event(LogEventLevel.Information, "Started"));
            _sink.Emit(Event(LogEventLevel.Warning, "Slow feed"));
        }

        Assert.Equal(
            "2026-10-18T07:00:03.120Z INF Started\n2026-10-18T07:00:03.120Z WRN Slow feed",
            LogOf(runId));
    }

    [Fact]
    public void Events_below_the_minimum_level_are_not_captured()
    {
        var runId = StartRun();

        using (ScheduledTaskLogCapture.Begin(_runLog, TaskId, runId, LogLevel.Warning))
        {
            _sink.Emit(Event(LogEventLevel.Information, "Routine"));
            _sink.Emit(Event(LogEventLevel.Error, "Broken"));
        }

        Assert.EndsWith("ERR Broken", LogOf(runId));
        Assert.DoesNotContain("Routine", LogOf(runId));
    }

    [Fact]
    public void Events_outside_a_capture_are_not_captured()
    {
        var runId = StartRun();
        using (ScheduledTaskLogCapture.Begin(_runLog, TaskId, runId, LogLevel.Information)) { }

        _sink.Emit(Event(LogEventLevel.Error, "Elsewhere"));

        Assert.Null(LogOf(runId));
    }

    [Fact]
    public async Task A_capture_follows_the_run_across_awaits()
    {
        var runId = StartRun();

        using (ScheduledTaskLogCapture.Begin(_runLog, TaskId, runId, LogLevel.Information))
        {
            await Task.Yield();
            await Task.Run(() => _sink.Emit(Event(LogEventLevel.Information, "From the thread pool")));
        }

        Assert.EndsWith("From the thread pool", LogOf(runId));
    }

    [Fact]
    public async Task Work_started_outside_a_capture_is_not_captured()
    {
        var runId = StartRun();
        var release = new TaskCompletionSource();
        var unrelated = Task.Run(async () =>
        {
            await release.Task;
            _sink.Emit(Event(LogEventLevel.Information, "Unrelated"));
        });

        using (ScheduledTaskLogCapture.Begin(_runLog, TaskId, runId, LogLevel.Information))
        {
            release.SetResult();
            await unrelated;
        }

        Assert.Null(LogOf(runId));
    }

    [Fact]
    public void Turning_capture_off_captures_nothing()
        => Assert.Null(ScheduledTaskLogCapture.Begin(_runLog, TaskId, StartRun(), LogLevel.None));

    [Fact]
    public void A_nested_capture_hands_back_to_the_outer_one()
    {
        var outer = StartRun();
        var inner = _runLog.Start("recurring:Other.Job", ScheduledTaskTrigger.Schedule, null);

        using (ScheduledTaskLogCapture.Begin(_runLog, TaskId, outer, LogLevel.Information))
        {
            using (ScheduledTaskLogCapture.Begin(_runLog, "recurring:Other.Job", inner, LogLevel.Information))
            {
                _sink.Emit(Event(LogEventLevel.Information, "Inner"));
            }

            _sink.Emit(Event(LogEventLevel.Information, "Outer"));
        }

        Assert.EndsWith("Outer", LogOf(outer));
        Assert.DoesNotContain("Inner", LogOf(outer));
        Assert.EndsWith("Inner", _runLog.GetRuns("recurring:Other.Job").Single().Log);
    }
}

public class ScheduledTaskHistoryWriterTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 18, 8, 0, 0, TimeSpan.Zero));
    private readonly InMemoryRunStore _store = new();
    private readonly ScheduledTasksOptions _options = new();
    private readonly ScheduledTaskRunLog _runLog;
    private readonly ScheduledTaskRunControl _control;

    public ScheduledTaskHistoryWriterTests()
    {
        _runLog = new ScheduledTaskRunLog(_time, TestSupport.Options(_options), _store);
        _control = new ScheduledTaskRunControl(_runLog, _time);
    }

    private ScheduledTaskHistoryWriter CreateWriter() => new(
        _runLog,
        _store,
        _control,
        Substitute.For<IScheduledTaskAuditLog>(),
        TestSupport.Options(_options),
        _time,
        NullLogger<ScheduledTaskHistoryWriter>.Instance);

    [Fact]
    public void Nothing_happens_until_the_table_exists()
    {
        _store.IsAvailable = false;
        var writer = CreateWriter();
        _runLog.Start("recurring:My.Job", ScheduledTaskTrigger.Schedule, null);

        writer.Tick();

        Assert.Empty(_store.Interrupted);
        Assert.Empty(_store.PrunedOlderThan);
        Assert.Equal(0, _store.SaveCalls);
    }

    [Fact]
    public void Runs_this_server_left_running_at_its_last_shutdown_are_closed_once()
    {
        var writer = CreateWriter();
        var started = _time.GetUtcNow().UtcDateTime;
        _time.Advance(TimeSpan.FromSeconds(5));

        writer.Tick();
        writer.Tick();

        Assert.Equal([(Environment.MachineName, started)], _store.Interrupted);
    }

    [Fact]
    public void A_tick_saves_the_changed_runs()
    {
        var writer = CreateWriter();
        var runId = _runLog.Start("recurring:My.Job", ScheduledTaskTrigger.Schedule, null);

        writer.Tick();

        Assert.Contains(runId, _store.Runs.Keys);
    }

    [Fact]
    public async Task What_is_left_is_saved_after_the_jobs_have_stopped()
    {
        var writer = CreateWriter();
        var runId = _runLog.Start("recurring:My.Job", ScheduledTaskTrigger.Schedule, null);

        await writer.StoppedAsync(CancellationToken.None);

        Assert.Contains(runId, _store.Runs.Keys);
    }

    [Fact]
    public void A_stop_requested_on_another_server_reaches_the_run_here()
    {
        var writer = CreateWriter();
        using var execution = _control.Begin("recurring:My.Job", CancellationToken.None);
        var runId = _runLog.Start("recurring:My.Job", ScheduledTaskTrigger.Schedule, null);
        using var attempt = execution.BeginAttempt(runId, null);
        writer.Tick();
        _store.RequestStop(runId, "Anna");

        writer.Tick();

        Assert.True(attempt.Token.IsCancellationRequested);
        Assert.Equal("Anna", execution.StopRequestedBy);
    }

    [Fact]
    public void Stop_requests_are_only_looked_for_while_something_runs_here()
    {
        var store = Substitute.For<IScheduledTaskRunStore>();
        store.IsAvailable.Returns(true);
        var writer = new ScheduledTaskHistoryWriter(
            _runLog, store, _control, Substitute.For<IScheduledTaskAuditLog>(), TestSupport.Options(_options), _time,
            NullLogger<ScheduledTaskHistoryWriter>.Instance);

        writer.Tick();
        store.DidNotReceiveWithAnyArgs().GetStopRequests(default!);

        using var execution = _control.Begin("recurring:My.Job", CancellationToken.None);
        writer.Tick();
        store.Received(1).GetStopRequests(Environment.MachineName);
    }

    [Fact]
    public void Old_runs_are_deleted_hourly()
    {
        _options.RunHistoryRetention = TimeSpan.FromDays(30);
        var writer = CreateWriter();

        writer.Tick();
        _time.Advance(TimeSpan.FromMinutes(59));
        writer.Tick();
        _time.Advance(TimeSpan.FromMinutes(1));
        writer.Tick();

        Assert.Equal(
            [new DateTime(2026, 9, 18, 8, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 18, 9, 0, 0, DateTimeKind.Utc)],
            _store.PrunedOlderThan);
    }

    [Fact]
    public void A_failing_step_does_not_stop_the_others()
    {
        var store = Substitute.For<IScheduledTaskRunStore>();
        store.IsAvailable.Returns(true);
        store.MarkInterrupted(default!, default).ReturnsForAnyArgs(_ => throw new InvalidOperationException("database unreachable"));
        var writer = new ScheduledTaskHistoryWriter(
            _runLog, store, _control, Substitute.For<IScheduledTaskAuditLog>(), TestSupport.Options(_options), _time,
            NullLogger<ScheduledTaskHistoryWriter>.Instance);

        writer.Tick();

        store.ReceivedWithAnyArgs(1).PruneOlderThan(default);
    }
}
