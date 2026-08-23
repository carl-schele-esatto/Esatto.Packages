using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class ConsentDecisionTests
{
    private static ConsentDecision Decision(int policyVersion, params ConsentCategory[] granted)
        => new(policyVersion, new DateTimeOffset(2026, 8, 21, 9, 12, 33, TimeSpan.Zero), "abc123", granted.ToHashSet());

    // Pins that necessary is implied, not stored: it is absent from the cookie by design, so
    // asking the decision about it must still answer true.
    [Fact]
    public void Necessary_is_granted_even_though_it_is_never_stored()
    {
        ConsentDecision decision = Decision(1);

        Assert.True(decision.HasGranted(ConsentCategory.Necessary));
        Assert.DoesNotContain(ConsentCategory.Necessary, decision.Granted);
    }

    // Pins the gate the tag helpers read: only the categories actually in the set are granted.
    [Fact]
    public void Reports_only_the_granted_categories()
    {
        ConsentDecision decision = Decision(1, ConsentCategory.Statistics);

        Assert.True(decision.HasGranted(ConsentCategory.Statistics));
        Assert.False(decision.HasGranted(ConsentCategory.Marketing));
        Assert.False(decision.HasGranted(ConsentCategory.Preferences));
    }

    // Pins the re-prompt rule that makes PolicyVersion useful: an older stored version re-prompts,
    // an equal or newer one does not (so bumping the option cannot loop the banner forever).
    [Fact]
    public void Needs_reprompt_only_when_stored_version_is_older()
    {
        ConsentDecision decision = Decision(1);

        Assert.False(decision.NeedsRePrompt(1));
        Assert.True(decision.NeedsRePrompt(2));
        Assert.False(Decision(3).NeedsRePrompt(2));
    }
}
