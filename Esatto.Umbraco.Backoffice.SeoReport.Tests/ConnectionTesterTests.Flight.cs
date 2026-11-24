using System.Collections;
using System.Reflection;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using NSubstitute;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// The other half of <see cref="ConnectionTesterTests"/>: Test connection cannot be used to burn Google quota. Calls with the same
/// request share one run, a different request waits for the run in progress and then runs its own, a caller that gives up does not
/// stop the run the others wait for, and a second call within five seconds of the end of a run gets that run's result without
/// touching Google. The clock is a fake, so no test waits for real seconds; a run is frozen at its first GA4 call with
/// <see cref="SplitGoogle.Hold"/>.
/// </summary>
public partial class ConnectionTesterTests
{
    private static readonly TimeSpan JustUnderTheWindow = ConnectionTester.MinimumInterval - TimeSpan.FromMilliseconds(1);

    private static ConnectionTestRequest OtherProperty() => ConnectionRig.Request(ga4: "987654321");

    /// <summary>Waits (in real time, a few milliseconds at most in practice) until a run of another task has reached a point.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("the run did not reach the expected point");
            await Task.Delay(5);
        }
    }

    // ---- Single-flight ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Calls_with_the_same_request_that_overlap_share_one_run()
    {
        var rig = await ConnectionRig.CreateAsync();
        var hold = new TaskCompletionSource();
        rig.Saved.Hold = hold.Task;

        var first = rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);
        await WaitUntilAsync(() => rig.Saved.Ga4ServiceRequests == 1);
        var second = rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);
        var third = rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);
        Assert.False(second.IsCompleted);

        hold.SetResult();
        var results = await Task.WhenAll(first, second, third);

        Assert.Same(results[0], results[1]);
        Assert.Same(results[0], results[2]);
        Assert.Equal(1, rig.Saved.Ga4ServiceRequests);
        Assert.Single(rig.Saved.Ga4.Calls);
        Assert.Equal(2, rig.Saved.SearchConsole.Calls.Count);
    }

    [Fact]
    public async Task A_different_request_waits_for_the_run_in_progress_and_then_runs_its_own()
    {
        var rig = await ConnectionRig.CreateAsync();
        var hold = new TaskCompletionSource();
        rig.Saved.Hold = hold.Task;

        var first = rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);
        await WaitUntilAsync(() => rig.Saved.Ga4ServiceRequests == 1);
        var second = rig.Tester.TestAsync(OtherProperty(), CancellationToken.None);

        // Two runs never overlap: while the first is parked, the second has not begun.
        await Task.Delay(100);
        Assert.False(second.IsCompleted);
        Assert.Equal(1, rig.Saved.Ga4ServiceRequests);

        hold.SetResult();
        var one = await first;
        var two = await second;

        Assert.NotSame(one, two);
        Assert.Equal(2, rig.Saved.Ga4.Calls.Count);
        Assert.EndsWith("/properties/123456789:runReport", rig.Saved.Ga4.Calls[0].Uri.AbsolutePath);
        Assert.EndsWith("/properties/987654321:runReport", rig.Saved.Ga4.Calls[1].Uri.AbsolutePath);
    }

    [Fact]
    public async Task A_caller_that_gives_up_does_not_stop_the_run_the_others_are_waiting_for()
    {
        var rig = await ConnectionRig.CreateAsync();
        var hold = new TaskCompletionSource();
        rig.Saved.Hold = hold.Task;
        using var giveUp = new CancellationTokenSource();

        var impatient = rig.Tester.TestAsync(ConnectionRig.Request(), giveUp.Token);
        await WaitUntilAsync(() => rig.Saved.Ga4ServiceRequests == 1);
        var patient = rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        await giveUp.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => impatient);
        Assert.False(patient.IsCompleted);

        hold.SetResult();
        var result = await patient;

        Assert.True(result.Ga4.Ok);
        Assert.True(result.SearchConsole.Ok);
        Assert.Equal(1, rig.Saved.Ga4ServiceRequests);
        Assert.Single(rig.Saved.Ga4.Calls);
    }

    [Fact]
    public async Task A_run_whose_only_caller_gave_up_still_ends_and_its_result_is_kept_for_the_next_call()
    {
        var rig = await ConnectionRig.CreateAsync();
        var hold = new TaskCompletionSource();
        rig.Saved.Hold = hold.Task;
        using var giveUp = new CancellationTokenSource();

        var impatient = rig.Tester.TestAsync(ConnectionRig.Request(), giveUp.Token);
        await WaitUntilAsync(() => rig.Saved.Ga4ServiceRequests == 1);
        await giveUp.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => impatient);

        hold.SetResult();
        await WaitUntilAsync(() => rig.Saved.SearchConsole.Calls.Count == 2 && rig.Saved.Ga4.Calls.Count == 1);
        var later = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.True(later.Ga4.Ok);
        Assert.Single(rig.Saved.Ga4.Calls);
        Assert.Equal(2, rig.Saved.SearchConsole.Calls.Count);
    }

    [Fact]
    public async Task A_call_that_is_cancelled_before_it_starts_starts_nothing()
    {
        var rig = await ConnectionRig.CreateAsync();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rig.Tester.TestAsync(ConnectionRig.Request(), cancelled.Token));

        Assert.Equal(0, rig.Saved.Calls);
        Assert.Equal(0, rig.Saved.Ga4ServiceRequests);
        await rig.Credentials.DidNotReceiveWithAnyArgs().GetAsync(default);
    }

    [Fact]
    public async Task A_run_that_never_answers_is_given_up_after_its_timeout_and_only_that_source_is_unknown()
    {
        var rig = await ConnectionRig.CreateAsync();
        rig.Saved.Hold = new TaskCompletionSource().Task;

        var call = rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);
        await WaitUntilAsync(() => rig.Saved.Ga4ServiceRequests == 1 && rig.Saved.SearchConsole.Calls.Count == 2);
        await Task.Delay(200); // lets Search Console's answer be read, so only the parked GA4 call is cut short by the timeout
        rig.Time.Advance(ConnectionTester.RunTimeout + TimeSpan.FromSeconds(1));
        var result = await call.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(new SourceTest(false, ErrorCodes.Unknown, null), result.Ga4);
        Assert.True(result.SearchConsole.Ok);
        Assert.True(result.Key.Ok);
    }

    // ---- Five seconds -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_second_call_within_five_seconds_gets_the_previous_result_without_touching_Google()
    {
        var rig = await ConnectionRig.CreateAsync();
        var first = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);
        var calls = rig.Saved.Calls;

        rig.Time.Advance(JustUnderTheWindow);
        var second = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.Same(first, second);
        Assert.Equal(calls, rig.Saved.Calls);
        Assert.Equal(1, rig.Saved.Ga4ServiceRequests);

        // The saved key is looked up on every call (locally; it decides whether the replay still applies), Google is not called.
        await rig.Credentials.Received(2).GetAsync(Arg.Any<CancellationToken>());
    }

    // ---- A key saved, replaced or removed is never answered from the replay ------------------------------------------------

    [Fact]
    public async Task A_key_saved_inside_the_window_is_tested_at_once_and_not_answered_with_the_stale_result()
    {
        var rig = await ConnectionRig.CreateAsync(source: new CredentialResult(null, null, KeySourceKind.None, ErrorCodes.NotConfigured));
        var before = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);
        Assert.Equal(ErrorCodes.NotConfigured, before.Key.ErrorCode);

        rig.SetSource(await ConnectionRig.UsableAsync("saved@proj.iam.gserviceaccount.com"));
        rig.Time.Advance(TimeSpan.FromSeconds(1));
        var after = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.True(after.Key.Ok);
        Assert.Equal("saved@proj.iam.gserviceaccount.com", after.ServiceAccountEmail);
        Assert.True(after.Ga4.Ok);
        Assert.Single(rig.Saved.Ga4.Calls);
    }

    [Fact]
    public async Task A_key_replaced_inside_the_window_shows_the_new_keys_address_and_is_tested_again()
    {
        var rig = await ConnectionRig.CreateAsync(source: await ConnectionRig.UsableAsync("one@proj.iam.gserviceaccount.com"));
        var first = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        rig.SetSource(await ConnectionRig.UsableAsync("two@proj.iam.gserviceaccount.com"));
        rig.Time.Advance(TimeSpan.FromSeconds(1));
        var second = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.Equal("one@proj.iam.gserviceaccount.com", first.ServiceAccountEmail);
        Assert.Equal("two@proj.iam.gserviceaccount.com", second.ServiceAccountEmail);
        Assert.Equal(2, rig.Saved.Ga4.Calls.Count);
    }

    [Fact]
    public async Task A_key_removed_inside_the_window_is_reported_as_gone_and_not_as_the_old_result()
    {
        var rig = await ConnectionRig.CreateAsync();
        var first = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);
        Assert.True(first.Key.Ok);

        rig.SetSource(new CredentialResult(null, null, KeySourceKind.None, ErrorCodes.NotConfigured));
        var second = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.Equal(ErrorCodes.NotConfigured, second.Key.ErrorCode);
        Assert.Equal(SourceTest.NotTested, second.Ga4);
        Assert.Single(rig.Saved.Ga4.Calls);
    }

    [Fact]
    public async Task A_key_that_is_unchanged_is_still_replayed_with_no_call_to_Google()
    {
        var rig = await ConnectionRig.CreateAsync();
        var first = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);
        var calls = rig.Saved.Calls;

        rig.Time.Advance(TimeSpan.FromSeconds(2));
        var second = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.Same(first, second);
        Assert.Equal(calls, rig.Saved.Calls);
    }

    [Fact]
    public async Task A_failed_key_result_is_never_given_again_even_when_the_credential_source_answers_the_same_failure()
    {
        var failure = new CredentialResult(null, null, KeySourceKind.None, ErrorCodes.KeyUnreadable);
        var rig = await ConnectionRig.CreateAsync(source: failure);

        var first = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);
        var second = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.Equal(ErrorCodes.KeyUnreadable, second.Key.ErrorCode);
        Assert.NotSame(first, second);
        await rig.Credentials.Received(2).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task What_the_tester_remembers_about_a_saved_key_does_not_keep_the_credential_alive()
    {
        var rig = await ConnectionRig.CreateAsync();
        await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        var held = HeldObjects(rig.Tester).Any(item => ReferenceEquals(item, rig.Source) || ReferenceEquals(item, rig.Source.Credential));

        Assert.False(held);
    }

    [Fact]
    public async Task After_five_seconds_the_next_call_runs_again()
    {
        var rig = await ConnectionRig.CreateAsync();
        var first = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        rig.Time.Advance(ConnectionTester.MinimumInterval);
        var second = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.NotSame(first, second);
        Assert.Equal(2, rig.Saved.Ga4.Calls.Count);
        Assert.Equal(4, rig.Saved.SearchConsole.Calls.Count);
    }

    [Fact]
    public async Task The_five_seconds_count_from_the_end_of_the_run_not_from_its_start()
    {
        var rig = await ConnectionRig.CreateAsync();
        var hold = new TaskCompletionSource();
        rig.Saved.Hold = hold.Task;

        var slow = rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);
        await WaitUntilAsync(() => rig.Saved.Ga4ServiceRequests == 1);
        rig.Time.Advance(TimeSpan.FromSeconds(10));
        hold.SetResult();
        var first = await slow;

        rig.Time.Advance(JustUnderTheWindow);
        Assert.Same(first, await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None));

        rig.Time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.NotSame(first, await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None));
    }

    [Fact]
    public async Task A_failed_result_is_given_again_inside_the_window_too_and_is_no_new_error()
    {
        var rig = await ConnectionRig.CreateAsync(ga4: _ => GoogleErrorBodies.DeniedAnswer());
        var first = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        rig.Time.Advance(TimeSpan.FromSeconds(1));
        var second = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.Same(first, second);
        Assert.Equal(ErrorCodes.NoAccess, second.Ga4.ErrorCode);
        Assert.Single(rig.Saved.Ga4.Calls);
    }

    // ---- A different request is never answered with another one's result ----------------------------------------------

    [Fact]
    public async Task A_different_property_inside_the_window_runs_and_gets_its_own_result()
    {
        var rig = await ConnectionRig.CreateAsync(ga4: call => call.Uri.AbsolutePath.Contains("987654321") ? GoogleErrorBodies.NotFoundAnswer() : ConnectionRig.Ga4Ok(call));
        var first = await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        rig.Time.Advance(TimeSpan.FromSeconds(1));
        var second = await rig.Tester.TestAsync(OtherProperty(), CancellationToken.None);

        Assert.True(first.Ga4.Ok);
        Assert.Equal(ErrorCodes.PropertyNotFound, second.Ga4.ErrorCode);
        Assert.Equal(2, rig.Saved.Ga4.Calls.Count);
    }

    [Fact]
    public async Task A_different_site_inside_the_window_runs_too()
    {
        var rig = await ConnectionRig.CreateAsync();
        await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        await rig.Tester.TestAsync(ConnectionRig.Request(site: "https://www.example.com/"), CancellationToken.None);

        Assert.Equal(4, rig.Saved.SearchConsole.Calls.Count);
    }

    [Fact]
    public async Task A_different_key_inside_the_window_runs_and_is_tested_with_its_own_clients()
    {
        var rig = await ConnectionRig.CreateAsync();
        await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        var typed = await rig.Tester.TestAsync(ConnectionRig.Request(FakeServiceAccount.Json("typed@proj.iam.gserviceaccount.com")), CancellationToken.None);
        var typedAgain = await rig.Tester.TestAsync(ConnectionRig.Request(FakeServiceAccount.Json("typed@proj.iam.gserviceaccount.com")), CancellationToken.None);
        var other = await rig.Tester.TestAsync(ConnectionRig.Request(FakeServiceAccount.Json("other@proj.iam.gserviceaccount.com")), CancellationToken.None);

        Assert.Equal("typed@proj.iam.gserviceaccount.com", typed.ServiceAccountEmail);
        Assert.Same(typed, typedAgain);
        Assert.Equal("other@proj.iam.gserviceaccount.com", other.ServiceAccountEmail);
        Assert.Equal(2, rig.Typed.Ga4.Calls.Count);
    }

    [Fact]
    public async Task Only_the_latest_request_is_remembered_so_going_back_to_an_earlier_one_runs_it_again()
    {
        var rig = await ConnectionRig.CreateAsync();
        await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);
        await rig.Tester.TestAsync(OtherProperty(), CancellationToken.None);

        await rig.Tester.TestAsync(ConnectionRig.Request(), CancellationToken.None);

        Assert.Equal(3, rig.Saved.Ga4.Calls.Count);
    }

    [Fact]
    public async Task The_same_property_written_another_way_is_the_same_request()
    {
        var rig = await ConnectionRig.CreateAsync();
        var first = await rig.Tester.TestAsync(ConnectionRig.Request(ga4: "123456789", site: "example.com"), CancellationToken.None);

        var second = await rig.Tester.TestAsync(ConnectionRig.Request(ga4: " Properties/123456789 ", site: "SC-DOMAIN:EXAMPLE.com/"), CancellationToken.None);

        Assert.Same(first, second);
        Assert.Single(rig.Saved.Ga4.Calls);
    }

    // ---- What the tester keeps ----------------------------------------------------------------------------------------

    /// <summary>
    /// Every object reachable from the tester's own state (what it remembers between calls), not from the services it was given.
    /// It walks fields of this package's types and of collections, which is all the state holds; a weak reference is not followed.
    /// </summary>
    private static List<object> HeldObjects(ConnectionTester tester)
    {
        const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type[] given = [typeof(IGoogleCredentialSource), typeof(IGoogleClients), typeof(IGoogleClientFactory), typeof(TimeProvider)];

        var held = new List<object>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var pending = new Queue<object>();
        foreach (var field in typeof(ConnectionTester).GetFields(All).Where(field => !given.Any(type => type.IsAssignableFrom(field.FieldType))))
        {
            if (field.GetValue(tester) is { } value) pending.Enqueue(value);
        }

        while (pending.Count > 0)
        {
            var item = pending.Dequeue();
            held.Add(item);
            if (item is string) continue;

            var type = item.GetType();
            if (type.IsPrimitive || type.IsEnum || (!type.IsValueType && !seen.Add(item))) continue;

            if (item is IEnumerable items)
            {
                foreach (var element in items)
                {
                    if (element is not null) pending.Enqueue(element);
                }

                continue;
            }

            var ours = type.Assembly == typeof(ConnectionTester).Assembly
                       || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>));
            if (!ours) continue;

            for (var t = type; t is not null && t != typeof(object); t = t.BaseType)
            {
                foreach (var field in t.GetFields(All | BindingFlags.DeclaredOnly))
                {
                    if (field.GetValue(item) is { } value) pending.Enqueue(value);
                }
            }
        }

        return held;
    }

    private static List<string> StringsKept(ConnectionTester tester) => [.. HeldObjects(tester).OfType<string>()];

    [Fact]
    public async Task What_the_tester_remembers_holds_neither_the_key_nor_any_text_of_it()
    {
        var rig = await ConnectionRig.CreateAsync();
        var pemBody = FakeServiceAccount.PrivateKeyPem.Split('\n')[1];

        await rig.Tester.TestAsync(ConnectionRig.Request(KeyWithMarkers()), CancellationToken.None);
        await rig.Tester.TestAsync(ConnectionRig.Request(KeyWithMarkers()), CancellationToken.None); // the replay
        var kept = StringsKept(rig.Tester);

        // The walk is real: it reaches what the tester remembers, the result with the key's e-mail address.
        Assert.Contains(kept, text => text.Contains(FakeServiceAccount.DefaultClientEmail));
        Assert.All(kept, text =>
        {
            Assert.DoesNotContain(Marker, text);
            Assert.DoesNotContain("PRIVATE KEY", text);
            Assert.DoesNotContain(pemBody, text);
            Assert.DoesNotContain("\"private_key\"", text);
        });
    }
}
