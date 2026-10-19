using System.Text.Json.Nodes;
using Esatto.Umbraco.Backoffice.ScheduledTasks;
using NSubstitute;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

/// <summary>Moving editor settings between environments: export and import.</summary>
public class ScheduledTaskTransferTests
{
    private static readonly ScheduledTaskScheduleDto MondaysAtSeven = new("weekly", "07:00", ["Monday"], null, null, null, Calendars.Stockholm);

    private readonly ServiceHarness _harness = new();

    private static ScheduledTasksExport Document(params ScheduledTaskExportEntry[] tasks)
        => new(ScheduledTasksExport.FormatName, ScheduledTasksExport.CurrentVersion, DateTime.UtcNow, "STAGING", tasks);

    private static ScheduledTaskExportEntry Entry(
        string id,
        bool paused = false,
        double? intervalSeconds = null,
        ScheduledTaskScheduleDto? schedule = null,
        JsonObject? properties = null,
        ScheduledTaskRunPolicyDto? runPolicy = null)
        => new(id, null, paused, intervalSeconds, schedule, properties, runPolicy);

    private void NothingSaved() => _harness.Settings.DidNotReceiveWithAnyArgs().Update(default!, default!, default);

    private ScheduledTaskSettingState SavedFor(string taskId)
        => _harness.Settings.ChangeSavedFor(taskId, "Carl")(ScheduledTaskSettingState.Default);

    [Fact]
    public void An_export_lists_only_registered_tasks_with_settings()
    {
        _harness.Store(ServiceHarness.RecurringId, s => s with { IsPaused = true, PeriodOverride = TimeSpan.FromMinutes(10) });
        _harness.Store(ServiceHarness.RetryingId); // on its defaults
        _harness.Store("recurring:Gone.Job", s => s with { IsPaused = true });

        var export = _harness.Create().Export();

        Assert.Equal(ScheduledTasksExport.FormatName, export.Format);
        Assert.Equal(ScheduledTasksExport.CurrentVersion, export.Version);
        var entry = Assert.Single(export.Tasks);
        Assert.Equal(ServiceHarness.RecurringId, entry.Id);
        Assert.Equal("Scripted", entry.Name);
        Assert.True(entry.Paused);
        Assert.Equal(600, entry.IntervalSeconds);
    }

    [Fact]
    public void An_export_carries_the_calendar_properties_and_run_policy()
    {
        _harness.Store(ServiceHarness.FeedId, s => s with
        {
            PeriodOverride = TimeSpan.FromHours(3), // replaced by the calendar
            Calendar = Calendars.MondaysAtSeven(),
            PropertiesJson = """{"feed":"https://example.com/feed"}""",
            RunPolicy = new ScheduledTaskRunPolicy(2, null, TimeSpan.Zero),
        });

        var entry = Assert.Single(_harness.Create().Export().Tasks);

        Assert.Null(entry.IntervalSeconds);
        Assert.Equal(MondaysAtSeven with { Weekdays = entry.Schedule!.Weekdays }, entry.Schedule);
        Assert.Equal(["Monday"], entry.Schedule.Weekdays!);
        Assert.Equal("https://example.com/feed", (string?)entry.Properties!["feed"]);
        Assert.Equal(new ScheduledTaskRunPolicyDto(2, null, 0), entry.RunPolicy);
    }

    [Fact]
    public void A_file_of_another_format_is_refused()
    {
        var result = _harness.Create().Import(Document() with { Format = "something-else" }, apply: true, resetMissing: false, "Carl");

        Assert.False(result.Applied);
        Assert.True(result.HasErrors);
        Assert.Equal("This file isn't a Scheduled Tasks export.", result.Error);
    }

    [Fact]
    public void A_file_from_a_newer_version_is_refused()
    {
        var result = _harness.Create().Import(
            Document(Entry(ServiceHarness.RecurringId, paused: true)) with { Version = ScheduledTasksExport.CurrentVersion + 1 },
            apply: true, resetMissing: false, "Carl");

        Assert.False(result.Applied);
        Assert.Contains("newer version", result.Error);
        NothingSaved();
    }

    [Fact]
    public void A_preview_shows_the_changes_without_saving_them()
    {
        var result = _harness.Create().Import(
            Document(Entry(ServiceHarness.RecurringId, paused: true, intervalSeconds: 600)), apply: false, resetMissing: false, "Carl");

        Assert.False(result.Applied);
        Assert.False(result.HasErrors);
        var item = Assert.Single(result.Tasks);
        Assert.Equal("changed", item.Status);
        Assert.Equal(["Paused", "Schedule: Every 5 minutes → Every 10 minutes"], item.Changes);
        NothingSaved();
        _harness.Audit.DidNotReceiveWithAnyArgs().Write(default!, default!, default);
    }

