namespace Esatto.Umbraco.Backoffice.Accessibility.Scanning;

/// <summary>Which scans lose their pages and findings: all but the newest <c>keep</c> per site.</summary>
public static class Retention
{
    public const int Keep = 10;

    public static IReadOnlyList<int> ScansToPrune(IEnumerable<(int Id, DateTime Started)> scansOfSite, int keep = Keep)
        => scansOfSite
            .OrderByDescending(s => s.Started)
            .Skip(keep)
            .Select(s => s.Id)
            .ToList();
}
