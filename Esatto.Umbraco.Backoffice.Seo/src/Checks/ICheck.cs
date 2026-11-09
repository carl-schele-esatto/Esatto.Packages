using Esatto.Umbraco.Backoffice.Seo.Evaluation;

namespace Esatto.Umbraco.Backoffice.Seo.Checks;

/// <summary>
/// A check: a pure function from a scan to pass/fail outcomes. To add one: a class here, an id in
/// <see cref="CheckIds"/>, a text in Knowledge/checks.en.json, an entry in <see cref="CheckCatalog"/>, and tests.
/// </summary>
public interface ICheck
{
    CheckInfo Info { get; }

    /// <summary>One outcome per page the check applies to (or one for the site). Not applicable = no outcome.</summary>
    IEnumerable<Outcome> Evaluate(ScanSnapshot snapshot);
}

/// <summary>A check judged one page at a time.</summary>
public abstract class PageCheck(string id, Severity severity, Owner owner, CheckScope scope = CheckScope.Page) : ICheck
{
    public CheckInfo Info { get; } = new(id, severity, owner, scope);

    /// <summary>Default: pages whose content is judged (loaded, and indexable or the whole site is hidden).</summary>
    protected virtual bool AppliesTo(PageContext p, ScanSnapshot s) => s.ContentChecksApply(p);

    /// <summary>Called only for applicable pages, whose <see cref="PageContext.Facts"/> is then never null.</summary>
    protected abstract (bool Passed, string? Value) Test(PageContext p, ScanSnapshot s);

    public IEnumerable<Outcome> Evaluate(ScanSnapshot snapshot)
    {
        foreach (var page in snapshot.Pages)
        {
            if (page.Facts is null || !AppliesTo(page, snapshot)) continue;
            var (passed, value) = Test(page, snapshot);
            yield return Outcome.For(Info.Id, page, passed, value);
        }
    }

    protected static (bool, string?) Pass => (true, null);

    protected static (bool, string?) Fail(string? value) => (false, value);
}