    [Fact]
    public void Applying_saves_each_changed_task_and_records_it()
    {
        var result = _harness.Create().Import(
            Document(Entry(ServiceHarness.RecurringId, paused: true, intervalSeconds: 600)), apply: true, resetMissing: false, "Carl");

        Assert.True(result.Applied);
        var saved = SavedFor(ServiceHarness.RecurringId);
        Assert.True(saved.IsPaused);
        Assert.Equal(TimeSpan.FromMinutes(10), saved.PeriodOverride);
        _harness.Host.Received(1).SyncWithSettings();
        _harness.Audit.Received(1).Write(
            ServiceHarness.RecurringId, ScheduledTaskAuditActions.Imported, "Carl", "Paused; Schedule: Every 5 minutes → Every 10 minutes");
    }

    [Fact]
    public void Tasks_that_do_not_exist_here_are_listed_but_do_not_block_the_import()
    {
        var result = _harness.Create().Import(
            Document(Entry("recurring:Only.On.Staging", paused: true), Entry(ServiceHarness.RecurringId, paused: true)),
            apply: true, resetMissing: false, "Carl");

        Assert.True(result.Applied);
        Assert.Equal(["unknownTask", "changed"], result.Tasks.Select(t => t.Status));
    }

    [Fact]
    public void A_task_whose_settings_cannot_be_used_means_nothing_is_imported()
    {
        var result = _harness.Create().Import(
            Document(Entry(ServiceHarness.RecurringId, paused: true), Entry(ServiceHarness.FeedId, intervalSeconds: -5)),
            apply: true, resetMissing: false, "Carl");

        Assert.False(result.Applied);
        Assert.True(result.HasErrors);
        var invalid = result.Tasks.Single(t => t.Id == ServiceHarness.FeedId);
        Assert.Equal("invalid", invalid.Status);
        Assert.Equal(["The interval must be a positive number of seconds."], invalid.Errors);
        NothingSaved();
    }

    [Theory]
    [InlineData(0.5, "The interval must be between 1 second and 10 years.")]
    [InlineData(1e12, "The interval must be between 1 second and 10 years.")]
    public void An_interval_outside_the_allowed_range_is_invalid(double seconds, string expected)
    {
        var result = _harness.Create().Import(Document(Entry(ServiceHarness.FeedId, intervalSeconds: seconds)), false, false, "Carl");

        Assert.Equal([expected], Assert.Single(result.Tasks).Errors);
    }

    [Fact]
    public void A_distributed_interval_shorter_than_Umbracos_poll_is_invalid()
    {
        var result = _harness.Create().Import(Document(Entry(ServiceHarness.DistributedId, intervalSeconds: 2)), false, false, "Carl");

        Assert.Equal("invalid", Assert.Single(result.Tasks).Status);
    }

    [Fact]
    public void A_task_listed_twice_is_invalid()
    {
        var result = _harness.Create().Import(
            Document(Entry(ServiceHarness.RecurringId, paused: true), Entry(ServiceHarness.RecurringId)), true, false, "Carl");

        Assert.False(result.Applied);
        Assert.Equal(["The task is listed more than once."], result.Tasks[1].Errors);
    }

    [Fact]
    public void A_task_with_both_an_interval_and_a_calendar_is_invalid()
    {
        var result = _harness.Create().Import(
            Document(Entry(ServiceHarness.RecurringId, intervalSeconds: 600, schedule: MondaysAtSeven)), false, false, "Carl");

        Assert.Equal(["It has both an interval and a calendar schedule; keep one."], Assert.Single(result.Tasks).Errors);
    }

    [Fact]
    public void A_run_policy_outside_the_allowed_range_is_invalid()
    {
        var result = _harness.Create().Import(
            Document(Entry(ServiceHarness.RecurringId, runPolicy: new ScheduledTaskRunPolicyDto(20, null, null))), false, false, "Carl");

        Assert.Equal(["Retries must be between 0 and 10."], Assert.Single(result.Tasks).Errors);
    }

    [Fact]
    public void Settings_that_are_already_in_place_are_unchanged()
    {
        _harness.Store(ServiceHarness.RecurringId, s => s with { IsPaused = true, PeriodOverride = TimeSpan.FromMinutes(10) });

        var result = _harness.Create().Import(
            Document(Entry(ServiceHarness.RecurringId, paused: true, intervalSeconds: 600)), apply: true, resetMissing: false, "Carl");

        Assert.True(result.Applied);
        var item = Assert.Single(result.Tasks);
        Assert.Equal("unchanged", item.Status);
        Assert.Empty(item.Changes);
        NothingSaved();
    }

