using Esatto.Umbraco.Backoffice.SeoReport.Google;
using Esatto.Umbraco.Backoffice.SeoReport.Reporting;
using Esatto.Umbraco.Backoffice.SeoReport.Settings;
using Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;
using Google.Apis.AnalyticsData.v1beta;
using Google.Apis.Auth.OAuth2;
using Google.Apis.SearchConsole.v1;
using NSubstitute;
using Xunit;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests;

/// <summary>
/// Where the Google services come from: the credential in force, turned into one pair of services that is reused until the
/// credential changes, and the code (never anything else) when there is no credential.
/// </summary>
public class GoogleClientsTests
{
    private static async Task<GoogleCredential> NewCredentialAsync()
        => await ServiceAccountCredentials.TryCreateAsync(FakeServiceAccount.Json(), CancellationToken.None)
           ?? throw new InvalidOperationException("the synthetic key should load");

    private static CredentialResult Usable(GoogleCredential credential)
        => new(credential, FakeServiceAccount.DefaultClientEmail, KeySourceKind.Database, null);

    private static CredentialResult Failed(string code) => new(null, null, KeySourceKind.None, code);

    private sealed class SwitchableSource(CredentialResult first) : IGoogleCredentialSource
    {
        public CredentialResult Result { get; set; } = first;

        public int Asked { get; private set; }

        public Task<CredentialResult> GetAsync(CancellationToken ct = default)
        {
            Asked++;
            return Task.FromResult(Result);
        }
    }

    [Fact]
    public void ReportFetchException_CarriesTheCodeAndNothingElse()
    {
        var error = new ReportFetchException("keyInvalid");

        Assert.Equal("keyInvalid", error.Code);
        Assert.Equal("keyInvalid", error.Message);
        Assert.Null(error.InnerException);
    }

    /// <summary>
    /// With no usable credential the call fails with the credential layer's own code, from either service, and no service is
    /// ever built for it.
    /// </summary>
    [Theory]
    [InlineData(KeyErrorCodes.NotConfigured)]
    [InlineData(KeyErrorCodes.KeyUnreadable)]
    [InlineData(KeyErrorCodes.KeyInvalid)]
    public async Task ACredentialError_IsThrownAsAReportFetchExceptionAndTheFactoryIsNeverUsed(string code)
    {
        var factory = Substitute.For<IGoogleClientFactory>();
        var clients = new GoogleClients(new SwitchableSource(Failed(code)), factory);

        var analytics = await Assert.ThrowsAsync<ReportFetchException>(() => clients.GetAnalyticsDataAsync(CancellationToken.None));
        var searchConsole = await Assert.ThrowsAsync<ReportFetchException>(() => clients.GetSearchConsoleAsync(CancellationToken.None));

        Assert.Equal(code, analytics.Code);
        Assert.Equal(code, analytics.Message);
        Assert.Equal(code, searchConsole.Code);
        factory.DidNotReceiveWithAnyArgs().Create(default!);
    }

    [Fact]
    public async Task ANullCredentialWithoutACode_IsReportedAsNotConfigured()
    {
        var factory = Substitute.For<IGoogleClientFactory>();
        var clients = new GoogleClients(new SwitchableSource(new CredentialResult(null, null, KeySourceKind.None, null)), factory);

        var error = await Assert.ThrowsAsync<ReportFetchException>(() => clients.GetAnalyticsDataAsync(CancellationToken.None));

        Assert.Equal(KeyErrorCodes.NotConfigured, error.Code);
        factory.DidNotReceiveWithAnyArgs().Create(default!);
    }

    [Fact]
    public async Task TheServicesAreBuiltOncePerCredentialAndShared()
    {
        var credential = await NewCredentialAsync();
        var built = Substitute.For<IGoogleClients>();
        var factory = Substitute.For<IGoogleClientFactory>();
        factory.Create(credential).Returns(built);
        var source = new SwitchableSource(Usable(credential));
        var clients = new GoogleClients(source, factory);
        var analytics = new AnalyticsDataService();
        var searchConsole = new SearchConsoleService();
        built.GetAnalyticsDataAsync(Arg.Any<CancellationToken>()).Returns(analytics);
        built.GetSearchConsoleAsync(Arg.Any<CancellationToken>()).Returns(searchConsole);

        Assert.Same(analytics, await clients.GetAnalyticsDataAsync(CancellationToken.None));
        Assert.Same(analytics, await clients.GetAnalyticsDataAsync(CancellationToken.None));
        Assert.Same(searchConsole, await clients.GetSearchConsoleAsync(CancellationToken.None));

        factory.Received(1).Create(credential);
        Assert.Equal(3, source.Asked);
    }

