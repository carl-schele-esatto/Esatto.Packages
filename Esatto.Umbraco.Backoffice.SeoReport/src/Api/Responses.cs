using System.Text.Json.Serialization;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Jobs;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;

namespace Esatto.Umbraco.Backoffice.SeoReport.Api;

// The response and request models of the controller. Every one is a small record that the Management API writes in camelCase (see
// ResponseJsonTests, which pins the names the client reads). The one exception is the report itself, which keeps its snake_case names.
// None carries a sentence: codes, numbers, months as YYYY-MM and times in UTC.

/// <summary>
/// What the dashboard needs to open: whether the package is set up and what is missing, how the backfill is going, whether the archive
/// is current, and which months can be shown.
/// </summary>
/// <param name="Setup">Whether the package can fetch. Written as the word <c>notConfigured</c>, <c>keyUnreadable</c> or <c>ready</c>.</param>
/// <param name="Missing">What is still needed to be ready: any of <c>key</c>, <c>ga4PropertyId</c>, <c>searchConsoleSite</c>.</param>
/// <param name="Fetch">The backfill: the latest run of this process, and what is on record about the last failure and success.</param>
/// <param name="UpToDate">Whether the latest month that should be archived by now is.</param>
/// <param name="Periods">The archived months, <c>YYYY-MM</c>, oldest first.</param>
/// <param name="ExpectedLatestPeriod">The latest month that should be archived by now.</param>
/// <param name="DisplayName">The name to show on the report instead of the site's address; null when none is set.</param>
public sealed record StatusResponse(
    [property: JsonConverter(typeof(CamelCaseEnumConverter<SetupState>))] SetupState Setup,
    string[] Missing,
    FetchStatusResponse Fetch,
    bool UpToDate,
    string[] Periods,
    string ExpectedLatestPeriod,
    string? DisplayName);

/// <summary>
/// How the backfill is going: the progress of the latest run of this process (<see cref="FetchProgress"/>; not running, nothing done
/// when this process has not run one) and, from the <c>fetchState</c> document, the code and times of the last failure and success,
/// which survive a restart. Codes and times only: no text, no name of the person who pressed Fetch now.
/// </summary>
/// <param name="Running">True from the start of the run to its end. With <paramref name="Total"/> 0 it is "starting": the months are counted a moment later.</param>
/// <param name="Done">Months finished.</param>
/// <param name="Total">The months that needed a fetch when the run started; 0 with nothing running means there was nothing to fetch.</param>
/// <param name="CurrentPeriod">The month being fetched, or null.</param>
/// <param name="ErrorCode">Why the latest run ended badly, or null.</param>
/// <param name="LastErrorCode">
/// The code of the last recorded failure while nothing has worked since (<see cref="LastFailureUtc"/> later than <see cref="LastSuccessUtc"/>),
/// otherwise null: a failure that was fixed is not the reason anything stopped.
/// </param>
/// <param name="LastSuccessUtc">When the last run that archived a month ended.</param>
/// <param name="LastFailureUtc">When the last run that failed ended.</param>
public sealed record FetchStatusResponse(
    bool Running,
    int Done,
    int Total,
    string? CurrentPeriod,
    string? ErrorCode,
    string? LastErrorCode,
    DateTimeOffset? LastSuccessUtc,
    DateTimeOffset? LastFailureUtc)
{
    // A code is one of the fixed words. A document that someone edited by hand must not be a way to put text on the screen.
    internal static FetchStatusResponse From(FetchProgress? progress, FetchState state) => new(
        progress?.Running ?? false,
        progress?.Done ?? 0,
        progress?.Total ?? 0,
        progress?.CurrentPeriod,
        Known(progress?.ErrorCode),
        FailedLast(state) ? Known(state.LastErrorCode) : null,
        state.LastSuccessUtc,
        state.LastFailureUtc);

    // The recorded failure is the latest news only while nothing has worked since; at the same moment, the success wins.
    private static bool FailedLast(FetchState state)
        => state.LastFailureUtc is { } failed && (state.LastSuccessUtc is not { } worked || failed > worked);

    private static string? Known(string? code) => code is null || ErrorCodes.IsKnown(code) ? code : ErrorCodes.Unknown;
}

/// <summary>A report with what belongs beside it: the month's note, when it was fetched, and which month it is.</summary>
/// <param name="Report">The report as this user may see it (findings left out for a user outside the findings groups), or null when there is none.</param>
/// <param name="Note">The month's note, or null.</param>
/// <param name="FetchedUtc">When the report was fetched, or null when there is none.</param>
/// <param name="Period">The month, <c>YYYY-MM</c>: the one asked for, or the newest when none was asked for. Null when nothing is archived.</param>
public sealed record ReportResponse(Report? Report, string? Note, DateTimeOffset? FetchedUtc, string? Period);

/// <summary>The answer to Fetch now once a backfill is running.</summary>
/// <param name="Result">Always <c>started</c>: a refusal is a problem.</param>
/// <param name="Progress">The progress as it is when the run starts (see <see cref="FetchStatusResponse"/>).</param>
public sealed record StartFetchResponse(string Result, FetchStatusResponse Progress);

/// <summary>A month's note, as the editor wrote it.</summary>
public sealed record NoteRequest(string? Text);

/// <summary>The note as saved: trimmed, cut to the longest allowed, or null when it was blank.</summary>
public sealed record NoteResponse(string? Note);

/// <summary>The editor's target queries, as typed.</summary>
public sealed record TargetsRequest(string[]? Terms);

/// <summary>The target queries as saved: trimmed, lower case, without blanks and duplicates.</summary>
public sealed record TargetsResponse(string[] Terms);

/// <summary>A backoffice user group, for the findings picker: the alias is what is stored, the name is what is shown.</summary>
public sealed record UserGroupItem(string Alias, string Name);