    [Fact]
    public void Exporting_and_importing_into_the_same_site_changes_nothing()
    {
        _harness.Store(ServiceHarness.FeedId, s => s with
        {
            Calendar = Calendars.MondaysAtSeven(),
            PropertiesJson = """{"feed":"https://example.com/feed"}""",
            RunPolicy = new ScheduledTaskRunPolicy(2, TimeSpan.FromMinutes(5), null),
        });
        _harness.Store(ServiceHarness.RetryingId, s => s with { IsPaused = true, RunPolicy = new ScheduledTaskRunPolicy(null, null, TimeSpan.Zero) });
        var service = _harness.Create();

        var result = service.Import(service.Export(), apply: true, resetMissing: true, "Carl");

        Assert.All(result.Tasks, t => Assert.Equal("unchanged", t.Status));
        Assert.Equal(2, result.Tasks.Count);
        NothingSaved();
    }

    [Fact]
    public void Reset_missing_puts_tasks_the_file_leaves_out_back_on_their_defaults()
    {
        _harness.Store(ServiceHarness.FeedId, s => s with { IsPaused = true });
        _harness.Store(ServiceHarness.RetryingId); // already on its defaults

        var result = _harness.Create().Import(Document(Entry(ServiceHarness.RecurringId, paused: true)), apply: true, resetMissing: true, "Carl");

        var reset = result.Tasks.Single(t => t.Id == ServiceHarness.FeedId);
        Assert.Equal("reset", reset.Status);
        Assert.Equal(["Resumed"], reset.Changes);
        Assert.DoesNotContain(result.Tasks, t => t.Id == ServiceHarness.RetryingId);
        Assert.True(_harness.Settings.ChangeSavedFor(ServiceHarness.FeedId, "Carl")(
            new ScheduledTaskSettingState(true, null, null)).IsDefault);
    }

    [Fact]
    public void The_same_calendar_keeps_when_it_was_saved()
    {
        // Its bookkeeping counts from then: a new save time would skip or repeat an occurrence.
        var saved = Calendars.MondaysAtSeven(savedUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _harness.Store(ServiceHarness.RecurringId, s => s with { Calendar = saved });

        _harness.Create().Import(
            Document(Entry(ServiceHarness.RecurringId, paused: true, schedule: MondaysAtSeven)), apply: true, resetMissing: false, "Carl");

        Assert.Equal(saved.SavedUtc, SavedFor(ServiceHarness.RecurringId).Calendar?.SavedUtc);
    }

    [Fact]
    public void A_new_calendar_counts_from_the_import()
    {
        var before = DateTime.UtcNow;

        _harness.Create().Import(Document(Entry(ServiceHarness.RecurringId, schedule: MondaysAtSeven)), apply: true, resetMissing: false, "Carl");

        var calendar = SavedFor(ServiceHarness.RecurringId).Calendar!;
        Assert.True(calendar.SavedUtc >= before);
        Assert.Equal(ScheduledTaskCalendarKind.Weekly, calendar.Kind);
    }

    [Fact]
    public void Properties_the_task_does_not_have_here_are_ignored_with_a_note()
    {
        var properties = new JsonObject { ["feed"] = "https://example.com/feed", ["retired"] = true };

        var result = _harness.Create().Import(Document(Entry(ServiceHarness.FeedId, properties: properties)), apply: true, resetMissing: false, "Carl");

        var item = Assert.Single(result.Tasks);
        Assert.Equal("changed", item.Status);
        Assert.Equal(["Properties: feed", "Ignored property “retired”: the task doesn't have it here."], item.Changes);
        var saved = JsonNode.Parse(SavedFor(ServiceHarness.FeedId).PropertiesJson!)!.AsObject();
        Assert.Equal(["feed"], saved.Select(p => p.Key));
        Assert.Equal("https://example.com/feed", (string?)saved["feed"]);
    }

    [Fact]
    public void Properties_for_a_task_without_properties_are_ignored_with_a_note()
    {
        var result = _harness.Create().Import(
            Document(Entry(ServiceHarness.RecurringId, properties: new JsonObject { ["feed"] = "x" })), false, false, "Carl");

        var item = Assert.Single(result.Tasks);
        Assert.Equal("unchanged", item.Status);
        Assert.Equal(["Ignored its properties: the task has none here."], item.Changes);
    }

    [Fact]
    public void A_run_policy_equal_to_the_jobs_defaults_is_not_stored()
    {
        _harness.Store(ServiceHarness.RetryingId, s => s with { IsPaused = true });

        _harness.Create().Import(
            Document(Entry(ServiceHarness.RetryingId, runPolicy: new ScheduledTaskRunPolicyDto(3, 120, 600))), apply: true, resetMissing: false, "Carl");

        Assert.Null(SavedFor(ServiceHarness.RetryingId).RunPolicy);
    }

    [Fact]
    public void A_changed_run_policy_is_described()
    {
        var result = _harness.Create().Import(
            Document(Entry(ServiceHarness.RecurringId, runPolicy: new ScheduledTaskRunPolicyDto(2, 30, null))), false, false, "Carl");

        Assert.Equal(
            ["Retries and time limit: no retries · no time limit → 2 retries, 30 seconds apart · no time limit"],
            Assert.Single(result.Tasks).Changes);
    }
}
