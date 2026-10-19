using Esatto.Umbraco.Backoffice.ScheduledTasks;
using Xunit;

namespace Esatto.Umbraco.Backoffice.ScheduledTasks.Tests;

public class ScheduledTaskProgressTests
{
    private readonly ScheduledTaskRunLog _runLog = new(TimeProvider.System, TestSupport.Options());

    [Fact]
    public void A_recurring_job_reports_progress_on_its_running_run()
    {
        var job = new ScriptedJob(TimeSpan.FromMinutes(5));
        var taskId = ScheduledTaskIdentity.ForRecurring(typeof(ScriptedJob));
        var runId = _runLog.Start(taskId, ScheduledTaskTrigger.Schedule, null);
        var progress = new ScheduledTaskProgress(_runLog);

        progress.Report(job, 40, "Checked 400 of 1000 articles");

        var current = _runLog.GetStats(taskId).Current!;
        Assert.Equal(runId, current.Id);
        Assert.Equal(40, current.Progress);
        Assert.Equal("Checked 400 of 1000 articles", current.ProgressMessage);
    }

    [Fact]
    public void A_distributed_job_is_found_by_its_name()
    {
        var job = new ScriptedDistributedJob("SitemapPing", TimeSpan.FromMinutes(1));
        var taskId = ScheduledTaskIdentity.ForDistributed("SitemapPing");
        _runLog.Start(taskId, ScheduledTaskTrigger.Manual, "Editor");

        new ScheduledTaskProgress(_runLog).Report(job, 75);

        Assert.Equal(75, _runLog.GetStats(taskId).Current!.Progress);
    }

    [Fact]
    public void Percentages_are_clamped_and_null_keeps_the_previous_value()
    {
        const string taskId = "recurring:X";
        _runLog.Start(taskId, ScheduledTaskTrigger.Schedule, null);

        _runLog.ReportProgress(taskId, 140, "Almost");
        Assert.Equal(100, _runLog.GetStats(taskId).Current!.Progress);

        _runLog.ReportProgress(taskId, -5, null);
        Assert.Equal(0, _runLog.GetStats(taskId).Current!.Progress);
        Assert.Equal("Almost", _runLog.GetStats(taskId).Current!.ProgressMessage);

        _runLog.ReportProgress(taskId, null, "Only the message");
        _runLog.ReportProgress(taskId, double.NaN, null);
        var current = _runLog.GetStats(taskId).Current!;
        Assert.Equal(0, current.Progress);
        Assert.Equal("Only the message", current.ProgressMessage);
    }

    [Fact]
    public void Reports_without_a_run_in_progress_are_ignored()
    {
        const string taskId = "recurring:X";
        Assert.False(_runLog.ReportProgress(taskId, 10, "Nothing running"));

        var runId = _runLog.Start(taskId, ScheduledTaskTrigger.Schedule, null);
        _runLog.Succeed(taskId, runId);
        Assert.False(_runLog.ReportProgress(taskId, 10, "Already finished"));
        Assert.Null(_runLog.GetStats(taskId).Last!.ProgressMessage);
    }

    [Fact]
    public void The_finished_run_keeps_its_last_progress_for_the_history()
    {
        const string taskId = "recurring:X";
        var runId = _runLog.Start(taskId, ScheduledTaskTrigger.Schedule, null);
        _runLog.ReportProgress(taskId, 40, "Checked 400 of 1000 articles");
        _runLog.Fail(taskId, runId, new InvalidOperationException("Database went away"));

        var dto = _runLog.GetRuns(taskId)[0].ToDto();
        Assert.Equal("failed", dto.Outcome);
        Assert.Equal(40, dto.Progress);
        Assert.Equal("Checked 400 of 1000 articles", dto.ProgressMessage);
    }

    [Fact]
    public void Long_messages_are_truncated()
    {
        const string taskId = "recurring:X";
        _runLog.Start(taskId, ScheduledTaskTrigger.Schedule, null);

        _runLog.ReportProgress(taskId, 1, new string('x', 2000));

        Assert.True(_runLog.GetStats(taskId).Current!.ProgressMessage!.Length <= 501);
    }

    [Fact]
    public void Other_objects_are_rejected_with_a_clear_message()
    {
        var error = Assert.Throws<ArgumentException>(() => ScheduledTaskIdentity.ForJob(new object(), "to report progress"));
        Assert.Contains("IRecurringBackgroundJob or IDistributedBackgroundJob to report progress", error.Message);
    }
}