    [Fact]
    public async Task ANewCredentialInstance_BuildsNewServices()
    {
        var first = await NewCredentialAsync();
        var second = await NewCredentialAsync();
        var factory = Substitute.For<IGoogleClientFactory>();
        var firstClients = Substitute.For<IGoogleClients>();
        var secondClients = Substitute.For<IGoogleClients>();
        factory.Create(first).Returns(firstClients);
        factory.Create(second).Returns(secondClients);
        var firstAnalytics = new AnalyticsDataService();
        var secondAnalytics = new AnalyticsDataService();
        firstClients.GetAnalyticsDataAsync(Arg.Any<CancellationToken>()).Returns(firstAnalytics);
        secondClients.GetAnalyticsDataAsync(Arg.Any<CancellationToken>()).Returns(secondAnalytics);
        var source = new SwitchableSource(Usable(first));
        var clients = new GoogleClients(source, factory);

        Assert.Same(firstAnalytics, await clients.GetAnalyticsDataAsync(CancellationToken.None));

        source.Result = Usable(second);

        Assert.Same(secondAnalytics, await clients.GetAnalyticsDataAsync(CancellationToken.None));
        factory.Received(1).Create(first);
        factory.Received(1).Create(second);
    }

    [Fact]
    public async Task LosingTheKey_StopsTheCallsEvenWhenServicesWereAlreadyBuilt()
    {
        var credential = await NewCredentialAsync();
        var source = new SwitchableSource(Usable(credential));
        var clients = new GoogleClients(source, new GoogleClientFactory());
        await clients.GetAnalyticsDataAsync(CancellationToken.None);

        source.Result = Failed(KeyErrorCodes.NotConfigured);

        var error = await Assert.ThrowsAsync<ReportFetchException>(() => clients.GetAnalyticsDataAsync(CancellationToken.None));
        Assert.Equal(KeyErrorCodes.NotConfigured, error.Code);
    }

    [Fact]
    public async Task TheDefaultFactory_BuildsEachServiceOnceForTheCredentialAndNamesTheApplication()
    {
        var credential = await NewCredentialAsync();

        var set = new GoogleClientFactory().Create(credential);
        var analytics = await set.GetAnalyticsDataAsync(CancellationToken.None);
        var searchConsole = await set.GetSearchConsoleAsync(CancellationToken.None);

        Assert.Same(analytics, await set.GetAnalyticsDataAsync(CancellationToken.None));
        Assert.Same(searchConsole, await set.GetSearchConsoleAsync(CancellationToken.None));
        Assert.Equal("Esatto.Umbraco.Backoffice.SeoReport", analytics.ApplicationName);
        Assert.Equal("Esatto.Umbraco.Backoffice.SeoReport", searchConsole.ApplicationName);
        Assert.NotSame(analytics, await new GoogleClientFactory().Create(credential).GetAnalyticsDataAsync(CancellationToken.None));
    }

    [Fact]
    public async Task TheDefaultClients_OnTheDefaultFactory_ReuseTheServicesWhileTheCredentialStays()
    {
        var credential = await NewCredentialAsync();
        var clients = new GoogleClients(new SwitchableSource(Usable(credential)), new GoogleClientFactory());

        var first = await clients.GetAnalyticsDataAsync(CancellationToken.None);

        Assert.Same(first, await clients.GetAnalyticsDataAsync(CancellationToken.None));
    }

    // ---- Over the real credential source -----------------------------------------------------------------------
    //
    // The source rebuilds its result every minute. The services must survive that, and be replaced only when the key really
    // changes: otherwise a long-running site would build a fresh pair (and fetch a fresh token) every minute of use.

