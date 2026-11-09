using Esatto.Umbraco.Backoffice.Seo.Evaluation;
using Esatto.Umbraco.Backoffice.Seo.Facts;

namespace Esatto.Umbraco.Backoffice.Seo.Checks;

// Templates and setup: usually fixed by developers.

public sealed class StatusError() : PageCheck(CheckIds.StatusError, Severity.Critical, Owner.Developer)
{
    protected override bool AppliesTo(PageContext p, ScanSnapshot s) => true;

    protected override (bool, string?) Test(PageContext p, ScanSnapshot s)
    {
        if (p.Fetched) return Pass;
        var f = p.Facts!;
        var what = Describe(f);
        var chain = f.RedirectChain.Count > 0 ? "\nRedirects: " + string.Join(" → ", f.RedirectChain.Append(f.FinalUrl)) : string.Empty;
        return Fail(what + chain);
    }

    /// <summary>Why a page couldn't be judged, in one phrase. Also the page row's reason, so both say the same.</summary>
    public static string Describe(PageFacts f) => f.Error ?? (f.StatusCode == 200 ? "Not an HTML page" : $"HTTP {f.StatusCode}");
}

public sealed class HiddenButBlocked() : PageCheck(CheckIds.HiddenButBlocked, Severity.Notice, Owner.Developer)
{
    protected override bool AppliesTo(PageContext p, ScanSnapshot s) => p.Fetched;

    protected override (bool, string?) Test(PageContext p, ScanSnapshot s)
        => p.Facts!.Noindex && p.BlockedByRobots ? Fail("noindex + robots.txt Disallow") : Pass;
}

public sealed class CanonicalMissing() : PageCheck(CheckIds.CanonicalMissing, Severity.Notice, Owner.Developer)
{
    protected override (bool, string?) Test(PageContext p, ScanSnapshot s) => p.Facts!.Canonical is null ? Fail(null) : Pass;
}

public sealed class CanonicalMismatch() : PageCheck(CheckIds.CanonicalMismatch, Severity.Warning, Owner.Developer)
{
    protected override bool AppliesTo(PageContext p, ScanSnapshot s) => base.AppliesTo(p, s) && p.Facts!.Canonical is not null;

    protected override (bool, string?) Test(PageContext p, ScanSnapshot s)
    {
        var canonical = p.Facts!.Canonical!;
        var self = UrlNormalizer.Normalize(p.Facts.FinalUrl.Length > 0 ? p.Facts.FinalUrl : p.Page.Url);
        if (UrlNormalizer.Normalize(canonical) == self) return Pass;

        var target = s.FindPage(canonical);
        var why = target switch
        {
            null => "points to another address",
            { Fetched: false } => "points to a page that doesn't load",
            { Hidden: true } => "points to a page hidden from search engines",
            _ => "points to another page",
        };
        return Fail($"{canonical} ({why})");
    }
}

public sealed class HreflangMissing() : PageCheck(CheckIds.HreflangMissing, Severity.Warning, Owner.Developer)
{
    protected override bool AppliesTo(PageContext p, ScanSnapshot s) => base.AppliesTo(p, s) && p.Page.OtherCultures.Count > 0;

    protected override (bool, string?) Test(PageContext p, ScanSnapshot s)
        => p.Facts!.Hreflangs.Count == 0 ? Fail("Also published in: " + string.Join(", ", p.Page.OtherCultures)) : Pass;
}

public sealed class HreflangNoXDefault() : PageCheck(CheckIds.HreflangNoXDefault, Severity.Notice, Owner.Developer)
{
    protected override bool AppliesTo(PageContext p, ScanSnapshot s) => base.AppliesTo(p, s) && p.Facts!.Hreflangs.Count > 0;

    protected override (bool, string?) Test(PageContext p, ScanSnapshot s)
        => p.Facts!.Hreflangs.Any(h => h.Lang.Equals("x-default", StringComparison.OrdinalIgnoreCase)) ? Pass : Fail(null);
}

public sealed class LangMismatch() : PageCheck(CheckIds.LangMismatch, Severity.Warning, Owner.Developer)
{
    protected override (bool, string?) Test(PageContext p, ScanSnapshot s)
    {
        var lang = p.Facts!.HtmlLang;
        if (lang is null) return Fail("lang attribute missing");
        if (p.Page.Culture.Length == 0) return Pass; // invariant content: only presence can be judged
        return Primary(lang) == Primary(p.Page.Culture) ? Pass : Fail($"lang=\"{lang}\", page language {p.Page.Culture}");
    }

    private static string Primary(string tag) => tag.Split('-', '_')[0].Trim().ToLowerInvariant();
}

public sealed class StructuredDataInvalid() : PageCheck(CheckIds.StructuredDataInvalid, Severity.Warning, Owner.Developer)
{
    protected override bool AppliesTo(PageContext p, ScanSnapshot s) => base.AppliesTo(p, s) && p.Facts!.JsonLdTotal > 0;

    protected override (bool, string?) Test(PageContext p, ScanSnapshot s)
        => p.Facts!.JsonLdInvalid > 0 ? Fail($"{p.Facts.JsonLdInvalid} of {p.Facts.JsonLdTotal} JSON-LD blocks") : Pass;
}

public sealed class SlowResponse() : PageCheck(CheckIds.SlowResponse, Severity.Notice, Owner.Developer)
{
    public const long MaxMs = 1500;

    protected override (bool, string?) Test(PageContext p, ScanSnapshot s)
        => p.Facts!.ElapsedMs > MaxMs ? Fail($"{p.Facts.ElapsedMs} ms") : Pass;
}

public sealed class HeavyHtml() : PageCheck(CheckIds.HeavyHtml, Severity.Notice, Owner.Developer)
{
    public const long MaxBytes = 512_000;

    protected override (bool, string?) Test(PageContext p, ScanSnapshot s)
        => p.Facts!.Bytes > MaxBytes ? Fail($"{p.Facts.Bytes / 1024} kB") : Pass;
}
