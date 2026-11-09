using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Esatto.Umbraco.Backoffice.Seo.Scanning;

/// <summary>
/// Runs scans in the background, one at a time per site, and holds their live progress. Singleton: the
/// dashboard polls <see cref="GetProgress"/> while a scan runs. When the application stops, running scans
/// stop too and are recorded as incomplete.
/// </summary>
public sealed class ScanCoordinator(IServiceScopeFactory scopes, ILogger<ScanCoordinator> logger, IHostApplicationLifetime? lifetime = null)
{
    private readonly CancellationToken _stopping = lifetime?.ApplicationStopping ?? CancellationToken.None;

    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, Running> _running = [];

    /// <summary>Starts a scan unless one is already running for the site. Returns immediately.</summary>
    public bool TryStart(Guid siteKey, string trigger, string userName, Uri? requestBase)
    {
        var run = new Running();
        lock (_lock)
        {
            if (!_running.TryAdd(siteKey, run)) return false;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                using var either = CancellationTokenSource.CreateLinkedTokenSource(run.Cancel.Token, _stopping);
                await using var scope = scopes.CreateAsyncScope();
                var runner = scope.ServiceProvider.GetRequiredService<ScanRunner>();
                await runner.RunAsync(siteKey, trigger, userName, requestBase, run, either.Token, _stopping);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SEO Health scan of site {SiteKey} failed", siteKey);
            }
            finally
            {
                lock (_lock) _running.Remove(siteKey);
                run.Cancel.Dispose();
            }
        });
        return true;
    }

    public bool IsRunning(Guid siteKey)
    {
        lock (_lock) return _running.ContainsKey(siteKey);
    }

    public ScanProgress? GetProgress(Guid siteKey)
    {
        lock (_lock) return _running.TryGetValue(siteKey, out var run) ? run.Latest : null;
    }

    public bool Cancel(Guid siteKey)
    {
        Running? run;
        lock (_lock) _running.TryGetValue(siteKey, out run);
        if (run is null) return false;
        try
        {
            run.Cancel.Cancel();
        }
        catch (ObjectDisposedException)
        {
            return false; // finished in the meantime
        }

        return true;
    }

    /// <summary>A running scan: its cancellation and the latest progress (no SynchronizationContext, unlike Progress&lt;T&gt;).</summary>
    private sealed class Running : IProgress<ScanProgress>
    {
        private ScanProgress? _latest;

        public CancellationTokenSource Cancel { get; } = new();

        public ScanProgress? Latest => Volatile.Read(ref _latest);

        public void Report(ScanProgress value) => Volatile.Write(ref _latest, value);
    }
}
