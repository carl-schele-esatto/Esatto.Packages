using Microsoft.Extensions.Options;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentThrottleTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Injectable clock, so a window-expiry test advances time instead of sleeping.</summary>
    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static (ConsentThrottle Throttle, MutableTimeProvider Clock) Build(int? requestsPerMinute = null)
    {
        var settings = new CookieBannerOptions();
        if (requestsPerMinute is not null)
        {
            settings.ThrottleRequestsPerMinute = requestsPerMinute.Value;
        }

        var clock = new MutableTimeProvider(Start);
        return (new ConsentThrottle(Options.Create(settings), clock), clock);
    }

    [Fact]
    public void Allows_the_configured_number_of_requests_within_one_window()
    {
        // Pins the contract inherited from the ASP.NET Core rate limiter this replaces:
        // 10 requests per minute per client, taken from the option default.
        (ConsentThrottle throttle, _) = Build();

        for (var i = 1; i <= 10; i++)
        {
            Assert.True(throttle.TryAcquire("198.51.100.4"), $"request {i} should be allowed");
        }
    }

    [Fact]
    public void The_request_after_the_limit_is_refused()
    {
        // QueueLimit was 0 on the old fixed-window limiter: the overflow request is rejected
        // outright, never queued, so the endpoint can answer 429 immediately.
        (ConsentThrottle throttle, _) = Build(requestsPerMinute: 3);

        Assert.True(throttle.TryAcquire("198.51.100.4"));
        Assert.True(throttle.TryAcquire("198.51.100.4"));
        Assert.True(throttle.TryAcquire("198.51.100.4"));
        Assert.False(throttle.TryAcquire("198.51.100.4"));
    }

    [Fact]
    public void Each_client_key_has_its_own_budget()
    {
        // The old limiter partitioned by remote IP. One noisy visitor must not lock out the site.
        (ConsentThrottle throttle, _) = Build(requestsPerMinute: 1);

        Assert.True(throttle.TryAcquire("198.51.100.4"));
        Assert.False(throttle.TryAcquire("198.51.100.4"));
        Assert.True(throttle.TryAcquire("203.0.113.9"));
    }

    [Fact]
    public void The_budget_refreshes_once_the_window_has_passed()
    {
        // Pins that the window slides rather than being a one-shot budget: a visitor blocked at
        // 12:00 can save their choice a minute later.
        (ConsentThrottle throttle, MutableTimeProvider clock) = Build(requestsPerMinute: 1);

        Assert.True(throttle.TryAcquire("198.51.100.4"));
        Assert.False(throttle.TryAcquire("198.51.100.4"));

        clock.Now = Start.AddSeconds(61);

        Assert.True(throttle.TryAcquire("198.51.100.4"));
    }

    [Fact]
    public void A_non_positive_limit_disables_throttling()
    {
        // ThrottleRequestsPerMinute = 0 is the documented off-switch. Without this guard a
        // misconfigured site would answer 429 to every consent POST and pin the banner open.
        (ConsentThrottle throttle, _) = Build(requestsPerMinute: 0);

        for (var i = 0; i < 50; i++)
        {
            Assert.True(throttle.TryAcquire("198.51.100.4"));
        }
    }
}
