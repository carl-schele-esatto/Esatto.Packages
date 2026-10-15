namespace Esatto.Umbraco.Backoffice.AICosts.Tests;

/// <summary>A settable clock for services that take a <see cref="TimeProvider"/>.</summary>
public sealed class TestClock(DateTime utcNow) : TimeProvider
{
    private DateTimeOffset _now = new(utcNow);
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}
