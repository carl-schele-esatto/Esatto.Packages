using System.Net;
using Esatto.Umbraco.Backoffice.AICosts;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Esatto.Umbraco.Backoffice.AICosts.Tests;

// Parsing tests ported from Esatto.Web (EcbExchangeRateProviderTests); caching tests are new.
public class ExchangeRateTests
{
    private const string Feed = """
        <?xml version="1.0" encoding="UTF-8"?>
        <gesmes:Envelope xmlns:gesmes="http://www.gesmes.org/xml/2002-08-01" xmlns="http://www.ecb.int/vocabulary/2002-08-01/eurofxref">
          <gesmes:subject>Reference rates</gesmes:subject>
          <Cube>
            <Cube time="2026-10-09">
              <Cube currency="USD" rate="1.25"/>
              <Cube currency="SEK" rate="11.50"/>
            </Cube>
          </Cube>
        </gesmes:Envelope>
        """;

    [Fact]
    public void Parses_date_and_rates_with_eur_as_one()
    {
        var rates = EcbExchangeRateProvider.ParseEcbXml(Feed);
        Assert.Equal(new DateOnly(2026, 10, 9), rates.Date);
        Assert.Equal(1m, rates.PerBaseUnit["EUR"]);
        Assert.Equal(11.50m, rates.PerBaseUnit["SEK"]);
    }

    [Fact]
    public void Rebases_to_usd()
    {
        var usd = EcbExchangeRateProvider.Rebase(EcbExchangeRateProvider.ParseEcbXml(Feed), "USD")!;
        Assert.Equal(1m, usd.PerBaseUnit["USD"]);
        Assert.Equal(0.8m, usd.PerBaseUnit["EUR"]);
        Assert.Equal(9.2m, usd.PerBaseUnit["SEK"]);
    }

    [Fact]
    public void Rebase_returns_null_for_an_unquoted_base() =>
        Assert.Null(EcbExchangeRateProvider.Rebase(EcbExchangeRateProvider.ParseEcbXml(Feed), "XYZ"));

    [Fact]
    public void Rejects_a_feed_without_rates() =>
        Assert.Throws<FormatException>(() => EcbExchangeRateProvider.ParseEcbXml("<Envelope><Cube><Cube time=\"2026-10-09\"/></Cube></Envelope>"));

    private sealed class Handler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(respond());
        }
    }

    private static (EcbExchangeRateProvider Provider, Handler Handler, TestClock Clock) Provider(Func<HttpResponseMessage> respond)
    {
        var handler = new Handler(respond);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(EcbExchangeRateProvider.HttpClientName).Returns(_ => new HttpClient(handler, disposeHandler: false));
        var clock = new TestClock(new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc));
        return (new EcbExchangeRateProvider(factory, clock, NullLogger<EcbExchangeRateProvider>.Instance), handler, clock);
    }

    [Fact]
    public async Task Caches_rates_for_six_hours()
    {
        var (provider, handler, clock) = Provider(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Feed) });

        await provider.GetRatesAsync("USD", CancellationToken.None);
        clock.Advance(TimeSpan.FromHours(5));
        await provider.GetRatesAsync("USD", CancellationToken.None);
        Assert.Equal(1, handler.Calls);

        clock.Advance(TimeSpan.FromHours(2));
        await provider.GetRatesAsync("USD", CancellationToken.None);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task A_failure_returns_null_and_waits_fifteen_minutes()
    {
        var (provider, handler, clock) = Provider(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        Assert.Null(await provider.GetRatesAsync("USD", CancellationToken.None));
        clock.Advance(TimeSpan.FromMinutes(10));
        await provider.GetRatesAsync("USD", CancellationToken.None);
        Assert.Equal(1, handler.Calls);

        clock.Advance(TimeSpan.FromMinutes(6));
        await provider.GetRatesAsync("USD", CancellationToken.None);
        Assert.Equal(2, handler.Calls);
    }

    // Deferred minor (fixed): a page load the browser cancels must not be cached as an ECB outage,
    // or every user sees "rates unavailable" for 15 minutes.
    [Fact]
    public async Task A_cancelled_request_is_not_cached_as_a_failure()
    {
        using var pageLoad = new CancellationTokenSource();
        var calls = 0;
        // The browser aborts while the ECB request is in flight; the next load succeeds.
        var (provider, handler, _) = Provider(() =>
        {
            if (++calls > 1) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Feed) };
            pageLoad.Cancel();
            throw new TaskCanceledException();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetRatesAsync("USD", pageLoad.Token));

        Assert.NotNull(await provider.GetRatesAsync("USD", CancellationToken.None));
        Assert.Equal(2, handler.Calls);
    }
}
