using System.Runtime.CompilerServices;

namespace Esatto.Umbraco.Backoffice.SeoReport.Reporting;

internal static class PeriodGuard
{
    /// <summary>
    /// Refuses <c>default(Period)</c>, year 0 and month 0: the one value of the type that skips its validation, so the typed
    /// period at the service boundary really does mean "a month that exists". The message is fixed text.
    /// </summary>
    public static void Require(Period period, [CallerArgumentExpression(nameof(period))] string? name = null)
    {
        if (period == default) throw new ArgumentException("The period is not set.", name);
    }
}
