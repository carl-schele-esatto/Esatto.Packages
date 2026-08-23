using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>Per-client request budget for the consent endpoint.</summary>
internal interface IConsentThrottle
{
    /// <summary>
    /// Consumes one request from <paramref name="clientKey"/>'s budget. False means the caller
    /// must answer HTTP 429 — nothing is queued.
    /// </summary>
    bool TryAcquire(string clientKey);
}

/// <summary>
/// In-memory sliding window, one budget per client key.
/// </summary>
/// <remarks>
/// Replaces ASP.NET Core rate limiting deliberately. The framework limiter forces a consumer to
/// place <c>UseRateLimiter()</c> between <c>UseUmbraco().WithMiddleware(...)</c> and
/// <c>.WithEndpoints(...)</c> — anyone copying a conventional Umbraco <c>Program.cs</c> gets that
/// wrong, and a missing named policy throws at request time. Owning the window here keeps the
/// package to a single <c>UseCookieConsent()</c> line while preserving the previous contract:
/// 10 requests per minute per remote IP, overflow rejected rather than queued.
/// Registered as a singleton, so <see cref="TryAcquire"/> must be thread-safe.
/// </remarks>
internal sealed class ConsentThrottle : IConsentThrottle
{
    /// <summary>
    /// The window the option is expressed in (<c>ThrottleRequestsPerMinute</c>), so only the
    /// permit count is configurable.
    /// </summary>
    private static readonly TimeSpan WindowLength = TimeSpan.FromMinutes(1);

    private readonly IOptions<CookieBannerOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, ClientWindow> _windows = new(StringComparer.Ordinal);
    private long _nextSweepTicks;

    public ConsentThrottle(IOptions<CookieBannerOptions> options, TimeProvider timeProvider)
    {
        _options = options;
        _timeProvider = timeProvider;
    }

    public bool TryAcquire(string clientKey)
    {
        var limit = _options.Value.ThrottleRequestsPerMinute;
        if (limit <= 0)
        {
            // Documented off-switch. Answering 429 to every POST would pin the banner open.
            return true;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        Sweep(now);

        return _windows
            .GetOrAdd(clientKey, static _ => new ClientWindow())
            .TryAcquire(now, WindowLength, limit);
    }

    /// <summary>
    /// Drops windows that have been idle for a full window, so a crawler cycling through
    /// addresses cannot grow the dictionary without bound. Runs at most once per window: a burst
    /// of requests must not turn into a burst of full-dictionary scans. A sweep racing an
    /// in-flight <see cref="TryAcquire"/> can at worst forget one just-recorded hit, which
    /// loosens the limit for a single request and never tightens it.
    /// </summary>
    private void Sweep(DateTimeOffset now)
    {
        var nowTicks = now.UtcTicks;
        var due = Interlocked.Read(ref _nextSweepTicks);
        if (nowTicks < due)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _nextSweepTicks, nowTicks + WindowLength.Ticks, due) != due)
        {
            return;
        }

        foreach (KeyValuePair<string, ClientWindow> pair in _windows)
        {
            if (pair.Value.IsIdle(now, WindowLength))
            {
                _windows.TryRemove(pair.Key, out _);
            }
        }
    }

    private sealed class ClientWindow
    {
        private readonly Queue<DateTimeOffset> _hits = new();
        private DateTimeOffset _lastHit;

        public bool TryAcquire(DateTimeOffset now, TimeSpan window, int limit)
        {
            lock (_hits)
            {
                while (_hits.Count > 0 && now - _hits.Peek() >= window)
                {
                    _hits.Dequeue();
                }

                if (_hits.Count >= limit)
                {
                    return false;
                }

                _hits.Enqueue(now);
                _lastHit = now;
                return true;
            }
        }

        public bool IsIdle(DateTimeOffset now, TimeSpan window)
        {
            lock (_hits)
            {
                return now - _lastHit >= window;
            }
        }
    }
}
