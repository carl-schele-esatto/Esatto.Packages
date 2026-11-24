namespace Esatto.Umbraco.Backoffice.SeoReport.Api;

/// <summary>
/// The <c>code</c> of a problem that is the API's own and not a Google or key problem (those are <see cref="Google.ErrorCodes"/>).
/// Fixed words, like the others: the server sends a code and the client has the words.
/// </summary>
public static class ApiCodes
{
    /// <summary>A period that is not exactly <c>YYYY-MM</c> (or, where one is required, none).</summary>
    public const string PeriodInvalid = "periodInvalid";

    /// <summary>A valid month that cannot be fetched now: later than the latest finished month, or earlier than Search Console keeps.</summary>
    public const string PeriodOutOfRange = "periodOutOfRange";

    /// <summary>A day that is not exactly <c>YYYY-MM-DD</c> (or none), for the sources of a day.</summary>
    public const string DateInvalid = "dateInvalid";

    /// <summary>
    /// A valid day or month that the daily chart cannot show: a day earlier than the first day of the oldest month its picker offers
    /// or later than today, or a month the picker does not offer. Nothing is asked of Google for it.
    /// </summary>
    public const string DateOutOfRange = "dateOutOfRange";

    /// <summary>A backfill is running already, here or on another instance.</summary>
    public const string AlreadyRunning = "alreadyRunning";

    /// <summary>The request has no body, or not the part of it the endpoint needs.</summary>
    public const string RequestInvalid = "requestInvalid";

    /// <summary>
    /// Every code, in the order the texts are written. Each one has a title and a text in <c>Client/src/lang/en.ts</c>, registered in
    /// <see cref="Reporting.ReportKeys"/> as area <c>error</c> under the code in snake_case (see <see cref="Google.ErrorCodes.BaseId"/>),
    /// so the client builds the key from the code alone, as it does for <see cref="Google.ErrorCodes"/>. The client's own copy of both
    /// lists is <c>Client/src/error-codes.ts</c>; <c>ClientErrorCodesTests</c> fails when it differs.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [PeriodInvalid, PeriodOutOfRange, DateInvalid, DateOutOfRange, AlreadyRunning, RequestInvalid];
}
