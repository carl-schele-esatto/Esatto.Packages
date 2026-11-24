using Esatto.Umbraco.Backoffice.SeoReport.Api;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Jobs;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Xunit;
using static Esatto.Umbraco.Backoffice.SeoReport.Tests.Support.ApiResults;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>Fetch now and the progress the dashboard polls. One fetch at a time is the services' job; here it is only passed on.</summary>
public partial class ControllerTests
{
    [Fact]
    public async Task Fetch_now_starts_a_backfill_as_a_person_and_answers_202_with_the_progress()
    {
        var rig = await ApiRig.CreateAsync();
        var progress = new FetchProgress(Running: true, Done: 0, Total: 0, CurrentPeriod: null, ErrorCode: null);
        rig.Spy.Progress = progress;

        var started = Body<StartFetchResponse>(await rig.Controller.StartFetch(), 202);

        Assert.Equal("started", started.Result);
        Assert.Equal(new FetchStatusResponse(true, 0, 0, null, null, null, null, null), started.Progress);
        Assert.Equal([FetchState.Manual], rig.Spy.Starts); // the kind and nothing else: the person's name never reaches the coordinator
    }

    [Fact]
    public async Task Fetch_now_while_a_backfill_is_running_is_a_409_alreadyRunning()
    {
        var rig = await ApiRig.CreateAsync();
        rig.Spy.Result = FetchStartResult.AlreadyRunning;

        var problem = Problem(await rig.Controller.StartFetch(), 409);

        Assert.Equal(ApiCodes.AlreadyRunning, Code(problem));
        Assert.Equal(rig.Http.TraceIdentifier, problem.Extensions["traceId"]);
    }

    [Fact]
    public async Task Fetch_now_before_the_package_is_set_up_is_a_409_notConfigured()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: false));
        rig.Spy.Result = FetchStartResult.NotConfigured;

        Assert.Equal(ErrorCodes.NotConfigured, Code(Problem(await rig.Controller.StartFetch(), 409)));
    }

    [Fact]
    public async Task Fetch_now_with_a_key_that_cannot_be_read_says_keyUnreadable()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: false));
        await rig.Harness.StoreRawProtectedKeyAsync(FakeServiceAccount.Json(), new SecretProtector(new EphemeralDataProtectionProvider()));
        rig.Harness.Time.Advance(TimeSpan.FromMinutes(2)); // the settings are cached for a minute
        rig.Spy.Result = FetchStartResult.NotConfigured;

        Assert.Equal(ErrorCodes.KeyUnreadable, Code(Problem(await rig.Controller.StartFetch(), 409)));
    }

    [Fact]
    public async Task Fetch_now_with_nothing_to_fetch_is_still_a_202_and_the_progress_is_passed_on_as_it_is()
    {
        var rig = await ApiRig.CreateAsync();
        rig.Spy.Progress = new FetchProgress(Running: false, Done: 0, Total: 0, CurrentPeriod: null, ErrorCode: null);

        var started = Body<StartFetchResponse>(await rig.Controller.StartFetch(), 202);

        Assert.Equal(0, started.Progress.Total);
        Assert.False(started.Progress.Running);
    }

    [Fact]
    public async Task Fetch_now_twice_in_a_row_is_refused_the_second_time_by_the_coordinator()
    {
        // The real coordinator, not the spy: the controller does not guard against a double click, the service does.
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(RealCoordinator: true));
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Reports.OnRefresh = (_, ct) => release.Task.WaitAsync(ct);

        var first = await rig.Controller.StartFetch();
        var second = await rig.Controller.StartFetch();

        Body<StartFetchResponse>(first, 202);
        Assert.Equal(ApiCodes.AlreadyRunning, Code(Problem(second, 409)));

        release.SetResult();
        await rig.Real.CurrentRun!.WaitAsync(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task The_progress_is_the_same_object_status_carries_with_the_last_failure_from_the_record()
    {
        var rig = await ApiRig.CreateAsync();
        var failed = new DateTimeOffset(2026, 8, 19, 5, 0, 0, TimeSpan.Zero);
        await rig.Harness.Store.SaveDocumentAsync(DocumentKeys.FetchState, new FetchState(failed, null, failed, ErrorCodes.Quota, FetchState.Manual).ToJson());
        rig.Spy.Progress = new FetchProgress(Running: false, Done: 11, Total: 13, CurrentPeriod: null, ErrorCode: ErrorCodes.Quota);

        var progress = Ok<FetchStatusResponse>(await rig.Controller.GetFetchProgress());
        var status = Ok<StatusResponse>(await rig.Controller.GetStatus(Ct));

        Assert.Equal(new FetchStatusResponse(false, 11, 13, null, ErrorCodes.Quota, ErrorCodes.Quota, null, failed), progress);
        Assert.Equal(progress, status.Fetch);
    }
}
