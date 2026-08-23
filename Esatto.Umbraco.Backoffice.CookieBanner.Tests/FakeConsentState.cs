using Esatto.Umbraco.Backoffice.CookieBanner;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

/// <summary>
/// Hand-written rather than substituted: these tests assert on category gating, so the fake must
/// reproduce the real rule that a pending decision grants nothing but Necessary.
/// </summary>
internal sealed class FakeConsentState(params ConsentCategory[] granted) : IConsentState
{
    private readonly HashSet<ConsentCategory> _granted = granted.ToHashSet();

    public ConsentDecision? Decision => new(1, DateTimeOffset.UtcNow, "test", _granted);

    public bool NeedsDecision { get; init; }

    public bool HasGranted(ConsentCategory category)
        => category == ConsentCategory.Necessary || (NeedsDecision is false && _granted.Contains(category));
}
