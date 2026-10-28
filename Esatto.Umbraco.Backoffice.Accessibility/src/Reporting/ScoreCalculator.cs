using Esatto.Umbraco.Backoffice.Accessibility.Knowledge;

namespace Esatto.Umbraco.Backoffice.Accessibility.Reporting;

public enum CriterionStatus { Pass, Fail, NotApplicable, NotVerified }

/// <summary>
/// What is known about one criterion: open automated violations, whether axe saw a passing rule for it,
/// and the manual result (<c>pass</c>, <c>fail</c>, <c>na</c> or null).
/// </summary>
public sealed record CriterionInput(Criterion Criterion, int OpenViolations, bool AutomatedPassSeen, string? Manual);

/// <summary>
/// <see cref="Percent"/> = Pass / (all − not applicable), whole percent, null when nothing applies.
/// <see cref="AutomatedPass"/>/<see cref="AutomatedFail"/> count criteria axe passed without violations / found violations for.
/// <see cref="ScannerChecked"/> is the part of <see cref="NotVerified"/> that axe checked and found no violations for, so
/// a person only has to confirm it; the rest of <see cref="NotVerified"/> only a person can check.
/// </summary>
public sealed record ScoreResult(int? Percent, int Pass, int Fail, int NotApplicable, int NotVerified, int AutomatedPass, int AutomatedFail, int ScannerChecked)
{
    public static ScoreResult Empty => new(null, 0, 0, 0, 0, 0, 0, 0);
}

/// <summary>
/// The honest score from the spec: a criterion passes only when a person confirmed it, or when it is one
/// of the very few that axe can fully check. A clean automated scan alone therefore never reads 100 %.
/// </summary>
public static class ScoreCalculator
{
    public static CriterionStatus StatusOf(CriterionInput i)
    {
        if (i.OpenViolations > 0 || i.Manual == ManualStatus.Fail) return CriterionStatus.Fail;
        if (i.Manual == ManualStatus.NotApplicable) return CriterionStatus.NotApplicable;
        if (i.Manual == ManualStatus.Pass) return CriterionStatus.Pass;
        if (i.Criterion.FullyAutomatable && i.AutomatedPassSeen) return CriterionStatus.Pass;
        return CriterionStatus.NotVerified;
    }

    public static ScoreResult Compute(IEnumerable<CriterionInput> inputs)
    {
        var list = inputs.ToList();
        var statuses = list.Select(StatusOf).ToList();
        int Count(CriterionStatus s) => statuses.Count(x => x == s);

        var pass = Count(CriterionStatus.Pass);
        var na = Count(CriterionStatus.NotApplicable);
        var applicable = list.Count - na;
        int? percent = applicable == 0 ? null : (int)Math.Round(100m * pass / applicable, MidpointRounding.AwayFromZero);

        return new ScoreResult(
            percent,
            pass,
            Count(CriterionStatus.Fail),
            na,
            Count(CriterionStatus.NotVerified),
            list.Count(i => i.AutomatedPassSeen && i.OpenViolations == 0),
            list.Count(i => i.OpenViolations > 0),
            list.Where((i, n) => statuses[n] == CriterionStatus.NotVerified).Count(i => i.AutomatedPassSeen));
    }
}

public static class ManualStatus
{
    public const string Pass = "pass";
    public const string Fail = "fail";
    public const string NotApplicable = "na";

    public static bool IsValid(string? status) => status is Pass or Fail or NotApplicable;
}