    /// <summary>The default factory, counting how many pairs of services it was asked for and for which credential.</summary>
    private sealed class CountingFactory : IGoogleClientFactory
    {
        public List<GoogleCredential> Created { get; } = [];

        public IGoogleClients Create(GoogleCredential credential)
        {
            Created.Add(credential);
            return new GoogleClientFactory().Create(credential);
        }
    }

    private static string Key(string email = FakeServiceAccount.DefaultClientEmail) => FakeServiceAccount.Json(email);

    [Fact]
    public async Task OverTheRealSource_TheServicesSurviveTheSourcesCacheExpiring()
    {
        var h = new SettingsHarness();
        await h.SaveKeyAsync(Key());
        var factory = new CountingFactory();
        var clients = new GoogleClients(h.Credentials, factory);

        var first = await clients.GetAnalyticsDataAsync(CancellationToken.None);
        h.Time.Advance(TimeSpan.FromMinutes(2));
        var second = await clients.GetAnalyticsDataAsync(CancellationToken.None);
        h.Time.Advance(TimeSpan.FromMinutes(2));
        var third = await clients.GetSearchConsoleAsync(CancellationToken.None);
        var fourth = await clients.GetSearchConsoleAsync(CancellationToken.None);

        Assert.Same(first, second);
        Assert.Same(third, fourth);
        Assert.Single(factory.Created);
    }

    [Fact]
    public async Task OverTheRealSource_ASavedDifferentKeyBuildsNewServices()
    {
        var h = new SettingsHarness();
        await h.SaveKeyAsync(Key("one@proj.iam.gserviceaccount.com"));
        var factory = new CountingFactory();
        var clients = new GoogleClients(h.Credentials, factory);
        var first = await clients.GetAnalyticsDataAsync(CancellationToken.None);

        await h.SaveKeyAsync(Key("two@proj.iam.gserviceaccount.com"));
        var second = await clients.GetAnalyticsDataAsync(CancellationToken.None);
        h.Time.Advance(TimeSpan.FromMinutes(2));
        var third = await clients.GetAnalyticsDataAsync(CancellationToken.None);

        Assert.NotSame(first, second);
        Assert.Same(second, third);
        Assert.Equal(2, factory.Created.Count);
    }

    /// <summary>
    /// Saving the same key again (the dashboard's Save with the key box untouched or pasted again) is a new ciphertext and a new
    /// generation of the settings, so the source resolves again; the key is the same, so the services stay.
    /// </summary>
    [Fact]
    public async Task OverTheRealSource_SavingTheSameKeyAgainKeepsTheServices()
    {
        var h = new SettingsHarness();
        await h.SaveKeyAsync(Key());
        var factory = new CountingFactory();
        var clients = new GoogleClients(h.Credentials, factory);
        var first = await clients.GetAnalyticsDataAsync(CancellationToken.None);

        await h.SaveKeyAsync(Key());
        var second = await clients.GetAnalyticsDataAsync(CancellationToken.None);

        Assert.Same(first, second);
        Assert.Single(factory.Created);
    }

    [Fact]
    public async Task OverTheRealSource_ARemovedKeyIsNotServedFromTheCacheAndNeitherBuildsNorKeepsAnything()
    {
        var h = new SettingsHarness();
        await h.SaveKeyAsync(Key());
        var factory = new CountingFactory();
        var clients = new GoogleClients(h.Credentials, factory);
        var first = await clients.GetAnalyticsDataAsync(CancellationToken.None);

        await h.Settings.SaveAsync(new SeoReportSettings(), null, true);
        var failure = await Assert.ThrowsAsync<ReportFetchException>(() => clients.GetAnalyticsDataAsync(CancellationToken.None));
        h.Time.Advance(TimeSpan.FromMinutes(2));
        await Assert.ThrowsAsync<ReportFetchException>(() => clients.GetAnalyticsDataAsync(CancellationToken.None));
        Assert.Single(factory.Created);

        await h.SaveKeyAsync(Key());
        var again = await clients.GetAnalyticsDataAsync(CancellationToken.None);

        Assert.Equal(KeyErrorCodes.NotConfigured, failure.Code);
        Assert.NotSame(first, again);
        Assert.Equal(2, factory.Created.Count);
    }
}
