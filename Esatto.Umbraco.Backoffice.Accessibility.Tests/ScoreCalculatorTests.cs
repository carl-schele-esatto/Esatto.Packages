using Esatto.Umbraco.Backoffice.Accessibility.Knowledge;
using Esatto.Umbraco.Backoffice.Accessibility.Reporting;
using Xunit;

namespace Esatto.Umbraco.Backoffice.Accessibility.Tests;

public class ScoreCalculatorTests
{
    private static readonly KnowledgeBase Kb = new();

    private static CriterionInput In(string id, int violations = 0, bool automatedPass = false, string? manual = null)
        => new(Kb.Find(id)!, violations, automatedPass, manual);

    [Fact]
    public void Open_violation_fails() => Assert.Equal(CriterionStatus.Fail, ScoreCalculator.StatusOf(In("1.1.1", violations: 1, manual: "pass")));

    [Fact]
    public void Manual_fail_fails_without_violations() => Assert.Equal(CriterionStatus.Fail, ScoreCalculator.StatusOf(In("2.1.1", manual: "fail")));

    [Fact]
    public void Manual_pass_passes() => Assert.Equal(CriterionStatus.Pass, ScoreCalculator.StatusOf(In("2.1.1", manual: "pass")));

    [Fact]
    public void Fully_automatable_passes_on_automated_pass() => Assert.Equal(CriterionStatus.Pass, ScoreCalculator.StatusOf(In("3.1.1", automatedPass: true)));

    [Fact]
    public void Partly_automatable_with_automated_pass_is_not_verified()
        => Assert.Equal(CriterionStatus.NotVerified, ScoreCalculator.StatusOf(In("1.1.1", automatedPass: true)));

    [Fact]
    public void Manual_na_is_not_applicable() => Assert.Equal(CriterionStatus.NotApplicable, ScoreCalculator.StatusOf(In("1.2.2", manual: "na")));

    [Fact]
    public void Manual_na_is_excluded_from_denominator()
    {
        var result = ScoreCalculator.Compute([In("2.1.1", manual: "pass"), In("1.2.2", manual: "na"), In("2.4.3")]);
        Assert.Equal(50, result.Percent);
        Assert.Equal((1, 0, 1, 1), (result.Pass, result.Fail, result.NotApplicable, result.NotVerified));
    }

    [Fact]
    public void Clean_scan_without_manual_review_is_not_full_score()
    {
        var target = new WcagTarget(WcagVersion.V22, WcagLevel.AA);
        var result = ScoreCalculator.Compute(Kb.For(target).Select(c => new CriterionInput(c, 0, true, null)));

        Assert.True(result.Percent < 100);
        Assert.Equal(2, result.Percent); // only 3.1.1 of 55 passes on automation alone
        Assert.Equal(55, result.AutomatedPass);
    }

    [Fact]
    public void Nothing_applicable_gives_no_percent() => Assert.Null(ScoreCalculator.Compute([In("1.2.2", manual: "na")]).Percent);

    [Fact]
    public void Automated_counts_only_criteria_axe_saw()
    {
        var result = ScoreCalculator.Compute([In("1.1.1", violations: 2), In("3.1.1", automatedPass: true), In("2.1.1")]);
        Assert.Equal((1, 1), (result.AutomatedPass, result.AutomatedFail));
    }

    [Fact]
    public void Not_verified_counts_the_criteria_the_scanner_checked_without_violations()
    {
        var result = ScoreCalculator.Compute([
            In("1.1.1", automatedPass: true),                 // scanner checked, a person must confirm
            In("2.1.1"),                                      // only a person can check
            In("2.4.3", automatedPass: true, manual: "pass"), // confirmed: not "not verified"
            In("1.4.3", violations: 1, automatedPass: true),  // failing: not "not verified"
        ]);

        Assert.Equal((2, 1), (result.NotVerified, result.ScannerChecked));
    }
}
