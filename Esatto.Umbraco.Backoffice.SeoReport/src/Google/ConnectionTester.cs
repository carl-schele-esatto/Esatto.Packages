using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Google;
using Google.Apis.AnalyticsData.v1beta.Data;
using Microsoft.Extensions.Logging;

namespace Esatto.Umbraco.Backoffice.SeoReport.Google;

/// <summary>
/// What Test connection is asked to check: the two properties as they stand in the form, and a key. The key is a SECRET and this is
/// a record, so two things keep it from leaving by accident: <see cref="ToString"/> is written by hand and prints neither the key
/// nor the identifiers, and the key's JSON converter reads the key from a request body but writes <c>null</c>, so serializing a
/// request (it is a controller body) cannot echo the key into a response. Only the <see cref="ConnectionTestResult"/> is returned.
/// </summary>
/// <param name="Ga4PropertyId">The GA4 property as typed (<c>123456789</c> or <c>properties/123456789</c>).</param>
/// <param name="SearchConsoleSite">The Search Console property as typed (see <see cref="SettingsNormalizer.NormalizeSearchConsole"/>).</param>
/// <param name="ServiceAccountJson">
/// The service-account key JSON typed or pasted in the form, or blank to test the key already in force (saved or configured).
/// </param>
public sealed record ConnectionTestRequest(
    string? Ga4PropertyId,
    string? SearchConsoleSite,
    [property: JsonConverter(typeof(InputOnlyStringConverter))] string? ServiceAccountJson)
{
    public override string ToString() => $"{nameof(ConnectionTestRequest)} {{ KeyGiven = {!string.IsNullOrWhiteSpace(ServiceAccountJson)} }}";
}

/// <summary>A string that is read from JSON like any other and always written as <c>null</c>: for a value that may come in and never go out.</summary>
internal sealed class InputOnlyStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetString();

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) => writer.WriteNullValue();
}

/// <summary>
/// The outcome of one check (the key, GA4 or Search Console).
/// <list type="bullet">
/// <item><c>Ok</c> and no code: it works.</item>
/// <item><c>Ok</c> and <c>noData</c>: it works, but the source has nothing yet (new property, new site). Informative, not a failure.</item>
/// <item>Not <c>Ok</c> with a code: what is wrong, one of <see cref="ErrorCodes.All"/>, with its fixed <c>Args</c> (see <see cref="GoogleErrorMapper"/>).</item>
/// <item>Not <c>Ok</c> and no code (<see cref="NotTested"/>): not checked, because something before it failed. The reason is on that other check.</item>
/// </list>
/// </summary>
/// <param name="Ok">Whether the source answered and let the service account in.</param>
/// <param name="ErrorCode">A code from <see cref="ErrorCodes"/>, or null.</param>
/// <param name="Args">The code's fixed arguments (<c>api</c>, <c>source</c>), or null. A concrete type, as every collection in an answer (see <see cref="Reporting.TextRef"/>).</param>
public sealed record SourceTest(bool Ok, string? ErrorCode, Dictionary<string, object?>? Args = null)
{
    /// <summary>It works.</summary>
    public static SourceTest Passed { get; } = new(true, null);

    /// <summary>It was not checked (see the type's remarks).</summary>
    public static SourceTest NotTested { get; } = new(false, null);

    internal static SourceTest Failed(string code) => new(false, code);

    internal static SourceTest Failed(ProblemInfo problem) => new(false, problem.Code, ArgsOf(problem));

    /// <summary>It works, with a note: the source has no data yet.</summary>
    internal static SourceTest ConnectedWith(ProblemInfo note) => new(true, note.Code, ArgsOf(note));

    private static Dictionary<string, object?>? ArgsOf(ProblemInfo problem)
        => problem.Args is null ? null : new Dictionary<string, object?>(problem.Args, StringComparer.Ordinal);
}

/// <summary>
/// The three checks, each on its own. <see cref="ServiceAccountEmail"/> is the address to give access to in Google Analytics and
/// Search Console (public), taken from the key that was tested; it is null when there is no usable key. Nothing of the key is a
/// property, and the record's text and JSON hold only codes, fixed words and that address.
/// </summary>
/// <param name="ServiceAccountEmail">The tested key's service account, or null.</param>
/// <param name="Key">Whether there is a usable key.</param>
/// <param name="Ga4">GA4; <see cref="SourceTest.NotTested"/> when the key is not usable.</param>
/// <param name="SearchConsole">Search Console; <see cref="SourceTest.NotTested"/> when the key is not usable.</param>
public sealed record ConnectionTestResult(string? ServiceAccountEmail, SourceTest Key, SourceTest Ga4, SourceTest SearchConsole);

