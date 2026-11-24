using System.Net;
using Esatto.Umbraco.Backoffice.SeoReport.Api;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Jobs;
using Esatto.Umbraco.Backoffice.SeoReport.Persistence;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Xunit;
using static Esatto.Umbraco.Backoffice.SeoReport.Tests.Support.ApiResults;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// What the report endpoints answer: status, the report (and who may see its findings), refresh, notes and targets. The controller
/// is built over the real services and in-memory fakes (see <see cref="ApiRig"/>) and called directly, which tests what it does and
/// not what the framework adds around it; the attributes the framework reads are in <see cref="ControllerRoutingTests"/> and the JSON
/// it writes in <see cref="ResponseJsonTests"/>.
/// </summary>
public partial class ControllerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    // ---- status ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Status_says_what_is_set_up_what_is_archived_and_how_the_last_fetch_went()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: true));
        rig.Reports.Archive("2026-05", "2026-06");
        rig.Spy.Progress = new FetchProgress(Running: true, Done: 2, Total: 13, CurrentPeriod: "2026-04", ErrorCode: null);
        var failed = new DateTimeOffset(2026, 8, 19, 5, 0, 0, TimeSpan.Zero);
        var succeeded = new DateTimeOffset(2026, 8, 18, 5, 0, 0, TimeSpan.Zero);
        await rig.Harness.Store.SaveDocumentAsync(DocumentKeys.FetchState, new FetchState(failed, succeeded, failed, ErrorCodes.NoAccess, FetchState.Scheduled).ToJson());

        var status = Ok<StatusResponse>(await rig.Controller.GetStatus(Ct));

        Assert.Equal(SetupState.Ready, status.Setup);
        Assert.Empty(status.Missing);
        Assert.Equal(["2026-05", "2026-06"], status.Periods); // oldest first
        Assert.Equal("2026-07", status.ExpectedLatestPeriod);
        Assert.False(status.UpToDate); // the expected month is not archived
        Assert.Equal(new FetchStatusResponse(true, 2, 13, "2026-04", null, ErrorCodes.NoAccess, succeeded, failed), status.Fetch);
    }

    [Fact]
    public async Task Status_of_a_package_that_is_not_set_up_names_what_is_missing()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: false));

        var status = Ok<StatusResponse>(await rig.Controller.GetStatus(Ct));

        Assert.Equal(SetupState.NotConfigured, status.Setup);
        Assert.Equal(["key", "ga4PropertyId", "searchConsoleSite"], status.Missing);
        Assert.Empty(status.Periods);
        Assert.Null(status.DisplayName);
    }

    [Fact]
    public async Task Status_says_keyUnreadable_for_a_saved_key_this_environment_cannot_read()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: false));
        await rig.Harness.StoreRawProtectedKeyAsync(FakeServiceAccount.Json(), new SecretProtector(new EphemeralDataProtectionProvider()));
        rig.Harness.Time.Advance(TimeSpan.FromMinutes(2)); // the settings are cached for a minute

        var status = Ok<StatusResponse>(await rig.Controller.GetStatus(Ct));

        Assert.Equal(SetupState.KeyUnreadable, status.Setup);
        Assert.Contains("key", status.Missing); // an unreadable key counts as missing
    }

    [Fact]
    public async Task Status_carries_the_display_name_from_the_settings()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Ready: false));
        rig.Configure(nameof(SeoReportSettings.DisplayName), "Example Site");

        var status = Ok<StatusResponse>(await rig.Controller.GetStatus(Ct));

        Assert.Equal("Example Site", status.DisplayName);
    }

    [Fact]
    public async Task Status_with_nothing_fetched_yet_says_so_with_defaults_and_no_recorded_failure()
    {
        var rig = await ApiRig.CreateAsync();

        var fetch = Ok<StatusResponse>(await rig.Controller.GetStatus(Ct)).Fetch;

        Assert.Equal(new FetchStatusResponse(false, 0, 0, null, null, null, null, null), fetch);
    }

    [Fact]
    public async Task Status_gives_only_a_known_code_for_the_last_failure_whatever_the_document_says()
    {
        var rig = await ApiRig.CreateAsync();
        var failed = new DateTimeOffset(2026, 8, 19, 5, 0, 0, TimeSpan.Zero);
        await rig.Harness.Store.SaveDocumentAsync(
            DocumentKeys.FetchState, new FetchState(failed, null, failed, "words someone wrote straight into the table", FetchState.Manual).ToJson());

        var fetch = Ok<StatusResponse>(await rig.Controller.GetStatus(Ct)).Fetch;

        Assert.Equal(ErrorCodes.Unknown, fetch.LastErrorCode);
    }

    /// <summary>
    /// The last failure is only news while nothing has worked since: a fetch that failed and was fixed must not be shown as the reason the
    /// last fetch did not finish. The times stay as they are, for whoever wants them.
    /// </summary>
    [Theory]
    [InlineData("failure, then a later success", -2, -1, null)]
    [InlineData("success, then a later failure", -1, -2, "noAccess")]
    [InlineData("a failure and never a success", -1, null, "noAccess")]
    [InlineData("a success and never a failure", null, -1, null)]
    [InlineData("a failure and a success at the same moment", -1, -1, null)]
    public async Task Status_gives_the_last_failure_only_when_nothing_has_worked_since(string label, int? failedHoursAgo, int? workedHoursAgo, string? expected)
    {
        Assert.NotNull(label);
        var rig = await ApiRig.CreateAsync();
        var now = rig.Harness.Time.GetUtcNow();
        DateTimeOffset? failed = failedHoursAgo is { } f ? now.AddHours(f) : null;
        DateTimeOffset? worked = workedHoursAgo is { } w ? now.AddHours(w) : null;
        await rig.Harness.Store.SaveDocumentAsync(
            DocumentKeys.FetchState, new FetchState(now.AddHours(-1), worked, failed, ErrorCodes.NoAccess, FetchState.Manual).ToJson());

        var fetch = Ok<StatusResponse>(await rig.Controller.GetStatus(Ct)).Fetch;

        Assert.Equal(expected, fetch.LastErrorCode);
        Assert.Equal(worked, fetch.LastSuccessUtc);
        Assert.Equal(failed, fetch.LastFailureUtc);
    }

    [Fact]
    public async Task Opening_the_dashboard_never_fetches()
    {
        var rig = await ApiRig.CreateAsync();
        rig.Reports.ArchiveReport(ApiRig.ReportWithFindings());

        // Everything the dashboard asks for when it opens, and again when it polls.
        await rig.Controller.GetStatus(Ct);
        await rig.Controller.GetReport(null, Ct);
        await rig.Controller.GetReport("2026-07", Ct);
        await rig.Controller.GetReport("2026-03", Ct); // a month with no report is an empty answer, not a reason to fetch
        await rig.Controller.GetFetchProgress();
        await rig.Controller.GetTargets();
        await rig.Controller.GetSettings();

        Assert.Empty(rig.Reports.Refreshed);
        Assert.Empty(rig.Spy.Starts);
    }

    // ---- report ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_report_comes_with_its_note_when_it_was_fetched_and_which_month_it_is()
    {
        var rig = await ApiRig.CreateAsync();
        var fetched = new DateTimeOffset(2026, 8, 3, 6, 30, 0, TimeSpan.Zero);
        rig.Reports.ArchiveReport(ApiRig.ReportWithFindings("2026-06"), fetched);
        rig.Reports.ArchiveReport(ApiRig.ReportWithFindings("2026-07"), fetched.AddDays(1));
        await rig.Notes.SaveAsync(Period.Parse("2026-06"), "The cookie banner went in on the 12th.");

        var june = Ok<ReportResponse>(await rig.Controller.GetReport("2026-06", Ct));
        var newest = Ok<ReportResponse>(await rig.Controller.GetReport(null, Ct));

        Assert.Equal("2026-06", june.Period);
        Assert.Equal("2026-06", june.Report!.ReportPeriod);
        Assert.Equal("The cookie banner went in on the 12th.", june.Note);
        Assert.Equal(fetched, june.FetchedUtc);

        Assert.Equal("2026-07", newest.Period); // blank is the newest, and the answer says which month that was
        Assert.Null(newest.Note);
        Assert.Equal(fetched.AddDays(1), newest.FetchedUtc);
    }

    [Fact]
    public async Task A_report_that_does_not_exist_is_an_empty_answer_and_not_an_error()
    {
        var rig = await ApiRig.CreateAsync();

        var none = Ok<ReportResponse>(await rig.Controller.GetReport(null, Ct));
        var missing = Ok<ReportResponse>(await rig.Controller.GetReport("2026-03", Ct));

        Assert.Equal(new ReportResponse(null, null, null, null), none);
        Assert.Equal(new ReportResponse(null, null, null, "2026-03"), missing); // the month asked for, so a note for it can still be written
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026-00")]
    [InlineData("2026-7")]
    [InlineData("26-07")]
    [InlineData(" 2026-07")]
    [InlineData("2026-07 ")]
    [InlineData("../2026-07")]
    [InlineData("2026-07-01")]
    [InlineData("juli")]
    public async Task A_period_that_is_not_exactly_YYYY_MM_is_refused_before_anything_is_read(string period)
    {
        var rig = await ApiRig.CreateAsync();
        rig.Reports.ArchiveReport(ApiRig.ReportWithFindings());

        var problem = Problem(await rig.Controller.GetReport(period, Ct), 400);

        Assert.Equal(ApiCodes.PeriodInvalid, Code(problem));
        Assert.Equal(0, rig.Reports.ReportReads);
    }

    // ---- who sees the findings ------------------------------------------------------------------------------------------

    private static int Warnings(Report? report) => report!.Traffic.Search!.Annotations.Count(a => a.Severity == "warning");

    private static int Infos(Report? report) => report!.Traffic.Search!.Annotations.Count(a => a.Severity == "info");

    [Fact]
    public async Task A_user_outside_the_findings_groups_gets_the_report_without_its_warnings()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Groups: ["editors"], FindingsGroups: ["seo"]));
        var archived = ApiRig.ReportWithFindings();
        rig.Reports.ArchiveReport(archived);

        var response = Ok<ReportResponse>(await rig.Controller.GetReport("2026-07", Ct));

        Assert.Equal(0, Warnings(response.Report));
        Assert.Equal(1, Infos(response.Report)); // information about the data is never hidden
        Assert.Equal(2, Warnings(archived));      // the archive keeps the whole report, and nobody changed the shared instance
    }

    [Fact]
    public async Task A_user_in_one_of_the_findings_groups_sees_the_warnings_whatever_the_case_of_the_alias()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Groups: ["SEO", "editors"], FindingsGroups: ["seo"]));
        rig.Reports.ArchiveReport(ApiRig.ReportWithFindings());

        var response = Ok<ReportResponse>(await rig.Controller.GetReport("2026-07", Ct));

        Assert.Equal(2, Warnings(response.Report));
    }

    private static string[] Split(string commaSeparated) => commaSeparated.Split(',', StringSplitOptions.RemoveEmptyEntries);

    [Theory]
    [InlineData("editors")]
    [InlineData("admin,writers")]
    [InlineData("")] // a user who is in no group
    public async Task With_no_findings_groups_every_signed_in_user_sees_the_warnings_a_user_with_no_groups_too(string groups)
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Groups: Split(groups), FindingsGroups: []));
        rig.Reports.ArchiveReport(ApiRig.ReportWithFindings());

        var response = Ok<ReportResponse>(await rig.Controller.GetReport("2026-07", Ct));

        Assert.Equal(2, Warnings(response.Report));
    }

    [Theory]
    [InlineData("seo")]
    [InlineData("")] // no restriction: everyone WITH ACCESS, and nobody signed in is not one of them
    public async Task Nobody_signed_in_never_sees_the_warnings_whatever_the_list_says(string findingsGroups)
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(SignedIn: false, FindingsGroups: Split(findingsGroups)));
        rig.Reports.ArchiveReport(ApiRig.ReportWithFindings());

        var response = Ok<ReportResponse>(await rig.Controller.GetReport("2026-07", Ct));

        Assert.Equal(0, Warnings(response.Report));
        Assert.Equal(1, Infos(response.Report));
    }

    [Fact]
    public async Task A_refresh_answer_is_filtered_the_same_way()
    {
        var rig = await ApiRig.CreateAsync(new ApiRig.Options(Groups: ["editors"], FindingsGroups: ["seo"]));
        rig.Reports.ArchiveReport(ApiRig.ReportWithFindings());

        var response = Ok<ReportResponse>(await rig.Controller.Refresh("2026-07", Ct));

        Assert.Equal(0, Warnings(response.Report));
        Assert.Equal(["2026-07"], rig.Reports.Refreshed.Select(p => p.ToString())); // it did fetch: this is the one endpoint that does
    }

    // ---- refresh --------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Refresh_fetches_the_month_and_answers_like_the_report_does()
    {
        var rig = await ApiRig.CreateAsync();
        var fetched = new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);
        rig.Reports.ArchiveReport(ApiRig.ReportWithFindings("2026-07"), fetched);
        await rig.Notes.SaveAsync(Period.Parse("2026-07"), "Relaunch.");

        var response = Ok<ReportResponse>(await rig.Controller.Refresh("2026-07", Ct));

        Assert.Equal("2026-07", response.Period);
        Assert.Equal("2026-07", response.Report!.ReportPeriod);
        Assert.Equal("Relaunch.", response.Note);
        Assert.Equal(fetched, response.FetchedUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("2026-13")]
    [InlineData("2026-7")]
    [InlineData("../x")]
    public async Task Refresh_needs_a_valid_month_and_never_picks_one_itself(string? period)
    {
        var rig = await ApiRig.CreateAsync();

        var problem = Problem(await rig.Controller.Refresh(period, Ct), 400);

        Assert.Equal(ApiCodes.PeriodInvalid, Code(problem));
        Assert.Empty(rig.Reports.Refreshed);
    }

    [Fact]
    public async Task A_month_the_service_refuses_as_outside_the_archive_range_is_a_400_periodOutOfRange()
    {
        var rig = await ApiRig.CreateAsync();
        rig.Reports.OnRefresh = (_, _) => throw new ArgumentOutOfRangeException("period", $"outside {FakeServiceAccount.LeakMarker}");

        var problem = Problem(await rig.Controller.Refresh("2020-01", Ct), 400);

        Assert.Equal(ApiCodes.PeriodOutOfRange, Code(problem));
        Assert.DoesNotContain(FakeServiceAccount.LeakMarker, ManagementJson.Serialize(problem));
    }

    [Fact]
    public async Task An_out_of_range_error_from_somewhere_else_in_a_fetch_is_not_mistaken_for_a_bad_month()
    {
        var rig = await ApiRig.CreateAsync();
        rig.Reports.OnRefresh = (_, _) => throw new ArgumentOutOfRangeException("index", "deep in the mapper");

        var problem = Problem(await rig.Controller.Refresh("2026-07", Ct), 502);

        Assert.Equal(ErrorCodes.Unknown, Code(problem));
    }

    [Theory]
    [InlineData("notConfigured", 409, ErrorCodes.NotConfigured)]
    [InlineData("keyUnreadable", 409, ErrorCodes.KeyUnreadable)]
    [InlineData("keyInvalid", 409, ErrorCodes.KeyInvalid)]
    [InlineData("apiDisabled", 502, ErrorCodes.ApiDisabled)]
    [InlineData("noAccess", 502, ErrorCodes.NoAccess)]
    [InlineData("propertyNotFound", 502, ErrorCodes.PropertyNotFound)]
    [InlineData("quota", 502, ErrorCodes.Quota)]
    [InlineData("serverError", 502, ErrorCodes.Unknown)]
    [InlineData("surprise", 502, ErrorCodes.Unknown)]
    [InlineData("timeout", 502, ErrorCodes.Unknown)]
    [InlineData("unlistedCode", 502, ErrorCodes.Unknown)]
    public async Task A_failed_refresh_is_a_problem_with_a_code_the_status_the_trace_id_and_none_of_the_exception_text(
        string kind, int status, string code)
    {
        var rig = await ApiRig.CreateAsync();
        var exception = await GoogleFailures.FailureOf(kind);
        rig.Reports.OnRefresh = (_, _) => throw exception;

        var problem = Problem(await rig.Controller.Refresh("2026-07", Ct), status);

        Assert.Equal(code, Code(problem));
        Assert.Equal(rig.Http.TraceIdentifier, problem.Extensions["traceId"]);
        Assert.NotNull(problem.Title);
        Assert.DoesNotContain(FakeServiceAccount.LeakMarker, ManagementJson.Serialize(problem)); // nothing the exception said travels on
        Assert.DoesNotContain(FakeServiceAccount.LeakMarker, rig.AllLogText);
    }

    [Fact]
    public async Task A_disabled_API_carries_its_fixed_argument_into_the_problem()
    {
        var rig = await ApiRig.CreateAsync();
        var failure = await GoogleFailures.Ga4Async(HttpStatusCode.Forbidden, GoogleErrorBodies.Disabled());
        rig.Reports.OnRefresh = (_, _) => throw failure;

        var problem = Problem(await rig.Controller.Refresh("2026-07", Ct), 502);

        Assert.Equal(new Dictionary<string, object?> { ["api"] = "analyticsData" }, Args(problem));
    }

    [Fact]
    public async Task A_failure_no_code_names_is_logged_with_its_type_the_http_status_and_the_trace_id_so_an_admin_can_find_it()
    {
        var rig = await ApiRig.CreateAsync();
        var failure = await GoogleFailures.Ga4Async(HttpStatusCode.InternalServerError, GoogleErrorBodies.Error(500, "INTERNAL"));
        rig.Reports.OnRefresh = (_, _) => throw failure;

        Problem(await rig.Controller.Refresh("2026-07", Ct), 502);

        var entry = Assert.Single(rig.ControllerLog.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("GoogleApiException", entry.Text);
        Assert.Contains("500", entry.Text);
        Assert.Contains(rig.Http.TraceIdentifier, entry.Text);
        Assert.DoesNotContain(FakeServiceAccount.LeakMarker, entry.Text);
    }

    [Fact]
    public async Task A_failure_a_code_names_is_not_a_warning()
    {
        var rig = await ApiRig.CreateAsync();
        rig.Reports.OnRefresh = (_, _) => throw new ReportFetchException(ErrorCodes.NotConfigured);

        Problem(await rig.Controller.Refresh("2026-07", Ct), 409);

        Assert.DoesNotContain(rig.ControllerLog.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task A_caller_that_went_away_is_not_a_failure_to_report()
    {
        var rig = await ApiRig.CreateAsync();
        using var cancelled = new CancellationTokenSource();
        rig.Reports.OnRefresh = (_, ct) =>
        {
            cancelled.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rig.Controller.Refresh("2026-07", cancelled.Token));

        Assert.Empty(rig.ControllerLog.Entries);
    }

    // ---- notes ----------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_note_is_saved_trimmed_and_comes_back_with_the_report()
    {
        var rig = await ApiRig.CreateAsync();
        rig.Reports.ArchiveReport(ApiRig.ReportWithFindings("2026-07"));

        var saved = Ok<NoteResponse>(await rig.Controller.PutNote("2026-07", new NoteRequest("   A core update rolled out.  ")));
        var report = Ok<ReportResponse>(await rig.Controller.GetReport("2026-07", Ct));

        Assert.Equal("A core update rolled out.", saved.Note);
        Assert.Equal("A core update rolled out.", report.Note);
    }

    [Fact]
    public async Task A_blank_note_deletes_it()
    {
        var rig = await ApiRig.CreateAsync();
        await rig.Notes.SaveAsync(Period.Parse("2026-07"), "old");

        var saved = Ok<NoteResponse>(await rig.Controller.PutNote("2026-07", new NoteRequest("   ")));

        Assert.Null(saved.Note);
        Assert.Null(await rig.Notes.GetAsync(Period.Parse("2026-07")));
    }

    [Fact]
    public async Task A_note_that_is_too_long_is_cut_by_the_service_and_the_answer_says_what_was_kept()
    {
        var rig = await ApiRig.CreateAsync();

        var saved = Ok<NoteResponse>(await rig.Controller.PutNote("2026-07", new NoteRequest(new string('x', INoteService.MaxLength + 500))));

        Assert.Equal(INoteService.MaxLength, saved.Note!.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026-13")]
    [InlineData("2026-7")]
    public async Task A_note_needs_a_valid_month(string? period)
    {
        var rig = await ApiRig.CreateAsync();

        var problem = Problem(await rig.Controller.PutNote(period, new NoteRequest("text")), 400);

        Assert.Equal(ApiCodes.PeriodInvalid, Code(problem));
    }

    [Fact]
    public async Task A_note_with_no_body_is_a_400()
    {
        var rig = await ApiRig.CreateAsync();

        var problem = Problem(await rig.Controller.PutNote("2026-07", null), 400);

        Assert.Equal(ApiCodes.RequestInvalid, Code(problem));
    }

    // ---- targets --------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Targets_are_saved_normalized_and_read_back()
    {
        var rig = await ApiRig.CreateAsync();

        var saved = Ok<TargetsResponse>(await rig.Controller.PutTargets(new TargetsRequest(["  Red   Shoes ", "red shoes", "", "Blue Hat"])));
        var read = Ok<TargetsResponse>(await rig.Controller.GetTargets());

        Assert.Equal(["red shoes", "blue hat"], saved.Terms);
        Assert.Equal(saved.Terms, read.Terms);
    }

    [Fact]
    public async Task Targets_with_no_terms_clear_the_list_and_with_no_body_are_a_400()
    {
        var rig = await ApiRig.CreateAsync();
        await rig.Controller.PutTargets(new TargetsRequest(["keep"]));

        var cleared = Ok<TargetsResponse>(await rig.Controller.PutTargets(new TargetsRequest(null)));
        var refused = Problem(await rig.Controller.PutTargets(null), 400);

        Assert.Empty(cleared.Terms);
        Assert.Equal(ApiCodes.RequestInvalid, Code(refused));
    }
}
