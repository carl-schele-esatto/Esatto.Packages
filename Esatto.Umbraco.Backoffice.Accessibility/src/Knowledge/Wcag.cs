namespace Esatto.Umbraco.Backoffice.Accessibility.Knowledge;

/// <summary>A WCAG version. Each version contains every criterion of the previous one (except 4.1.1, removed in 2.2).</summary>
public enum WcagVersion { V20, V21, V22 }

/// <summary>A conformance level. Each level includes the levels below it.</summary>
public enum WcagLevel { A, AA, AAA }

/// <summary>Who usually fixes a failure: editors (content), developers (templates, CSS, JS) or either.</summary>
public enum Owner { Editor, Developer, Both }

/// <summary>The version × level a site is measured against, e.g. "2.2 AA".</summary>
public sealed record WcagTarget(WcagVersion Version, WcagLevel Level)
{
    public static WcagTarget Default => new(WcagVersion.V22, WcagLevel.AA);

    public bool Includes(Criterion criterion)
        => criterion.Since <= Version
           && criterion.Level <= Level
           && (criterion.ObsoleteIn is not { } obsolete || Version < obsolete);

    /// <summary>"2.2 · AA".</summary>
    public override string ToString() => $"{Display(Version)} · {Level}";

    public static string Display(WcagVersion version) => version switch
    {
        WcagVersion.V20 => "2.0",
        WcagVersion.V21 => "2.1",
        _ => "2.2",
    };
}
