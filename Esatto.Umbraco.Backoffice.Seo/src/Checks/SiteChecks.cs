using Esatto.Umbraco.Backoffice.Seo.Evaluation;

namespace Esatto.Umbraco.Backoffice.Seo.Checks;

// Whole-site checks: one outcome (or none when not applicable), shown as a checklist on the Overview.

public abstract class SiteCheck(string id, Severity severity) : ICheck
{
    public CheckInfo Info { get; } = new(id, severity, Owner.Developer, CheckScope.Site);

    /// <summary>Null = not applicable. Otherwise passed + a value for the failure.</summary>
    protected abstract (bool Passed, string? Value)? Test(ScanSnapshot s);

    public IEnumerable<Outcome> Evaluate(ScanSnapshot s)
    {
        if (!s.Pages.Any(p => p.Fetched)) yield break; // nothing loaded: status-error says it all
        if (Test(s) is { } result) yield return Outcome.ForSite(Info.Id, result.Passed, result.Value);
    }
}

public sealed class RobotsMissing() : SiteCheck(CheckIds.RobotsMissing, Severity.Notice)
{
    protected override (bool, string?)? Test(ScanSnapshot s)
    {
        if (s.Probe.RobotsFound.Count == 0) return null;
        var missing = s.Probe.RobotsFound.Where(kv => !kv.Value).Select(kv => kv.Key).ToList();
        return (missing.Count == 0, string.Join(", ", missing));
    }
}

public sealed class SitemapMissing() : SiteCheck(CheckIds.SitemapMissing, Severity.Warning)
{
    protected override (bool, string?)? Test(ScanSnapshot s) => (s.Probe.SitemapFound, null);
}

public sealed class HttpsRedirect() : SiteCheck(CheckIds.HttpsRedirect, Severity.Critical)
{
    protected override (bool, string?)? Test(ScanSnapshot s) => s.Probe.HttpsRedirects is { } ok ? (ok, null) : null;
}

public sealed class HostDuplicate() : SiteCheck(CheckIds.HostDuplicate, Severity.Warning)
{
    protected override (bool, string?)? Test(ScanSnapshot s) => s.Probe.HostDuplicate is { } duplicate ? (!duplicate, null) : null;
}

/// <summary>The start page is hidden. Replaced by <see cref="SiteHidden"/> when the whole site is.</summary>
public sealed class HomeHidden() : SiteCheck(CheckIds.HomeHidden, Severity.Critical)
{
    protected override (bool, string?)? Test(ScanSnapshot s)
    {
        if (s.SiteHidden) return null;
        var roots = s.Pages.Where(p => p.Page.IsRoot && p.Fetched).ToList();
        if (roots.Count == 0) return null;
        var hidden = roots.Where(r => r.Hidden).ToList();
        return (hidden.Count == 0, string.Join("\n", hidden.Select(r => $"{r.Page.Url} ({Reason(r)})")));
    }

    internal static string Reason(PageContext p) => p.Indexability switch
    {
        Indexability.Excluded when p.Facts!.HeaderNoindex => "X-Robots-Tag noindex",
        Indexability.Excluded => "robots meta noindex",
        _ => "blocked by robots.txt",
    };
}

/// <summary>Every page hidden: expected on Test/Dev, a disaster on Live. Off when configured as intentional.</summary>
public sealed class SiteHidden() : SiteCheck(CheckIds.SiteHidden, Severity.Critical)
{
    protected override (bool, string?)? Test(ScanSnapshot s) => s.IntentionallyHidden ? null : (!s.SiteHidden, null);
}
