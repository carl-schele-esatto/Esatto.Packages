using System.Net;
using Esatto.Umbraco.Backoffice.SeoReport.Api;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Jobs;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Xunit;
using static Esatto.Umbraco.Backoffice.SeoReport.Tests.Support.ApiResults;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>The Settings endpoints: reading and saving the settings and the key, Test connection, the user groups, and what a save sets going.</summary>
public partial class ControllerTests
{
    private static SeoReportSettings Valid(Action<SeoReportSettings>? change = null)
    {
        var settings = new SeoReportSettings { Ga4PropertyId = "123456789", SearchConsoleSite = "example.com" };
        change?.Invoke(settings);
        return settings;
    }

    private static void AssertNoStore(ApiRig rig)
        => Assert.Contains("no-store", rig.Http.Response.Headers.CacheControl.ToString());

    // ---- reading --------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_settings_come_with_the_locked_fields_and_what_is_known_about_the_key_and_never_the_key()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: true));
        rig.Configure(nameof(SeoReportSettings.DisplayName), "From configuration");

        var response = Ok<SettingsResponse>(await rig.Controller.GetSettings());

        Assert.Equal(ReadySettingsHarness.Ga4Property, response.Settings.Ga4PropertyId);
        Assert.Equal(ReadySettingsHarness.Site, response.Settings.SearchConsoleSite);
        Assert.Equal("From configuration", response.Settings.DisplayName);
        Assert.Equal(["displayName"], response.Locked);
        Assert.Equal(new KeyStatus(true, KeySourceKind.Database, FakeServiceAccount.DefaultClientEmail, false), response.Key);
        Assert.DoesNotContain("PRIVATE KEY", ManagementJson.Serialize(response));
        AssertNoStore(rig);
    }

    // ---- saving ---------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("abc", "example.com", null, ErrorCodes.Ga4PropertyInvalid)]
    [InlineData("123456789", "has spaces.com", null, ErrorCodes.SearchConsoleInvalid)]
    [InlineData("123456789", "example.com", "not a key", ErrorCodes.KeyInvalid)]
    public async Task A_save_that_is_refused_is_a_400_with_the_code_and_saves_nothing(string ga4, string site, string? key, string code)
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: false));
        var before = await rig.Harness.StoredSettingsAsync();

        var problem = Problem(
            await rig.Controller.PutSettings(new PutSettingsRequest(new SeoReportSettings { Ga4PropertyId = ga4, SearchConsoleSite = site }, key, false), Ct),
            400);

        Assert.Equal(code, Code(problem));
        Assert.Equal(rig.Http.TraceIdentifier, problem.Extensions["traceId"]);
        Assert.Equivalent(before, await rig.Harness.StoredSettingsAsync());
        Assert.Empty(rig.Spy.Starts);
        AssertNoStore(rig); // the refusal is not cached either
    }

    [Fact]
    public async Task Saving_a_valid_key_says_that_there_is_one_and_whose_it_is_and_never_gives_it_back()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: false));
        var key = FakeServiceAccount.Json();

        var response = Ok<SettingsResponse>(await rig.Controller.PutSettings(new PutSettingsRequest(Valid(), key, false), Ct));

        Assert.True(response.Key.HasKey);
        Assert.Equal(FakeServiceAccount.DefaultClientEmail, response.Key.ServiceAccountEmail);
        Assert.Equal(KeySourceKind.Database, response.Key.Source);
        Assert.False(response.Key.Unreadable);
        Assert.Equal("properties/123456789", response.Settings.Ga4PropertyId);
        Assert.Equal("sc-domain:example.com", response.Settings.SearchConsoleSite);

        var json = ManagementJson.Serialize(response);
        Assert.DoesNotContain("PRIVATE KEY", json);
        Assert.DoesNotContain(FakeServiceAccount.PrivateKeyPem.Split('\n')[1], json);
        AssertNoStore(rig);
    }

    [Fact]
    public async Task Removing_the_key_leaves_the_package_without_one()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: true));

        var response = Ok<SettingsResponse>(await rig.Controller.PutSettings(new PutSettingsRequest(Valid(), null, RemoveKey: true), Ct));

        Assert.False(response.Key.HasKey);
        Assert.Equal(KeySourceKind.None, response.Key.Source);
    }

    [Fact]
    public async Task A_save_with_no_body_or_no_settings_is_a_400()
    {
        var rig = await ApiRig.CreateAsync();

        Assert.Equal(ApiCodes.RequestInvalid, Code(Problem(await rig.Controller.PutSettings(null, Ct), 400)));
        Assert.Equal(ApiCodes.RequestInvalid, Code(Problem(await rig.Controller.PutSettings(new PutSettingsRequest(null!, null, false), Ct), 400)));
        AssertNoStore(rig);
    }

    // ---- what a save sets going -----------------------------------------------------------------------------------------

    [Fact]
    public async Task A_save_whose_connection_check_passes_starts_the_backfill_once_as_a_person_does()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: false));

        Ok<SettingsResponse>(await rig.Controller.PutSettings(new PutSettingsRequest(Valid(), FakeServiceAccount.Json(), false), Ct));

        Assert.Equal([FetchState.Manual], rig.Spy.Starts);
        Assert.True(rig.Google.Calls > 0); // it really did check, with the key and properties that were just saved
    }

    [Fact]
    public async Task A_source_that_is_connected_but_has_no_data_yet_does_not_hold_the_backfill_back()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: false));
        rig.Ga4Answer = _ => new FakeGoogleTransport.Answer(HttpStatusCode.OK, "{}");

        Ok<SettingsResponse>(await rig.Controller.PutSettings(new PutSettingsRequest(Valid(), FakeServiceAccount.Json(), false), Ct));

        Assert.Equal([FetchState.Manual], rig.Spy.Starts);
    }

    [Theory]
    [InlineData("ga4")]
    [InlineData("searchConsole")]
    public async Task A_save_whose_connection_check_fails_is_saved_but_starts_nothing(string failing)
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: false));
        if (failing == "ga4") rig.Ga4Answer = _ => GoogleErrorBodies.DeniedAnswer();
        else rig.SearchConsoleAnswer = _ => GoogleErrorBodies.DeniedAnswer();

        var response = Ok<SettingsResponse>(await rig.Controller.PutSettings(new PutSettingsRequest(Valid(), FakeServiceAccount.Json(), false), Ct));

        Assert.True(response.Key.HasKey); // the settings are saved: the check never fails the save
        Assert.Empty(rig.Spy.Starts);
    }

    [Fact]
    public async Task A_save_without_a_key_starts_nothing_and_asks_google_nothing()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: false));

        Ok<SettingsResponse>(await rig.Controller.PutSettings(new PutSettingsRequest(Valid(), null, false), Ct));

        Assert.Empty(rig.Spy.Starts);
        Assert.Equal(0, rig.Google.Calls);
    }

    [Theory]
    [InlineData(FetchStartResult.AlreadyRunning)]
    [InlineData(FetchStartResult.NotConfigured)]
    public async Task A_start_that_is_refused_does_not_change_the_answer_to_the_save(FetchStartResult refused)
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: false));
        rig.Spy.Result = refused;

        var response = Ok<SettingsResponse>(await rig.Controller.PutSettings(new PutSettingsRequest(Valid(), FakeServiceAccount.Json(), false), Ct));

        Assert.True(response.Key.HasKey);
        Assert.Single(rig.Spy.Starts);
    }

    [Fact]
    public async Task A_start_that_throws_does_not_fail_the_save_and_only_its_type_is_logged()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: false));
        rig.Spy.OnStart = new InvalidOperationException($"the store said {FakeServiceAccount.LeakMarker}");

        var response = Ok<SettingsResponse>(await rig.Controller.PutSettings(new PutSettingsRequest(Valid(), FakeServiceAccount.Json(), false), Ct));

        Assert.True(response.Key.HasKey);
        Assert.Contains(rig.ControllerLog.Entries, e => e.Text.Contains("InvalidOperationException"));
        Assert.DoesNotContain(FakeServiceAccount.LeakMarker, rig.AllLogText);
        Assert.DoesNotContain(rig.ControllerLog.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task A_save_does_not_wait_for_the_backfill_it_started()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: false, RealCoordinator: true));
        rig.Reports.ExpectedLatestPeriod = Period.Parse("2026-07");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstMonth = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Reports.OnRefresh = async (_, ct) =>
        {
            firstMonth.TrySetResult();
            await release.Task.WaitAsync(ct);
        };

        // This call returns while the first month is still being fetched: it would never return if the save waited for the run.
        var response = Ok<SettingsResponse>(await rig.Controller.PutSettings(new PutSettingsRequest(Valid(), FakeServiceAccount.Json(), false), Ct).WaitAsync(TimeSpan.FromSeconds(30)));
        await firstMonth.Task.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.True(response.Key.HasKey);
        Assert.True(rig.Real.Progress!.Running);

        release.SetResult();
        await rig.Real.CurrentRun!.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.False(rig.Real.Progress!.Running);
    }

    // ---- Test connection ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Test_connection_with_a_blank_key_tests_the_saved_one_and_answers_as_the_tester_does()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: true));

        var result = Ok<ConnectionTestResult>(await rig.Controller.TestConnection(new ConnectionTestRequest("123456789", "example.com", null), Ct));

        Assert.True(result.Key.Ok);
        Assert.True(result.Ga4.Ok);
        Assert.True(result.SearchConsole.Ok);
        Assert.Equal(FakeServiceAccount.DefaultClientEmail, result.ServiceAccountEmail);
        AssertNoStore(rig);
        Assert.Empty(rig.Spy.Starts); // testing starts nothing
    }

    [Fact]
    public async Task Test_connection_tells_each_source_apart_and_never_throws_a_google_problem()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: true));
        rig.Ga4Answer = _ => GoogleErrorBodies.DisabledAnswer();

        var result = Ok<ConnectionTestResult>(await rig.Controller.TestConnection(new ConnectionTestRequest("123456789", "example.com", null), Ct));

        Assert.Equal(ErrorCodes.ApiDisabled, result.Ga4.ErrorCode);
        Assert.True(result.SearchConsole.Ok);
    }

    [Fact]
    public async Task Test_connection_with_a_key_that_is_not_one_says_keyInvalid_and_does_not_repeat_it()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: false));

        var response = await rig.Controller.TestConnection(new ConnectionTestRequest("123456789", "example.com", $"not a key {FakeServiceAccount.LeakMarker}"), Ct);

        var result = Ok<ConnectionTestResult>(response);
        Assert.Equal(ErrorCodes.KeyInvalid, result.Key.ErrorCode);
        Assert.DoesNotContain(FakeServiceAccount.LeakMarker, ManagementJson.Serialize(result));
        Assert.DoesNotContain(FakeServiceAccount.LeakMarker, rig.AllLogText);
    }

    [Fact]
    public async Task Test_connection_with_no_body_is_a_400()
    {
        var rig = await ApiRig.CreateAsync();

        Assert.Equal(ApiCodes.RequestInvalid, Code(Problem(await rig.Controller.TestConnection(null, Ct), 400)));
        AssertNoStore(rig);
    }

    // ---- user groups ----------------------------------------------------------------------------------------------------

    private static IUserGroup Group(string alias, string? name)
    {
        var group = Substitute.For<IUserGroup>();
        group.Alias.Returns(alias);
        group.Name.Returns(name);
        return group;
    }

    [Fact]
    public async Task The_user_groups_are_listed_by_alias_and_name_in_name_order()
    {
        var rig = await ApiRig.CreateAsync();
        var all = new PagedModel<IUserGroup>(3, [Group("writers", "Writers"), Group("admin", "Administrators"), Group("seo", null)]);
        rig.UserGroups.GetAllAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(all);

        var groups = Ok<UserGroupItem[]>(await rig.Controller.GetUserGroups());

        Assert.Equal(
            [new UserGroupItem("admin", "Administrators"), new UserGroupItem("seo", "seo"), new UserGroupItem("writers", "Writers")],
            groups); // a group with no name is shown by its alias
        AssertNoStore(rig);
    }
}
