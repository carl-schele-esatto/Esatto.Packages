using System.Text.Json;
using Esatto.Umbraco.Backoffice.Seo.Checks;
using Esatto.Umbraco.Backoffice.Seo.Evaluation;
using Esatto.Umbraco.Backoffice.Seo.Facts;
using Esatto.Umbraco.Backoffice.Seo.Knowledge;
using Esatto.Umbraco.Backoffice.Seo.Persistence;
using Esatto.Umbraco.Backoffice.Seo.Settings;

namespace Esatto.Umbraco.Backoffice.Seo.Scanning;

/// <summary>Rebuilds a <see cref="ScanSnapshot"/> from stored rows; shared by the runner, re-checks and reports.</summary>
public sealed class SnapshotLoader(ISeoStore store, SeoEnvironment environment)
{
    public async Task<ScanSnapshot?> LoadAsync(int scanId)
    {
        var scan = await store.GetScanAsync(scanId);
        return scan is null ? null : Build(scan, await store.GetPagesAsync(scanId), environment.IntentionallyHidden);
    }

    public static ScanSnapshot Build(ScanEntity scan, IReadOnlyList<PageEntity> pages, bool intentionallyHidden)
        => ScanSnapshot.Build(
            scan.SiteKey,
            Split(scan.Cultures),
            pages.Where(p => p.Status != PageStatus.Skipped && p.FactsJson is not null).Select(p => (ToRef(p), Read<PageFacts>(p.FactsJson))),
            Read<SiteProbe>(scan.ProbeJson) ?? SiteProbe.None,
            Read<Dictionary<string, LinkStatus>>(scan.LinksJson) ?? [],
            intentionallyHidden);

    public static PageRef ToRef(PageEntity p) => new(p.ContentKey, p.Name, p.Culture, p.Url, p.IsRoot, p.IsVariant, Split(p.OtherCultures));

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, KnowledgeBase.Json);

    public static T? Read<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(json, KnowledgeBase.Json);
        }
        catch (JsonException)
        {
            return null; // a row written by a different package version must not break the dashboard
        }
    }

    public static IReadOnlyList<string> Split(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>Writes an evaluation's headline numbers onto the scan row.</summary>
public static class ScanResults
{
    public static Evaluation.Evaluation Apply(ScanEntity scan, ScanSnapshot snapshot, IReadOnlyList<IgnoreEntity> ignores)
    {
        var evaluation = Evaluator.Evaluate(snapshot, CheckCatalog.All, ToRules(ignores));
        scan.Score = ScoreCalculator.Calculate(snapshot, evaluation, CheckCatalog.Info);
        scan.ScoreVersion = ScoreCalculator.Version;
        var failures = evaluation.Failures.Select(f => CheckCatalog.Info[f.CheckId].Severity).ToList();
        scan.Critical = failures.Count(s => s == Severity.Critical);
        scan.Warning = failures.Count(s => s == Severity.Warning);
        scan.Notice = failures.Count(s => s == Severity.Notice);
        return evaluation;
    }

    public static IReadOnlyList<IgnoreRule> ToRules(IEnumerable<IgnoreEntity> ignores)
        => ignores.Select(i => new IgnoreRule(i.Id, i.CheckId, i.ContentKey, i.Culture)).ToList();
}

/// <summary>Hosts whose links count as internal: every page host and its www/bare twin.</summary>
public static class ScanHosts
{
    public static IReadOnlySet<string> From(IEnumerable<string> urls)
    {
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var url in urls)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) continue;
            hosts.Add(u.Host);
            hosts.Add(u.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? u.Host[4..] : "www." + u.Host);
        }

        return hosts;
    }
}
