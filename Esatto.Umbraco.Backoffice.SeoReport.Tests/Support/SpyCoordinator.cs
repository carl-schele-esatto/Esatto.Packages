using Esatto.Umbraco.Backoffice.SeoReport.Jobs;

namespace Esatto.Umbraco.Backoffice.SeoReport.Tests.Support;

/// <summary>
/// An <see cref="IFetchCoordinator"/> that starts nothing and remembers every start it was asked for (with the <c>startedBy</c> it
/// was given), so a test can prove that a request did, or never did, ask for a backfill, and how many times. What it answers is
/// whatever the test set; <see cref="OnStart"/> lets a test make a start fail.
/// </summary>
internal sealed class SpyCoordinator : IFetchCoordinator
{
    private readonly object _gate = new();
    private readonly List<string> _starts = [];

    /// <summary>What <see cref="TryStartAsync"/> answers.</summary>
    public FetchStartResult Result { get; set; } = FetchStartResult.Started;

    /// <summary>What <see cref="Progress"/> reports.</summary>
    public FetchProgress? Progress { get; set; }

    /// <summary>When set, a start throws this (a store that does not answer).</summary>
    public Exception? OnStart { get; set; }

    /// <summary>The <c>startedBy</c> of every start asked for, in order.</summary>
    public IReadOnlyList<string> Starts
    {
        get { lock (_gate) return _starts.ToList(); }
    }

    public Task<FetchStartResult> TryStartAsync(string startedBy)
    {
        lock (_gate) _starts.Add(startedBy);

        return OnStart is { } failure ? Task.FromException<FetchStartResult>(failure) : Task.FromResult(Result);
    }
}
