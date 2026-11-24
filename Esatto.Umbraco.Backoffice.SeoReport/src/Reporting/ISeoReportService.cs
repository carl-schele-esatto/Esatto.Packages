namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

/// <summary>
/// The report archive: ONE REPORT PER MONTH, kept for good, in the database (so instances share it).
///
/// A full fetch is 46 calls to Google (GA4: three segments over two years; Search Console: one probe, three segments for each of
/// thirteen months and three lists) and takes over ten seconds, so it NEVER happens because somebody opens the dashboard: reading
/// is cheap and never fetches. Each month lives in a row of its own and is never overwritten by another month.
///
/// A report written under an older <see cref="SeoReportService.ContractVersion"/> than the running code's counts as MISSING in
/// every member below, the same as a row that cannot be read: it is not listed, not returned, and needs a fetch.
///
/// SO DOES A REPORT BUILT FOR OTHER SETTINGS. A report is only valid for the GA4 property and the Search Console site it was
/// built from (it records both). When either differs from the settings in force now, the report counts as missing in every
/// member below, and it is valid again if the settings go back; a refresh then replaces it. Every read compares with the settings
/// in force at that moment, so a change takes effect at once. When the package is not set up (no usable key, or a property
/// missing) there is nothing to compare with and a read returns what is archived. The brand terms are not part of the comparison.
/// </summary>
public interface ISeoReportService
{
    /// <summary>
    /// The archived months (<c>YYYY-MM</c>), OLDEST FIRST: the dashboard draws them left to right, so the newest is the last
    /// element. Empty when nothing has been fetched. Cached for five minutes, since another instance can archive a month.
    /// </summary>
    Task<IReadOnlyList<string>> GetAvailablePeriodsAsync(CancellationToken ct = default);

    /// <summary>
    /// The latest month that SHOULD be archived now: today minus five days, rounded to the latest month that is entirely behind
    /// that date. "Five days after the month ends", whatever its length: on 4 September that still reaches only 30 August, so the
    /// answer is July, and on the 5th it reaches 31 August and the answer is August. The margin is deliberately wider than Search
    /// Console's own lag: a month fetched early is archived with its last days missing and is never fetched again, which is worse
    /// than waiting two more days. A pure calculation on the injected clock; it asks nobody.
    /// </summary>
    Period ExpectedLatestPeriod { get; }

    /// <summary>
    /// True when <see cref="ExpectedLatestPeriod"/> does not <see cref="NeedsFetchAsync"/>. That, and not the report's age, is the
    /// right measure of "up to date": a finished month never changes, so a fully current report is normally weeks old.
    /// </summary>
    Task<bool> IsUpToDateAsync(CancellationToken ct = default);

    /// <summary>
    /// Whether the month has to be fetched (again): it is not archived, or its report is from an older contract version or cannot
    /// be read, or it is an archived "no data" report (a connected site that answered nothing) fetched more than 24 hours ago, so
    /// that a new site is checked again once a day until data arrives. Reads through the report cache, so it can be up to an hour
    /// behind another instance; the worst that does is a redundant fetch.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="period"/> is <c>default(Period)</c>, which is not a month.</exception>
    Task<bool> NeedsFetchAsync(Period period, CancellationToken ct = default);

    /// <summary>
    /// The report for a month. With a null or blank <paramref name="period"/> the newest archived one. A period that is not
    /// <c>YYYY-MM</c> exactly (nothing is trimmed) is null without touching the store: the value comes from a query string and
    /// never becomes a key or a query. Null as well when the month is not archived, and when the archive is empty.
    /// </summary>
    /// <remarks>The report is shared by every reader (it is cached): treat it as read-only.</remarks>
    Task<Report?> GetReportAsync(string? period = null, CancellationToken ct = default);

    /// <summary>When the month's report was fetched, UTC. The same lookup rules as <see cref="GetReportAsync"/>.</summary>
    Task<DateTimeOffset?> GetFetchedUtcAsync(string? period = null, CancellationToken ct = default);

    /// <summary>
    /// Fetches the month from GA4 and Search Console, builds the report and archives it, replacing that month's earlier report.
    /// One fetch runs at a time in this process, and a refresh that waited while another refresh of the SAME month finished
    /// returns that report instead of fetching again, provided both were asked with the same settings (properties and brand terms).
    /// </summary>
    /// <exception cref="ReportFetchException">
    /// There is no usable setup: <c>notConfigured</c> (no key, or a property missing), <c>keyUnreadable</c>, or <c>keyInvalid</c>
    /// from the Google clients. Nothing was asked of Google.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="period"/> is <c>default(Period)</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The month is later than <see cref="ExpectedLatestPeriod"/> (not complete yet, so it would be archived cut off and never
    /// fetched again) or earlier than <c>GscAgent.EarliestAvailableMonth</c> for today (Search Console no longer has it). Both
    /// ends are inclusive and follow the injected clock. Checked first: nothing is asked of Google or written. The message is fixed.
    /// </exception>
    /// <remarks>Errors from Google itself (<c>GoogleApiException</c>) propagate unchanged; the caller maps them.</remarks>
    Task<Report> RefreshAsync(Period period, CancellationToken ct = default);
}
