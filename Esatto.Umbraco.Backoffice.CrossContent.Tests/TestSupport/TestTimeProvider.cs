namespace Esatto.Umbraco.Backoffice.CrossContent.Tests.TestSupport;

/// <summary>A TimeProvider whose "now" is settable, for cache-window tests.</summary>
public sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _now;
    public TestTimeProvider(DateTimeOffset start) => _now = start;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}
