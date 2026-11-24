using System.Text.Json;

namespace Esatto.Umbraco.Backoffice.SeoReport.Jobs;

/// <summary>
/// What the backfills did, kept in the <c>fetchState</c> document so that a restart does not lose it and the scheduled job on
/// another instance can see it. Times (UTC) and one code only: never the key, never the text of an exception, and never the name of
/// the person who pressed Fetch now (<see cref="StartedBy"/> says only <see cref="Scheduled"/> or <see cref="Manual"/>).
///
/// The JSON is camelCase: <c>lastAttemptUtc</c>, <c>lastSuccessUtc</c>, <c>lastFailureUtc</c>, <c>lastErrorCode</c>, <c>startedBy</c>.
/// <see cref="LastErrorCode"/> belongs to <see cref="LastFailureUtc"/> and is kept when a later run succeeds, as the last failure's.
/// </summary>
/// <param name="LastAttemptUtc">When the latest run that tried a month ended.</param>
/// <param name="LastSuccessUtc">When the latest run that archived at least one month ended.</param>
/// <param name="LastFailureUtc">When the latest run that failed (a month failed or the run had to stop) ended.</param>
/// <param name="LastErrorCode">The code of that failure, one of <c>ErrorCodes.All</c>.</param>
/// <param name="StartedBy"><see cref="Scheduled"/> or <see cref="Manual"/>: how the latest recorded run was started.</param>
internal sealed record FetchState(
    DateTimeOffset? LastAttemptUtc,
    DateTimeOffset? LastSuccessUtc,
    DateTimeOffset? LastFailureUtc,
    string? LastErrorCode,
    string? StartedBy)
{
    /// <summary>The start made by the scheduled job.</summary>
    public const string Scheduled = "scheduled";

    /// <summary>A start made by a person (Fetch now, or saving settings that work).</summary>
    public const string Manual = "manual";

    /// <summary>
    /// How long the scheduled job leaves a failure alone before it tries again: a failing month must not cost 46 Google calls every
    /// wake-up. A person pressing Fetch now is not held back by it.
    /// </summary>
    public static readonly TimeSpan FailureBackOff = TimeSpan.FromHours(1);

    /// <summary>Nothing recorded yet.</summary>
    public static FetchState Empty { get; } = new(null, null, null, null, null);

    /// <summary>The state in the document, or <see cref="Empty"/> when there is none or it cannot be read: a damaged row must never stop the job.</summary>
    public static FetchState Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Empty;

        try
        {
            return JsonSerializer.Deserialize<FetchState>(json, JsonSerializerOptions.Web) ?? Empty;
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonSerializerOptions.Web);

    /// <summary>
    /// Whether the scheduled job must hold back at <paramref name="now"/>: the latest failure is less than <see cref="FailureBackOff"/>
    /// old (exactly that old is allowed to run) and no success came after it (a run that failed one month and archived another
    /// records both at its end, which is not "after", so it still holds back). A failure stamped in the future (a clock that was
    /// set back, another server's clock) does not hold anything back: waiting for it could last for ever.
    /// </summary>
    public bool IsBackingOff(DateTimeOffset now)
    {
        if (LastFailureUtc is not { } failed) return false;
        if (LastSuccessUtc is { } succeeded && succeeded > failed) return false;

        var since = now - failed;
        return since >= TimeSpan.Zero && since < FailureBackOff;
    }
}
