using System.Net;
using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>
/// The Test connection service wired the way the host will wire it, over fakes: the saved key's clients and the clients the factory
/// builds for a key that is typed in but not saved (each pair over its own fake transports, so a test sees which pair a run used),
/// a credential source that hands out a real credential for a synthetic key, and a clock a test can move.
/// </summary>
internal sealed class ConnectionRig
{
    public const string Ga4Id = "123456789";
    public const string Site = "sc-domain:example.com";

    /// <summary>A GA4 answer with one row for yesterday.</summary>
    public const string Ga4Rows = """{"rows":[{"dimensionValues":[{"value":"20260714"}],"metricValues":[{"value":"42"}]}],"rowCount":1}""";

    /// <summary>What <c>sites.get</c> answers for a property the service account may read.</summary>
    public const string SiteJson = """{"siteUrl":"sc-domain:example.com","permissionLevel":"siteOwner"}""";

    /// <summary>A Search Console probe answer with a day of data.</summary>
    public const string ProbeRows = """{"rows":[{"keys":["2026-07-12"],"clicks":3,"impressions":50,"ctr":0.06,"position":8.5}]}""";

    public static readonly DateTimeOffset Now = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    private ConnectionRig(
        FakeTimeProvider time,
        SplitGoogle saved,
        SplitGoogle typed,
        IGoogleCredentialSource credentials,
        IGoogleClientFactory factory,
        CredentialResult source,
        CapturingLogger<ConnectionTester> log)
    {
        Time = time;
        Saved = saved;
        Typed = typed;
        Credentials = credentials;
        Factory = factory;
        Source = source;
        Log = log;
        Tester = new ConnectionTester(credentials, saved, factory, time, log);
    }

    public FakeTimeProvider Time { get; }

    /// <summary>The clients of the saved or configured key.</summary>
    public SplitGoogle Saved { get; }

    /// <summary>The clients the factory builds for a pasted key.</summary>
    public SplitGoogle Typed { get; }

    public IGoogleCredentialSource Credentials { get; }

    public IGoogleClientFactory Factory { get; }

    /// <summary>What the credential source answers (see <see cref="SetSource"/>).</summary>
    public CredentialResult Source { get; private set; }

    /// <summary>Everything the tester logged.</summary>
    public CapturingLogger<ConnectionTester> Log { get; }

    /// <summary>Changes what the credential source answers from now on, as saving or replacing a key does.</summary>
    public void SetSource(CredentialResult source)
    {
        Source = source;
        Credentials.GetAsync(Arg.Any<CancellationToken>()).Returns(source);
    }

    /// <summary>A usable credential result for a synthetic key of <paramref name="email"/>: a new credential, so a new instance each call.</summary>
    public static async Task<CredentialResult> UsableAsync(string email)
    {
        var credential = await ServiceAccountCredentials.TryCreateAsync(FakeServiceAccount.Json(email), CancellationToken.None)
                         ?? throw new InvalidOperationException("the synthetic key should load");
        return new CredentialResult(credential, email, KeySourceKind.Database, null);
    }

    public ConnectionTester Tester { get; }

    /// <summary>The credential the factory was last asked to build clients for, when it was asked.</summary>
    public GoogleCredential? FactoryCredential { get; private set; }

    /// <summary>The default GA4 answer: a row of data. Replace it per test with <paramref name="ga4"/>.</summary>
    public static FakeGoogleTransport.Answer Ga4Ok(FakeGoogleTransport.Call call) => new(HttpStatusCode.OK, Ga4Rows);

    /// <summary>The default Search Console answer: the site for <c>sites.get</c>, a day of data for the probe.</summary>
    public static FakeGoogleTransport.Answer SearchConsoleOk(FakeGoogleTransport.Call call)
        => new(HttpStatusCode.OK, IsProbe(call) ? ProbeRows : SiteJson);

    /// <summary>Whether a Search Console call is the Search Analytics query (the probe) and not <c>sites.get</c>.</summary>
    public static bool IsProbe(FakeGoogleTransport.Call call) => call.Uri.AbsolutePath.EndsWith("/searchAnalytics/query", StringComparison.Ordinal);

    /// <param name="ga4">How the GA4 transports answer, for the saved and the typed-in key's clients alike.</param>
    /// <param name="searchConsole">The same for Search Console.</param>
    /// <param name="source">What the credential source answers; by default a real credential for <see cref="FakeServiceAccount.Json"/>.</param>
    public static async Task<ConnectionRig> CreateAsync(
        Func<FakeGoogleTransport.Call, FakeGoogleTransport.Answer>? ga4 = null,
        Func<FakeGoogleTransport.Call, FakeGoogleTransport.Answer>? searchConsole = null,
        CredentialResult? source = null)
    {
        var time = new FakeTimeProvider(Now);

        SplitGoogle Pair() => new(
            new FakeAnalyticsData(new FakeGoogleTransport(ga4 ?? Ga4Ok)),
            new FakeSearchConsole(new FakeGoogleTransport(searchConsole ?? SearchConsoleOk)));

        var saved = Pair();
        var typed = Pair();

        var answer = source ?? await UsableAsync(FakeServiceAccount.DefaultClientEmail);

        var credentials = Substitute.For<IGoogleCredentialSource>();
        credentials.GetAsync(Arg.Any<CancellationToken>()).Returns(answer);

        var factory = Substitute.For<IGoogleClientFactory>();
        ConnectionRig? rig = null;
        factory.Create(Arg.Any<GoogleCredential>()).Returns(call =>
        {
            rig!.FactoryCredential = call.Arg<GoogleCredential>();
            return typed;
        });

        rig = new ConnectionRig(time, saved, typed, credentials, factory, answer, new CapturingLogger<ConnectionTester>());
        return rig;
    }

    public static ConnectionTestRequest Request(
        string? key = null,
        string? ga4 = Ga4Id,
        string? site = Site)
        => new(ga4, site, key);
}
