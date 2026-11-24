using Esatto.Umbraco.Backoffice.SeoReport.Jobs;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>The <c>fetchState</c> document: what a run records, in what shape, and what it never records.</summary>
public partial class FetchCoordinatorTests
{
    [Fact]
    public async Task A_run_that_archived_months_records_the_attempt_and_the_success_in_a_fixed_shape()
    {
        var rig = await Rig.ReadyAsync();

        await rig.RunAsync(FetchState.Scheduled);

        // The document is read back after a restart and by the job on another server: its names are a contract.
        Assert.Equal(
            "{\"lastAttemptUtc\":\"2026-08-20T10:00:00+00:00\",\"lastSuccessUtc\":\"2026-08-20T10:00:00+00:00\",\"lastFailureUtc\":null,\"lastErrorCode\":null,\"startedBy\":\"scheduled\"}",
            rig.Store.Documents[DocumentKeys.FetchState]);
    }

    [Fact]
    public async Task The_record_says_manual_for_a_person_and_never_says_who()
    {
        var rig = await Rig.ReadyAsync();

        await rig.RunAsync("Test User Name");

        Assert.Equal(FetchState.Manual, (await rig.StateAsync()).StartedBy);
        Assert.DoesNotContain("Test User Name", rig.Store.Documents[DocumentKeys.FetchState]);
        Assert.DoesNotContain("Test User Name", rig.Log.AllText);
        Assert.DoesNotContain("Test User Name", rig.Coordinator.Progress!.ToString());
    }

    [Fact]
    public async Task A_later_success_keeps_the_last_failure_as_history_and_lifts_the_hold_on_the_job()
    {
        var rig = await Rig.ReadyAsync();
        FailOn(rig, Period.Parse("2026-07"), await FailureForAsync("noAccess"));
        await rig.RunAsync();
        var failedAt = rig.Time.GetUtcNow();
        Assert.True((await rig.StateAsync()).IsBackingOff(failedAt));

        rig.Time.Advance(TimeSpan.FromMinutes(5));
        rig.Reports.OnRefresh = (_, _) => Task.CompletedTask;
        await rig.RunAsync();

        var state = await rig.StateAsync();
        Assert.Equal(failedAt, state.LastFailureUtc);
        Assert.Equal("noAccess", state.LastErrorCode);
        Assert.Equal(failedAt.AddMinutes(5), state.LastSuccessUtc);
        Assert.Equal(failedAt.AddMinutes(5), state.LastAttemptUtc);
        Assert.False(state.IsBackingOff(rig.Time.GetUtcNow()));
    }

    [Fact]
    public async Task A_run_in_which_one_month_failed_and_the_others_were_archived_still_holds_the_job_back()
    {
        var rig = await Rig.ReadyAsync();
        FailOn(rig, ThirdMonth, new InvalidOperationException("boom"));

        await rig.RunAsync();

        var state = await rig.StateAsync();
        Assert.Equal(state.LastFailureUtc, state.LastSuccessUtc);   // both are the end of the run: the success is not "after" the failure
        Assert.True(state.IsBackingOff(rig.Time.GetUtcNow()));
    }

    [Fact]
    public async Task A_run_with_nothing_to_do_leaves_an_earlier_record_alone()
    {
        var rig = await Rig.ReadyAsync();
        FailOn(rig, Period.Parse("2026-07"), await FailureForAsync("quota"));
        await rig.RunAsync();
        var before = rig.Store.Documents[DocumentKeys.FetchState];
        rig.Reports.Archive(ThirteenMonths);
        rig.Time.Advance(TimeSpan.FromHours(3));

        await rig.RunAsync();

        Assert.Equal(before, rig.Store.Documents[DocumentKeys.FetchState]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("{\"lastFailureUtc\":\"yesterday\"}")]
    [InlineData("null")]
    public void A_record_that_cannot_be_read_is_an_empty_one(string? json)
        => Assert.Equal(FetchState.Empty, FetchState.Parse(json));
}