/// <summary>
/// Test connection: checks the key, then GA4, then Search Console, on the values in the form and before anything is saved, and says
/// for each what is wrong and which fixed code names it. It never throws a Google or key problem: every failure is a code.
///
/// THE KEY. A blank <see cref="ConnectionTestRequest.ServiceAccountJson"/> tests the key in force, through
/// <see cref="IGoogleCredentialSource"/> and the same <see cref="IGoogleClients"/> the report uses. A pasted key is validated, turned
/// into a credential and given clients by <see cref="IGoogleClientFactory"/>: it is never saved, never cached and never logged, and
/// it lives only in locals of the run. A key that cannot be used is <c>keyInvalid</c> (or the credential layer's own code) and no
/// Google call is made. A key that parses but that Google itself refuses (deleted or rotated, or its service account deleted or
/// disabled: the token endpoint answers <c>invalid_grant</c> and the like) is found out by the first Google call; it is reported as
/// <c>keyInvalid</c> on the key and both sources are then <see cref="SourceTest.NotTested"/>.
///
/// THE CHECKS. The two identifiers are normalized first; one that cannot be normalized is <c>ga4PropertyInvalid</c> or
/// <c>searchConsoleInvalid</c> and that source is not called, whatever state the key is in. GA4 is one tiny <c>runReport</c> (one
/// metric, one row, yesterday). Search Console is <c>sites.get</c> (the property exists and the account may read it) and then the
/// probe the fetch uses. The two sources are independent: a refusal by one does not stop the other, and they run side by side. A
/// source that answers but has no data is <c>Ok</c> with <c>noData</c>; one that does not know the property or refuses the account
/// is never a success. Search Console answers <c>sites.get</c> with a 403, not a 404, for a property the account does not hold (the
/// wrong form of one that exists, say), so that 403 is <c>propertyNotFound</c> (its text covers "not found" and "no access" and names
/// the exact forms); a 403 of the probe, once the property is known and readable, stays <c>noAccess</c>. GA4's 403 stays <c>noAccess</c>
/// (its text also asks for the property ID to be checked).
///
/// THE LIMITS, so it cannot be used to burn Google quota. Runs are single-flight per process: a call whose request equals the run
/// in progress joins it and gets its result, a call with a different request waits for that run to end and then runs its own, so
/// two runs never overlap. A call within <see cref="MinimumInterval"/> of the END of the last run, with the same request, gets that
/// run's result again, errors included, without any call to Google. Only the latest run is remembered, and only one whose key was
/// usable: a result that says the key is not ok is never given again. "The same request" is the normalized identifiers and a SHA-256
/// of the key text (never the text); for a blank key it is also the identity of the credential the key in force resolves to (looked
/// up on every call, locally and without Google, and held only weakly), so a key that was saved, replaced or removed is always tested
/// at once and one that is unchanged still replays. A run is not tied to the caller that started it: a caller that cancels stops
/// waiting and nothing else, and the run ends within <see cref="RunTimeout"/> on its own.
///
/// WHAT IS LOGGED. A failure that no code names (<c>unknown</c>) is a Warning: the source, the exception type, the HTTP status when
/// there is one, and the exception itself (a Google or HTTP exception carries Google's answer, never the key). A failure that has a
/// code (no access, property not found, ...) is an expected refusal and a Debug line with the same facts and the code, no exception. A
/// failure to look up or build the key is a Warning with the exception TYPE only. Nothing is logged for a run that went well, and no
/// request body, key, identifier or digest ever is. The result has no correlation id; the controller that shows an <c>unknown</c>
/// adds its own.
/// </summary>
public sealed class ConnectionTester(
    IGoogleCredentialSource credentials,
    IGoogleClients savedClients,
    IGoogleClientFactory factory,
    TimeProvider time,
    ILogger<ConnectionTester> log)
{
    /// <summary>The least time between two runs of the same request: inside it the previous result is given again.</summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(5);

    /// <summary>How long a run may take before it is given up: a source that has not answered by then is <c>unknown</c>.</summary>
    public static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(30);

    // GA4's own word for the day before today, in the property's time zone.
    private const string Yesterday = "yesterday";

    private readonly object _gate = new();
    private Run? _inFlight;
    private Finished? _last;

    private sealed record Run(Fingerprint Request, Task<ConnectionTestResult> Task);

    private sealed record Finished(Fingerprint Request, ConnectionTestResult Result, DateTimeOffset At);

    /// <summary>The clients for the key that was tested, or the code when there is none, and the key's public e-mail.</summary>
    private sealed record KeyOutcome(string? Email, IGoogleClients? Clients, SourceTest Test);

    /// <summary>What the credential source said about the key in force: its answer, or the code of the exception it threw.</summary>
    private sealed record SavedKey(CredentialResult? Result, string? FailureCode);

    /// <summary>
    /// Checks the key and the two properties. Calls overlap and repeat as the type's remarks say.
    /// </summary>
    /// <exception cref="OperationCanceledException">The caller's own token was cancelled; the run is not affected.</exception>
    public async Task<ConnectionTestResult> TestAsync(ConnectionTestRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        // The key in force is looked up first, on every call: it is local (no Google call) and which credential it resolves to is
        // part of what makes two requests the same, so a key saved or replaced since the last run is never answered from the replay.
        var saved = string.IsNullOrWhiteSpace(request.ServiceAccountJson) ? await LookUpSavedKeyAsync(ct).ConfigureAwait(false) : null;
        var fingerprint = Fingerprint.Of(request, saved?.Result);

        while (true)
        {
            Task<ConnectionTestResult>? mine = null;
            Task? other = null;

            lock (_gate)
            {
                if (_inFlight is { } active)
                {
                    if (active.Request.Equals(fingerprint)) mine = active.Task;
                    else other = active.Task;
                }
                else if (_last is { } last && last.Request.Equals(fingerprint) && time.GetUtcNow() - last.At < MinimumInterval)
                {
                    return last.Result;
                }
                else
                {
                    mine = Start(request, saved, fingerprint);
                }
            }

            if (mine is not null) return await mine.WaitAsync(ct).ConfigureAwait(false);

            // Another request is running. Wait for it to end, whatever became of it, and look again.
            try
            {
                await other!.WaitAsync(ct).ConfigureAwait(false);
            }
            catch when (!ct.IsCancellationRequested)
            {
                // The other run failed or was given up: not this caller's concern. Only this caller's own cancellation ends the wait.
            }
        }
    }

    // Called with the gate held. The run does not see the caller's token: it belongs to everybody waiting for it.
    private Task<ConnectionTestResult> Start(ConnectionTestRequest request, SavedKey? saved, Fingerprint fingerprint)
    {
        var task = Task.Run(() => RunAsync(request, saved, fingerprint));
        _inFlight = new Run(fingerprint, task);
        return task;
    }

    private async Task<ConnectionTestResult> RunAsync(ConnectionTestRequest request, SavedKey? saved, Fingerprint fingerprint)
    {
        ConnectionTestResult? result = null;
        try
        {
            using var timeout = new CancellationTokenSource(RunTimeout, time);
            result = await ExecuteAsync(request, saved, timeout.Token).ConfigureAwait(false);
            return result;
        }
        finally
        {
            // The window counts from here, the end of the run. A run that threw is not kept, and neither is one whose key was not ok:
            // nothing about it is worth protecting, and the person is about to fix the key and test again.
            lock (_gate)
            {
                _inFlight = null;
                if (result is { Key.Ok: true }) _last = new Finished(fingerprint, result, time.GetUtcNow());
            }
        }
    }

    private async Task<ConnectionTestResult> ExecuteAsync(ConnectionTestRequest request, SavedKey? saved, CancellationToken ct)
    {
        var property = SettingsNormalizer.NormalizeGa4(request.Ga4PropertyId);
        var site = SettingsNormalizer.NormalizeSearchConsole(request.SearchConsoleSite);
        var key = await ResolveKeyAsync(request.ServiceAccountJson, saved, ct).ConfigureAwait(false);

        // A property that cannot be written is reported without Google; one that can is checked only when there is a key to check with.
        Task<SourceTest> Check(string? normalized, string invalidCode, Func<IGoogleClients, string, Task<SourceTest>> check)
            => normalized is null ? Task.FromResult(SourceTest.Failed(invalidCode))
                : key.Clients is { } clients ? check(clients, normalized)
                : Task.FromResult(SourceTest.NotTested);

        var ga4 = Check(property, ErrorCodes.Ga4PropertyInvalid, (clients, id) => CheckGa4Async(clients, id, ct));
        var searchConsole = Check(site, ErrorCodes.SearchConsoleInvalid, (clients, url) => CheckSearchConsoleAsync(clients, url, ct));
        await Task.WhenAll(ga4, searchConsole).ConfigureAwait(false);

        // Google refuses the key itself (the token endpoint says so on the first call): it is the key that is wrong, whichever source
        // noticed, so the key says so and neither source has been tested. Only a property that was never sent stays reported.
        if (ga4.Result.ErrorCode == ErrorCodes.KeyInvalid || searchConsole.Result.ErrorCode == ErrorCodes.KeyInvalid)
        {
            return new ConnectionTestResult(key.Email, SourceTest.Failed(ErrorCodes.KeyInvalid), NeverSent(ga4.Result), NeverSent(searchConsole.Result));
        }

        return new ConnectionTestResult(key.Email, key.Test, ga4.Result, searchConsole.Result);
    }

    private static SourceTest NeverSent(SourceTest test)
        => test.ErrorCode is ErrorCodes.Ga4PropertyInvalid or ErrorCodes.SearchConsoleInvalid ? test : SourceTest.NotTested;

    // ---- The key ------------------------------------------------------------------------------------------------------

    /// <summary>Asks the credential source about the key in force. A source that fails is a key problem as a result; only the caller's own cancellation is not.</summary>
    private async Task<SavedKey> LookUpSavedKeyAsync(CancellationToken ct)
    {
        try
        {
            return new SavedKey(await credentials.GetAsync(ct).ConfigureAwait(false), null);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            LogKeyFailure(ex);
            return new SavedKey(null, GoogleErrorMapper.Map(ex).Code);
        }
    }

    private async Task<KeyOutcome> ResolveKeyAsync(string? pasted, SavedKey? saved, CancellationToken ct)
    {
        try
        {
            return saved is not null ? SavedKeyOutcome(saved) : await PastedKeyAsync(pasted!, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // A loader that throws something unexpected, the run's timeout: a result, never an exception, and never any of its text.
            LogKeyFailure(ex);
            return Unusable(GoogleErrorMapper.Map(ex).Code);
        }
    }

    private KeyOutcome SavedKeyOutcome(SavedKey saved)
    {
        if (saved.Result is { Credential: not null, ErrorCode: null } usable)
        {
            return new KeyOutcome(usable.ClientEmail, savedClients, SourceTest.Passed);
        }

        return Unusable(saved.Result is null ? saved.FailureCode ?? ErrorCodes.Unknown : saved.Result.ErrorCode ?? ErrorCodes.NotConfigured);
    }

    private async Task<KeyOutcome> PastedKeyAsync(string pasted, CancellationToken ct)
    {
        var check = ServiceAccountKey.Validate(pasted);
        if (!check.IsValid || !ServiceAccountKey.TryNormalize(pasted, out var normalized)) return Unusable(ErrorCodes.KeyInvalid);

        // Google's loader can still refuse a key that has the right shape (a private key that is not RSA).
        if (await ServiceAccountCredentials.TryCreateAsync(normalized, ct).ConfigureAwait(false) is not { } credential)
        {
            return Unusable(ErrorCodes.KeyInvalid);
        }

        return new KeyOutcome(check.ClientEmail, factory.Create(credential), SourceTest.Passed);
    }

    // Whatever code the credential layer gave, only a known one is passed on.
    private static KeyOutcome Unusable(string code)
        => new(null, null, SourceTest.Failed(ErrorCodes.IsKnown(code) ? code : ErrorCodes.Unknown));

    // ---- The two sources ----------------------------------------------------------------------------------------------

    /// <summary>A report of one metric, one row, for yesterday. No row is a property that works but has nothing yet.</summary>
    private async Task<SourceTest> CheckGa4Async(IGoogleClients clients, string propertyId, CancellationToken ct)
    {
        try
        {
            var request = new RunReportRequest
            {
                Property = propertyId,
                DateRanges = [new DateRange { StartDate = Yesterday, EndDate = Yesterday }],
                Dimensions = [new Dimension { Name = "date" }],
                Metrics = [new Metric { Name = Ga4Requests.SessionsMetric }],
                Limit = 1,
            };

            var response = await Ga4Requests.RunAsync(clients, request, ct).ConfigureAwait(false);
            return Ga4Requests.RowsOf(response).Count > 0
                ? SourceTest.Passed
                : SourceTest.ConnectedWith(GoogleErrorMapper.NoData(GoogleErrorMapper.Ga4));
        }
        catch (Exception ex)
        {
            return Failure(ex, GoogleErrorMapper.Ga4, GoogleErrorMapper.Map(ex, GoogleErrorMapper.Ga4));
        }
    }

    /// <summary>
    /// <c>sites.get</c> first: Search Console answers an empty report both for a property it does not know and for one with no data,
    /// so only a property it hands back can be told apart from the wrong form. Then the probe of the fetch: no rows is a property
    /// that works but has nothing yet.
    /// </summary>
    private async Task<SourceTest> CheckSearchConsoleAsync(IGoogleClients clients, string site, CancellationToken ct)
    {
        try
        {
            var service = await clients.GetSearchConsoleAsync(ct).ConfigureAwait(false);

            try
            {
                await service.Sites.Get(site).ExecuteAsync(ct).ConfigureAwait(false);
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Forbidden)
            {
                // Search Console answers 403, not 404, for a property the account does not hold, the wrong form of one that exists
                // included. Unless the API is off or a limit is reached, the property is what is wrong (or not shared with the account:
                // the text covers both). A 401 says nothing about the property and stays noAccess.
                var problem = GoogleErrorMapper.Map(ex, GoogleErrorMapper.SearchConsole);
                return Failure(ex, GoogleErrorMapper.SearchConsole, problem.Code == ErrorCodes.NoAccess ? problem with { Code = ErrorCodes.PropertyNotFound } : problem);
            }

            var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
            var probe = await service.Searchanalytics.Query(GscAgent.BuildProbeRequest(today), site).ExecuteAsync(ct).ConfigureAwait(false);

            return probe.Rows is { Count: > 0 }
                ? SourceTest.Passed
                : SourceTest.ConnectedWith(GoogleErrorMapper.NoData(GoogleErrorMapper.SearchConsole));
        }
        catch (Exception ex)
        {
            return Failure(ex, GoogleErrorMapper.SearchConsole, GoogleErrorMapper.Map(ex, GoogleErrorMapper.SearchConsole));
        }
    }

    // ---- What is logged -----------------------------------------------------------------------------------------------

    /// <summary>
    /// The result of a source that failed, after it is logged: a Warning with the exception when no code names the failure, a Debug line
    /// (no exception) when one does.
    /// </summary>
    private SourceTest Failure(Exception ex, string source, ProblemInfo problem)
    {
        var status = GoogleErrorMapper.HttpStatusOf(ex);
        if (problem.Code == ErrorCodes.Unknown)
        {
            log.LogWarning(ex, "Test connection: {Source} failed with {ExceptionType} (HTTP status {HttpStatus}) and no code names it.", source, ex.GetType().Name, status);
        }
        else
        {
            log.LogDebug("Test connection: {Source} reported {Code} ({ExceptionType}, HTTP status {HttpStatus}).", source, problem.Code, ex.GetType().Name, status);
        }

        return SourceTest.Failed(problem);
    }

    // Only the exception TYPE: what a credential or key layer says can quote more than is safe to keep in a log.
    private void LogKeyFailure(Exception ex)
        => log.LogWarning("Test connection could not look up or build the key: {ExceptionType}.", ex.GetType().Name);

    // ---- What makes two requests the same -----------------------------------------------------------------------------

    /// <summary>
    /// A request, reduced to what decides its outcome: the two identifiers as they normalize (null, a property that cannot be
    /// written, is the empty text), a SHA-256 of the key text (empty when there is none to hash), and for "the key in force" the
    /// credential result it resolved to. That is held WEAKLY and compared by reference: the credential source hands back the same
    /// instance while the key is unchanged and a new one when it is saved, replaced or removed (and a new one for every failure), so
    /// a changed key is a different request, and nothing here keeps a key alive. The key text is not here, and neither the digest nor
    /// this value is ever printed or logged.
    /// </summary>
    private readonly record struct Fingerprint(string Ga4, string Site, string KeyDigest, WeakReference<CredentialResult>? SavedKey)
    {
        public static Fingerprint Of(ConnectionTestRequest request, CredentialResult? saved) => new(
            SettingsNormalizer.NormalizeGa4(request.Ga4PropertyId) ?? "",
            SettingsNormalizer.NormalizeSearchConsole(request.SearchConsoleSite) ?? "",
            Digest(request.ServiceAccountJson),
            saved is null ? null : new WeakReference<CredentialResult>(saved));

        private static string Digest(string? json)
            => string.IsNullOrWhiteSpace(json) ? "" : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json.Trim())));

        public bool Equals(Fingerprint other)
            => Ga4 == other.Ga4 && Site == other.Site && KeyDigest == other.KeyDigest && SameKey(SavedKey, other.SavedKey);

        public override int GetHashCode() => HashCode.Combine(Ga4, Site, KeyDigest);

        public override string ToString() => nameof(Fingerprint);

        private static bool SameKey(WeakReference<CredentialResult>? a, WeakReference<CredentialResult>? b)
            => a is null ? b is null : b is not null && a.TryGetTarget(out var left) && b.TryGetTarget(out var right) && ReferenceEquals(left, right);
    }
}
