namespace Esatto.Umbraco.Backoffice.SeoReport.Jobs;

/// <summary>
/// What the API needs of <see cref="FetchCoordinator"/>: to start a backfill and to read how far it is. The coordinator is a sealed
/// class with its own gates and database lock, so a test could not stand in for it; the controller asks for this interface so that
/// "opening the dashboard never starts a fetch" and "saving settings that work starts exactly one" can be proven by counting the
/// calls. The container hands out the one coordinator for both (the job uses the class itself).
/// </summary>
public interface IFetchCoordinator
{
    /// <inheritdoc cref="FetchCoordinator.Progress"/>
    FetchProgress? Progress { get; }

    /// <inheritdoc cref="FetchCoordinator.TryStartAsync"/>
    Task<FetchStartResult> TryStartAsync(string startedBy);
}
