using Esatto.Umbraco.Backoffice.Seo.Evaluation;

namespace Esatto.Umbraco.Backoffice.Seo.Checks;

// Page content: usually fixed by editors. Thresholds are the spec's (2026-11-09-seo-design.md, Checks).

public sealed class TitleMissing() : PageCheck(CheckIds.TitleMissing, Severity.Critical, Owner.Editor)
{
    protected override (bool, string?) Test(PageContext p, ScanSnapshot s) => p.Facts!.Title is null ? Fail(null) : Pass;
}

public sealed class TitleLength() : PageCheck(CheckIds.TitleLength, Severity.Warning, Owner.Editor)
{
    public const int Min = 30, Max = 60;

    protected override bool AppliesTo(PageContext p, ScanSnapshot s) => base.AppliesTo(p, s) && p.Facts!.Title is not null;

    protected override (bool, string?) Test(PageContext p, ScanSnapshot s) => LengthCheck.Test(p.Facts!.Title!, Min, Max);
}

public sealed class DescriptionMissing() : PageCheck(CheckIds.DescriptionMissing, Severity.Warning, Owner.Editor)
{
    protected override (bool, string?) Test(PageContext p, ScanSnapshot s) => p.Facts!.MetaDescription is null ? Fail(null) : Pass;
}

public sealed class DescriptionLength() : PageCheck(CheckIds.DescriptionLength, Severity.Notice, Owner.Editor)
{
    public const int Min = 70, Max = 160;

    protected override bool AppliesTo(PageContext p, ScanSnapshot s) => base.AppliesTo(p, s) && p.Facts!.MetaDescription is not null;

    protected override (bool, string?) Test(PageContext p, ScanSnapshot s) => LengthCheck.Test(p.Facts!.MetaDescription!, Min, Max);
}

internal static class LengthCheck
{
    public static (bool, string?) Test(string value, int min, int max)
        => value.Length >= min && value.Length <= max ? (true, null) : (false, $"{value.Length} characters: \"{value}\"");
}

public sealed class H1Missing() : PageCheck(CheckIds.H1Missing, Severity.Warning, Owner.Editor)
{
    protected override (bool, string?) Test(PageContext p, ScanSnapshot s) => p.Facts!.H1s.Any(h => h.Length > 0) ? Pass : Fail(null);
}

public sealed class H1Multiple() : PageCheck(CheckIds.H1Multiple, Severity.Notice, Owner.Editor)
{
    protected override bool AppliesTo(PageContext p, ScanSnapshot s) => base.AppliesTo(p, s) && p.Facts!.H1s.Any(h => h.Length > 0);

    protected override (bool, string?) Test(PageContext p, ScanSnapshot s)
    {
        var headings = p.Facts!.H1s.Where(h => h.Length > 0).ToList();
        return headings.Count > 1 ? Fail(string.Join("\n", headings)) : Pass;
    }
}

public sealed class ThinContent() : PageCheck(CheckIds.ThinContent, Severity.Notice, Owner.Editor)
{
    public const int MinWords = 200;

    protected override (bool, string?) Test(PageContext p, ScanSnapshot s)
        => p.Facts!.WordCount >= MinWords ? Pass : Fail($"{p.Facts.WordCount} words");
}

public sealed class OgMissing() : PageCheck(CheckIds.OgMissing, Severity.Notice, Owner.Editor)
{
    protected override (bool, string?) Test(PageContext p, ScanSnapshot s)
    {
        var missing = new[] { p.Facts!.OgTitle is null ? "og:title" : null, p.Facts.OgImage is null ? "og:image" : null }.OfType<string>().ToList();
        return missing.Count == 0 ? Pass : Fail(string.Join(", ", missing) + " missing");
    }
}

/// <summary>Shared by the two link checks: every loaded page (hidden ones too — their links are still followed).</summary>
public abstract class LinkCheck(string id, Severity severity, Func<LinkStatus, bool> bad) : PageCheck(id, severity, Owner.Editor)
{
    public const int MaxListed = 10;

    protected override bool AppliesTo(PageContext p, ScanSnapshot s) => p.Fetched;

    protected override (bool, string?) Test(PageContext p, ScanSnapshot s)
    {
        var offending = p.Facts!.InternalLinks.Where(l => s.Links.TryGetValue(l, out var st) && bad(st)).ToList();
        return offending.Count == 0 ? Pass : Fail(string.Join("\n", offending.Take(MaxListed)));
    }
}

public sealed class BrokenInternalLink() : LinkCheck(CheckIds.BrokenInternalLink, Severity.Warning, l => l.Broken);

public sealed class RedirectedInternalLink() : LinkCheck(CheckIds.RedirectedInternalLink, Severity.Notice, l => l.Redirected);

public sealed class TranslationMissing() : PageCheck(CheckIds.TranslationMissing, Severity.Notice, Owner.Editor)
{
    protected override bool AppliesTo(PageContext p, ScanSnapshot s) => base.AppliesTo(p, s) && p.Page.IsVariant;

    protected override (bool, string?) Test(PageContext p, ScanSnapshot s)
    {
        var has = p.Page.OtherCultures.Append(p.Page.Culture).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = s.SiteCultures.Where(c => !has.Contains(c)).ToList();
        return missing.Count == 0 ? Pass : Fail(string.Join(", ", missing));
    }
}
