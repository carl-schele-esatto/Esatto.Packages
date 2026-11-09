namespace Esatto.Umbraco.Backoffice.Seo.Persistence;

/// <summary>
/// Serialises the read-modify-write updates of a finished scan's row (a re-check, an ignore re-scoring it),
/// so one doesn't overwrite the other's changes with the row it read earlier. In-process only: on a
/// load-balanced site the backoffice normally runs on one server.
/// </summary>
public sealed class ScanRowLock
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Waits for the lock; dispose the result to release it.</summary>
    public async Task<IDisposable> AcquireAsync()
    {
        await _gate.WaitAsync();
        return new Releaser(_gate);
    }

    private sealed class Releaser(SemaphoreSlim gate) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) gate.Release();
        }
    }
}
